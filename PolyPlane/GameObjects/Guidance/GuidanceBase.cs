using PolyPlane.Helpers;

namespace PolyPlane.GameObjects.Guidance
{
    public abstract class GuidanceBase
    {
        public D2DPoint ImpactPoint { get; set; }
        public D2DPoint StableAimPoint { get; set; }
        public D2DPoint CurrentAimPoint { get; set; }

        protected GuidedMissile Missile { get; set; }
        public GameObject Target { get; set; }

        private const float ARM_TIME = 3f;

        private GameTimer _lostLockTimer = new GameTimer(3f);
        private GameTimer _groundScatterTimer = new GameTimer(5f);
        private GameTimer _armTimer = new GameTimer(ARM_TIME);

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

        protected GuidanceBase(GuidedMissile missile, GameObject target)
        {
            Missile = missile;
            Target = target;
            _lostLockTimer.TriggerCallback = () => _missedTarget = true;

            _armTimer.Start();
        }

        public float GuideTo(float dt)
        {
            DoDecoySuccess();
            DoGroundScatter();

            _lostLockTimer.Update(dt);
            _groundScatterTimer.Update(dt);
            _armTimer.Update(dt);

            // The guidance logic doesn't work when velo is zero (or very close).
            // Always return the current rotation if we aren't moving yet.
            if (Missile.Velocity.Length() == 0f)
                return Missile.Rotation;

            var rotFactor = 1f;
            var veloAngle = Missile.Velocity.Angle(true);
            var initialAngle = veloAngle;

            // Get rotation from implementation.
            var rotation = GetGuidanceDirection(dt);

            var isInFOV = Missile.IsObjInFOV(Target, World.SENSOR_FOV);

            if (!isInFOV)
            {
                if (!_missedTarget && !_lostLockTimer.IsRunning && !_armTimer.IsRunning)
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

            if (_missedTarget || _lostInGround)
                rotation = Missile.Rotation;

            // Lerp from current rotation towards guidance rotation as we 
            // approach the specified arm time.
            var armFactor = Utilities.Factor(_armTimer.Value, _armTimer.Interval);
            var finalRot = Utilities.LerpAngle(initialAngle, rotation, rotFactor * armFactor);

            return finalRot;
        }

        protected D2DPoint GetTargetPosition()
        {
            //if (this.Target is Plane plane)
            //    return plane.ExhaustPosition;
            //else

            var pos = this.Target.Position;

            // Try to compensate for lag?
            if (this.Target.IsNetObject)
                pos = this.Target.Position + (this.Target.Velocity * ((float)(this.Target.LagAmount * 2f) / 1000f));

            return this.Target.Position;
        }

        /// <summary>
        /// Consider distracting missiles with decoys.
        /// </summary>
        protected void DoDecoySuccess()
        {
            // Test for decoy success.
            const float MIN_DECOY_FOV = 10f;
            var decoys = World.ObjectManager.Decoys;

            var missile = this.Missile;
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
            const float MaxDecoyTemp = 3000f;

            const float EngineRadius = 4f;
            const float DecoyRadius = 2f;

            var targetDist = D2DPoint.Distance(missile.Position, target.Position);
            var targetTemp = MaxEngineTemp * target.ThrustAmount * EngineRadius;
            var engineArea = 4f * (float)Math.PI * (float)Math.Pow(targetDist, 2f);
            targetTemp /= engineArea;

            maxTempObj = target;
            maxTemp = targetTemp;

            for (int k = 0; k < decoys.Count; k++)
            {
                var decoy = decoys[k];

                if (!missile.IsObjInFOV(decoy, MIN_DECOY_FOV))
                    continue;

                //if (missile.Owner.IsObjInFOV(target, World.SENSOR_FOV * 0.25f) && )
                //    continue;

                var dist = D2DPoint.Distance(decoy.Position, missile.Position);
                var decoyTemp = (MaxDecoyTemp * DecoyRadius) / (4f * (float)Math.PI * (float)Math.Pow(dist, 2f));

                if (decoyTemp > maxTemp)
                {
                    maxTemp = decoyTemp;
                    maxTempObj = decoy;
                }

            }

            if (maxTempObj is Decoy)
            {
                missile.DoChangeTargetChance(maxTempObj);
            }
        }

        /// <summary>
        /// Consider losing target in ground scatter.
        /// </summary>
        protected void DoGroundScatter()
        {
            const float GROUND_SCATTER_ALT = 3000f;

            if (this.Missile.IsNetObject)
                return;

            if (this.Missile.Guidance == null)
                return;

            if (this.Missile.Target != null)
            {
                if (this.Missile.Target.Altitude <= GROUND_SCATTER_ALT)
                {
                    const int CHANCE_INIT = 10;
                    var chance = CHANCE_INIT;

                    // Increase chance for lower altitude.
                    var altFact = 1f - Utilities.Factor(this.Missile.Target.Altitude, GROUND_SCATTER_ALT);

                    chance -= (int)(altFact * 5);

                    if (!this.Missile.Guidance.GroundScatterInCooldown)
                    {
                        var rnd1 = Utilities.Rnd.Next(chance);
                        var rnd2 = Utilities.Rnd.Next(chance);
                        if (rnd1 == rnd2)
                        {
                            this.Missile.Guidance.LostInGround = true;
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

        /// <summary>
        /// Implement guidance, dummy!
        /// </summary>
        /// <param name="dt"></param>
        /// <returns></returns>
        public abstract float GetGuidanceDirection(float dt);
    }
}
