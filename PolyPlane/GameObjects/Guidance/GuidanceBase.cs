using PolyPlane.GameObjects.Tools;
using PolyPlane.Helpers;

namespace PolyPlane.GameObjects.Guidance
{
    public abstract class GuidanceBase
    {
        public D2DPoint ImpactPoint { get; set; }
        public D2DPoint CurrentAimPoint { get; set; }

        protected GuidedMissile Missile { get; set; }
        public GameObject Target { get; set; }

        private SensorType _sensorType = SensorType.HeatSeek;

        private const float ARM_TIME = 3f;
        private const float SENSOR_FOV = World.SENSOR_FOV * 1f;
        private const float SENSOR_FOV_DECOY = World.SENSOR_FOV * 0.3f;

        private GameTimer _lostLockTimer = new GameTimer(15f);
        private GameTimer _groundScatterTimer = new GameTimer(4f);
        private GameTimer _armTimer = new GameTimer(ARM_TIME);
        private GameTimer _decoyDistractCooldown = new GameTimer(3f);
        private GameTimer _pitbullTimer = new GameTimer(6f);

        public bool GroundScatterInCooldown
        {
            get
            {
                if (_groundScatterTimer.IsRunning)
                    return true;

                if (!_groundScatterTimer.IsRunning)
                {
                    _groundScatterTimer.Restart();
                    return false;
                }

                return false;
            }
        }

        public bool LostInGround
        {
            get { return _lostInGround; }
            set { _lostInGround = value; }
        }

        public bool MissedTarget => _missedTarget || _lostInGround;

        private bool _missedTarget = false;
        private bool _lostInGround = false;
        private bool _isArmed = false;
        private float _missedTargetRot = 0f;

        protected GuidanceBase(GuidedMissile missile, GameObject target, SensorType sensorType = SensorType.Radar)
        {
            Missile = missile;
            Target = target;
            _sensorType = sensorType;

            _lostLockTimer.TriggerCallback = () =>
            {
                _missedTarget = true;
            };

            _pitbullTimer.TriggerCallback = () =>
            {
                if (this.Target.IsExpired || (this.Target is FighterPlane plane && plane.IsDisabled))
                {
                    DoPitBull();
                }
            };

            _armTimer.TriggerCallback = () => 
            {
                _isArmed = true; 
            };

            _pitbullTimer.Start();
            _decoyDistractCooldown.Start();
        }

        public float GuideTo(float dt)
        {
            if (this.Missile.FlameOn && !_armTimer.IsRunning && _isArmed == false)
                _armTimer.Start();

            if (_isArmed)
            {
                _lostLockTimer.Update(dt);
                _groundScatterTimer.Update(dt);
                _decoyDistractCooldown.Update(dt);
                _pitbullTimer.Update(dt);

                if (_sensorType == SensorType.HeatSeek)
                    DoDecoySuccessByTemperature();
                else
                    DoDecoySuccessBySize();

                DoGroundScatter();
            }

            _armTimer.Update(dt);

            // The guidance logic doesn't work when velo is zero (or very close).
            // Always return the current rotation if we aren't moving yet.
            if (Missile.Velocity.Length() == 0f)
                return Missile.Rotation;

            // Get rotation from implementation.
            var rotation = GetGuidanceDirection(dt);

            var isInFOV = Missile.IsObjInFOV(Target, SENSOR_FOV);

            if (!isInFOV)
            {
                if (!_missedTarget && !_lostLockTimer.IsRunning && _isArmed)
                    _lostLockTimer.Restart();
            }
            else
            {
                if (_missedTarget || _lostLockTimer.IsRunning)
                {
                    _lostLockTimer.Stop();
                    _missedTarget = false;
                }
            }

            // If we lost lock, aim at the last know target position and hope we can find it again.
            if (_missedTarget || _lostInGround)
                rotation = _missedTargetRot;

            return rotation;
        }

        protected D2DPoint GetTargetPosition()
        {
            var pos = this.Target.Position;

            // Heat seekers will aim for the engine exhaust.
            if (_sensorType == SensorType.HeatSeek)
            {
                if (this.Target is FighterPlane plane)
                    pos = plane.ExhaustPosition;
            }

            // Try to compensate for lag?
            if (this.Target.IsNetObject && this.Target is GameObjectNet netObj)
                pos = this.Target.Position + (this.Target.Velocity * ((float)(netObj.LagAmount * 2f) / 1000f));

            return pos;
        }


        /// <summary>
        /// Consider distracting missiles with decoys.
        /// 
        /// Goes after decoys if their apparent size is larger than the profile of the target plane.
        /// </summary>
        protected void DoDecoySuccessBySize()
        {
            // Test for decoy success.
            const float MAX_DISTANCE = 20000f; // Max distance for decoys to be considered.

            var decoys = World.ObjectManager.Decoys;

            var missile = this.Missile;
            var owner = this.Missile.Owner as FighterPlane;
            var ownerRadar = owner.Radar;
            var target = missile.Target as FighterPlane;

            if (target == null)
                return;

            if (missile == null)
                return;

            // No sense in trying to control missiles we don't have control of...
            if (missile.IsNetObject)
                return;

            GameObject maxSizeObj = target;
            var maxDiam = 0f;
            var targetDiam = Utilities.AngularSize(target, this.Missile.Position);

            for (int k = 0; k < decoys.Count; k++)
            {
                var decoy = decoys[k] as Decoy;

                if (decoy.IsExpired)
                    continue;

                if (decoy.Owner.Equals(this.Missile.Owner))
                    continue;

                if (!missile.IsObjInFOV(decoy, SENSOR_FOV_DECOY))
                    continue;

                var dist = D2DPoint.Distance(decoy.Position, missile.Position);

                if (dist > MAX_DISTANCE)
                    continue;

                var decoyDiam = Utilities.AngularSize(decoy, this.Missile.Position);

                if (decoyDiam > targetDiam)
                {
                    maxDiam = decoyDiam;
                    maxSizeObj = decoy;
                }
            }

            if (maxSizeObj is Decoy)
            {
                // Decrease likelihood of decoy distractions when owner plane is locked onto the target.
                const int IN_FOV_CHANCE = 4;

                if (ownerRadar.LockedObj != null && ownerRadar.LockedObj.Equals(this.Target))
                    DoChangeTargetChance(maxSizeObj, IN_FOV_CHANCE);
                else
                    DoChangeTargetChance(maxSizeObj, 0);
            }
        }


        /// <summary>
        /// Consider distracting missiles with decoys.
        /// 
        /// Goes after decoys when their apparent tempurature is greater than the target plane exhaust.
        /// </summary>
        protected void DoDecoySuccessByTemperature()
        {
            // Test for decoy success.
            const float MAX_DISTANCE = 20000f; // Max distance for decoys to be considered.

            var decoys = World.ObjectManager.Decoys;

            var missile = this.Missile;
            var owner = this.Missile.Owner as FighterPlane;
            var ownerRadar = owner.Radar;
            var target = missile.Target as FighterPlane;

            if (target == null)
                return;

            if (missile == null)
                return;

            // No sense in trying to control missiles we don't have control of...
            if (missile.IsNetObject)
                return;

            GameObject maxTempObj;
            var maxTemp = 0f;
            const float MaxEngineTemp = 1800f;
            const float MaxDecoyTemp = 2000f;

            const float EngineRadius = 6f;
            const float DecoyRadius = 2f;

            var targetDist = D2DPoint.Distance(missile.Position, target.Position);
            var targetTemp = MaxEngineTemp * target.ThrustAmount * EngineRadius;
            var engineArea = 4f * MathF.PI * MathF.Pow(targetDist, 2f);
            targetTemp /= engineArea;

            maxTempObj = target;
            maxTemp = targetTemp;

            for (int k = 0; k < decoys.Count; k++)
            {
                var decoy = decoys[k];

                if (decoy.IsExpired)
                    continue;

                if (decoy.Owner.Equals(this.Missile.Owner))
                    continue;

                if (!missile.IsObjInFOV(decoy, SENSOR_FOV_DECOY))
                    continue;

                var dist = D2DPoint.Distance(decoy.Position, missile.Position);

                if (dist > MAX_DISTANCE)
                    continue;

                var decoyTemp = (MaxDecoyTemp * DecoyRadius) / (4f * MathF.PI * MathF.Pow(dist, 2f));

                if (decoyTemp > maxTemp)
                {
                    maxTemp = decoyTemp;
                    maxTempObj = decoy;
                }
            }

            if (maxTempObj is Decoy)
            {
                // Decrease likelihood of decoy distractions when owner plane is locked onto the target.
                const int IN_FOV_CHANCE = 4;

                if (ownerRadar.LockedObj != null && ownerRadar.LockedObj.Equals(this.Target))
                    DoChangeTargetChance(maxTempObj, IN_FOV_CHANCE);
                else
                    DoChangeTargetChance(maxTempObj, 0);
            }
        }

        /// <summary>
        /// Consider losing target in ground scatter.
        /// </summary>
        protected void DoGroundScatter()
        {
            const float MIN_DISTANCE = 10000f; // Min distance for ground scatter to be considered.
            const float GROUND_SCATTER_ALT = 3000f;

            if (this.Missile.IsNetObject)
                return;

            if (this.Missile.Guidance == null)
                return;

            // Don't do ground scatter if the missile is lower than the target.
            if (this.Missile.Altitude <= this.Target.Altitude)
                return;

            if (this.Missile.Target != null)
            {
                var dist = this.Missile.Position.DistanceTo(this.Missile.Target.Position);

                if (dist < MIN_DISTANCE)
                    return;

                if (this.Missile.Target.Altitude <= GROUND_SCATTER_ALT)
                {
                    const int CHANCE_INIT = 10;
                    var chance = CHANCE_INIT;

                    // Increase chance for lower altitude.
                    var altFact = 1f - Utilities.Factor(this.Missile.Target.Altitude, GROUND_SCATTER_ALT);

                    chance -= (int)(altFact * 5);

                    if (!this.GroundScatterInCooldown)
                    {
                        var rnd = Utilities.Rnd.Next(chance);
                        if (rnd == 0)
                        {
                            this.LostInGround = true;
                            //_missedTargetRot = 90f; // Send missile into ground.
                            _missedTargetRot = this.Missile.Rotation; ; // Hold current rotation.

                            Log.Msg("Lost in ground scatter....");
                        }
                    }
                }
                else
                {
                    this.LostInGround = false;
                }
            }
        }

        // Look for and target other nearby planes within the current FOV.
        private void DoPitBull()
        {
            const float MAX_DIST = 40000f;

            var planes = World.ObjectManager.Planes.Where(p =>
            !p.ID.Equals(this.Missile.Owner.ID) &&
            this.Missile.IsObjInFOV(p, SENSOR_FOV) &&
            !p.IsDisabled &&
            p.Position.DistanceTo(this.Missile.Position) < MAX_DIST);

            var nearest = planes.OrderBy(p => p.Position.DistanceTo(this.Missile.Position)).ToList();

            if (nearest.Count > 0)
            {
                var newTarget = nearest.First();
                if (!newTarget.ID.Equals(this.Target.ID))
                {
                    this.Target = newTarget;
                    _lostLockTimer.Stop();
                    _missedTarget = false;
                    _lostInGround = false;
                    this.Missile.ChangeTarget(newTarget);

                    return;
                }
            }

            _missedTarget = true;
            _missedTargetRot = this.Missile.Rotation;
        }

        private void DoChangeTargetChance(GameObject target, int chances)
        {
            if (_decoyDistractCooldown.IsRunning)
                return;

            var randOChanceO = Utilities.Rnd.Next(chances);
            var lucky = randOChanceO == 0;

            if (lucky)
            {
                this.Missile.ChangeTarget(target);
                _decoyDistractCooldown.Reset();
                _decoyDistractCooldown.Start();
                Log.Msg("Missile distracted!");
            }
            else
            {
                _decoyDistractCooldown.Reset();
                _decoyDistractCooldown.Start();
                Log.Msg("Nice try!");
            }
        }

        /// <summary>
        /// Implement guidance, dummy!
        /// </summary>
        /// <param name="dt"></param>
        /// <returns></returns>
        public abstract float GetGuidanceDirection(float dt);
    }
}
