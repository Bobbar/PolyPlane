using PolyPlane.GameObjects;
using PolyPlane.Helpers;

namespace PolyPlane.AI_Behavior
{
    public class FighterPlaneAI : IAIBehavior
    {
        public FighterPlane Plane => _plane;
        public FighterPlane TargetPlane => _targetPlane;
        public Missile DefendingMissile = null;
        public AIPersonality Personality { get; set; }

        private FighterPlane _plane;
        private FighterPlane _targetPlane;
        private float _sineWavePos = 0f;
        private float _avoidingGroundAlt = 0f;
        private bool _avoidingGround = false;
        private bool _gainingVelo = false;
        private bool _reverseDirection = false;
        private bool _isDefending = false;

        private GameTimer _fireBurstTimer = new GameTimer(2f, 6f);
        private GameTimer _fireMissileCooldown = new GameTimer(6f);
        private GameTimer _dropDecoysTimer = new GameTimer(2f, 3f);
        private GameTimer _changeTargetCooldown = new GameTimer(10f);

        private float MIN_MISSILE_TIME = 40f;
        private float MAX_MISSILE_TIME = 80f;
        private float MAX_SPEED = 1000f;
        private readonly float RUN_DISTANCE = 30000f; // How close before cowardly AI runs away.
        private readonly float MAX_DECOY_DIST = 20000f; // Max distance between missile and plane before dropping decoys.


        public FighterPlaneAI(FighterPlane plane, AIPersonality personality)
        {
            _plane = plane;
            Personality = personality;
            InitStuff();
        }

        private void InitStuff()
        {
            Plane.ThrustOn = true;
            Plane.AutoPilotOn = true;

            ConfigPersonality();

            _fireBurstTimer.StartCallback = () =>
            this.Plane.FiringBurst = true;

            _fireBurstTimer.TriggerCallback = () =>
            this.Plane.FiringBurst = false;

            _dropDecoysTimer.StartCallback = () => this.Plane.DroppingDecoy = true;
            _dropDecoysTimer.TriggerCallback = () => this.Plane.DroppingDecoy = false;

            _fireMissileCooldown = new GameTimer(Utilities.Rnd.NextFloat(MIN_MISSILE_TIME, MAX_MISSILE_TIME));
            _fireMissileCooldown.Start();
        }

        public void Update(float dt)
        {
            if (this.Plane.IsDisabled || this.Plane.HasCrashed)
                return;

            _sineWavePos += 0.3f * dt;

            if (_sineWavePos > 99999f)
                _sineWavePos = 0f;

            _fireBurstTimer.Update(dt);
            _fireMissileCooldown.Update(dt);
            _dropDecoysTimer.Update(dt);
            _changeTargetCooldown.Update(dt);

            if (TargetPlane != null)
            {
                ConsiderFireBurstAtTarget();
                ConsiderFireMissileAtTarget();
            }

            ConsiderDefendMissile();
            ConsiderNewTarget();
            ConsiderDropDecoy();

            var velo = this.Plane.AirSpeedTrue;

            if (velo > MAX_SPEED)
                this.Plane.ThrustOn = false;
            else
                this.Plane.ThrustOn = true;
        }

        private void ConfigPersonality()
        {
            switch (this.Personality)
            {
                case AIPersonality.Normal:

                    break;

                case AIPersonality.MissileHappy:
                    MIN_MISSILE_TIME = 20f;
                    MAX_MISSILE_TIME = 40f;

                    break;

                case AIPersonality.LongBursts:
                    _fireBurstTimer.Interval = 3f;
                    break;

                case AIPersonality.Cowardly:
                    MAX_SPEED = 700f;
                    this.Plane.Thrust = 700f;
                    break;

                case AIPersonality.Speedy:
                    MAX_SPEED = 2000f;
                    this.Plane.Thrust = 2000f;
                    break;
            }
        }

        private void ConsiderNewTarget()
        {
            if (this.TargetPlane == null || this.TargetPlane.IsExpired || this.TargetPlane.HasCrashed || this.TargetPlane.IsDisabled)
            {
                var rndTarg = this.Plane.Radar.FindNearestPlane();

                _targetPlane = rndTarg;

                if (_targetPlane != null)
                    Log.Msg($"Picked new target: {this.Plane.ID} -> {this.TargetPlane.ID} ");
            }
        }

        private void ConsiderFireMissileAtTarget()
        {
            if (_fireMissileCooldown.IsRunning)
                return;

            const float MAX_DIST = 50000f;
            const float MIN_DIST = 1500f;

            if (this.Plane.Radar.HasLock && this.Plane.Radar.LockedObj != null && this.Plane.Radar.LockedObj.Equals(TargetPlane))
            {
                var dist = this.Plane.Position.DistanceTo(this.Plane.Radar.LockedObj.Position);

                if (dist > MAX_DIST || dist < MIN_DIST)
                    return;

                var fov = this.Plane.FOVToObject(TargetPlane);

                if (fov > World.SENSOR_FOV * 0.5f)
                    return;

                this.Plane.FireMissile(this.Plane.Radar.LockedObj);

                _fireMissileCooldown = new GameTimer(Utilities.Rnd.NextFloat(MIN_MISSILE_TIME, MAX_MISSILE_TIME));
                _fireMissileCooldown.Restart();

                Log.Msg("Firing Missile");
            }
        }

        private void ConsiderFireBurstAtTarget()
        {
            if (_fireBurstTimer.IsRunning)
                return;

            // Give the burst cooldown a little bit of variation.
            _fireBurstTimer.Cooldown = Utilities.Rnd.NextFloat(4f, 6f);

            const float MIN_DIST = 2000f;
            const float MIN_OFFBORE = 10f;

            var plrDist = D2DPoint.Distance(TargetPlane.Position, this.Plane.Position);

            if (plrDist > MIN_DIST)
                return;

            var plrFOV = this.Plane.FOVToObject(TargetPlane);

            if (plrFOV <= MIN_OFFBORE)
            {
                _fireBurstTimer.Restart();
            }
        }

        private void ConsiderDefendMissile()
        {
            var threat = Plane.Radar.FindNearestThreat();

            DefendingMissile = threat;

            _isDefending = DefendingMissile != null;
        }

        private void ConsiderDropDecoy()
        {
            if (!_isDefending)
                return;
            else
            {
                if (DefendingMissile.Position.DistanceTo(this.Plane.Position) < MAX_DECOY_DIST)
                {
                    if (!_dropDecoysTimer.IsRunning)
                    {
                        _dropDecoysTimer.Restart();
                    }
                }
            }
        }

        public void ClearTarget()
        {
            _changeTargetCooldown.Reset();
            _targetPlane = null;
            ConsiderNewTarget();
        }

        public void ChangeTarget(FighterPlane plane)
        {
            if (_changeTargetCooldown.IsRunning)
                return;

            if (plane != null)
            {
                _targetPlane = plane;
                _changeTargetCooldown.Restart();
            }
        }

        public float GetAIGuidance()
        {
            const float MIN_IMPACT_TIME = 7f; // Min ground impact time to consider avoiding ground.
            const float BLOCK_PITCH_DOWN_ALT = 800f; // Do not allow pitch down angles below this altitude.

            var patrolDir = Utilities.ClampAngle(Utilities.RadsToDegrees((float)Math.Sin(_sineWavePos)));

            if (_reverseDirection)
                patrolDir = Utilities.ClampAngle(patrolDir + 180f);

            var groundImpactTime = Utilities.GroundImpactTime(this.Plane);
            var angle = patrolDir;

            if (TargetPlane != null)
            {
                var dirToPlayer = TargetPlane.Position - this.Plane.Position;

                // Fly away from target plane?
                if (this.Personality == AIPersonality.Cowardly && this.Plane.Position.DistanceTo(TargetPlane.Position) < RUN_DISTANCE)
                {
                    dirToPlayer *= -1f;
                    angle = dirToPlayer.Angle(true);
                    angle += patrolDir * 0.2f; // Incorporate a small amount of the sine wave so we 'bob & weave' a little bit.
                }
                else
                    angle = dirToPlayer.Angle(true);
            }

            // Run away from missile?
            if (_isDefending && DefendingMissile != null)
            {
                // Compute two tangential angles and choose the one closest to the current rotation.
                // We basically try to fly away and slightly perpendicular to the direction of the incoming missile.

                var angleAwayFromThreat = (this.Plane.Position - DefendingMissile.Position).Angle(true);

                const float DEFEND_ANGLE = 45f;
                var defendAngleOne = Utilities.ClampAngle(angleAwayFromThreat + DEFEND_ANGLE);
                var defendAngleTwo = Utilities.ClampAngle(angleAwayFromThreat - DEFEND_ANGLE);

                var diffOne = Utilities.AngleDiff(defendAngleOne, this.Plane.Rotation);
                var diffTwo = Utilities.AngleDiff(defendAngleTwo, this.Plane.Rotation);

                if (diffOne < diffTwo)
                    angle = defendAngleOne;
                else if (diffTwo < diffOne)
                    angle = defendAngleTwo;
            }

            // Try to lead the target if we are firing a burst.
            if (_fireBurstTimer.IsRunning && TargetPlane != null)
            {
                var aimAmt = LeadTarget(TargetPlane);
                angle = aimAmt;
            }

            // Level out if we get too slow.
            var velo = this.Plane.AirSpeedIndicated;
            if (velo < 180f)
                _gainingVelo = true;
            else if (velo > 250f)
                _gainingVelo = false;

            if (_gainingVelo)
                angle = Utilities.MaintainAltitudeAngle(this.Plane, this.Plane.Altitude - 200f);

            // Pitch up if we about to impact with ground.
            if (groundImpactTime > 0f && groundImpactTime < MIN_IMPACT_TIME && !_avoidingGround)
            {
                _avoidingGround = true;
                _avoidingGroundAlt = this.Plane.Altitude;

                if (_avoidingGroundAlt < 500f)
                    _avoidingGroundAlt = 500f;
            }

            if (groundImpactTime < 0f)
            {
                _avoidingGround = false;
            }

            // Climb until no longer in danger of ground collision.
            if (_avoidingGround)
                angle = Utilities.MaintainAltitudeAngle(this.Plane, _avoidingGroundAlt);

            // Stay within the spawn area when not actively targeting another plane.
            if (this.TargetPlane == null)
            {
                if (this.Plane.Position.X > World.PlaneSpawnRange.Y + 10000f && !_reverseDirection)
                    _reverseDirection = true;

                if (this.Plane.Position.X < World.PlaneSpawnRange.X - 10000f && _reverseDirection)
                    _reverseDirection = false;
            }

            // Start reversing the angle at very low altitude.
            // Pitch down becomes a pitch up.
            if (this.Plane.Altitude < BLOCK_PITCH_DOWN_ALT)
            {
                if (angle > 0f && angle < 180f)
                    angle = 360f - angle;
            }

            var finalAngle = angle;
            finalAngle = Utilities.ClampAngle(finalAngle);
            return finalAngle;
        }

        private float LeadTarget(GameObject target)
        {
            const float pValue = 5f;

            var los = target.Position - this.Plane.Position;
            var navigationTime = los.Length() / (this.Plane.AirSpeedTrue * World.DT);
            var targRelInterceptPos = los + ((target.Velocity * World.DT) * navigationTime);

            targRelInterceptPos *= pValue;

            var leadRotation = ((target.Position + targRelInterceptPos) - this.Plane.Position).Angle(true);
            var targetRot = leadRotation;

            return targetRot;
        }
    }
}
