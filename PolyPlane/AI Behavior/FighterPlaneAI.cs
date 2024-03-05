using PolyPlane.GameObjects;
using unvell.D2DLib;

namespace PolyPlane.AI_Behavior
{
    public class FighterPlaneAI : GameObject, IAIBehavior
    {
        public Plane Plane => _plane;
        public Plane TargetPlane => _targetPlane;
        public Missile DefendingMissile = null;

        private Plane _plane;
        private Plane _targetPlane;
        private float _AIDirOffset = 0f;
        private float _sinePos = 0f;
        private bool _avoidingGround = false;
        private bool _gainingVelo = false;

        private GameTimer _fireBurstTimer = new GameTimer(2f);
        private GameTimer _fireBurstCooldownTimer = new GameTimer(6f);
        private GameTimer _fireMissileCooldown = new GameTimer(6f);

        private readonly float MIN_MISSILE_TIME = 40f;
        private readonly float MAX_MISSILE_TIME = 80f;


        private readonly float _maxSpeed = 1000f;

        public FighterPlaneAI(Plane plane, Plane targetPlane)
        {
            _plane = plane;
            _targetPlane = targetPlane;

            InitStuff();
        }

        private void InitStuff()
        {
            Plane.ThrustOn = true;
            Plane.AutoPilotOn = true;

            _fireMissileCooldown = new GameTimer(Helpers.Rnd.NextFloat(MIN_MISSILE_TIME, MAX_MISSILE_TIME));
            _fireMissileCooldown.Start();
        }

        public override void Update(float dt, D2DSize viewport, float renderScale)
        {
            if (this.Plane.IsDamaged || this.Plane.HasCrashed)
                return;

            _sinePos += 0.3f * dt;

            _fireBurstTimer.Update(dt);
            _fireBurstCooldownTimer.Update(dt);
            _fireMissileCooldown.Update(dt);

            if (TargetPlane != null)
            {
                ConsiderFireBurstAtTarget();
                ConsiderFireMissileAtTarget();
            }

            ConsiderDefendMissile();
            ConsiderNewTarget();
            ConsiderDropDecoy();

            if (_fireBurstTimer.IsRunning)
                this.Plane.FiringBurst = true;
            else
                this.Plane.FiringBurst = false;


            var velo = this.Plane.Velocity.Length();

            if (velo > _maxSpeed)
                this.Plane.ThrustOn = false;
            else
                this.Plane.ThrustOn = true;
        }

        private void ConsiderNewTarget()
        {
            if (this.TargetPlane == null || this.TargetPlane.IsExpired || this.TargetPlane.HasCrashed || this.TargetPlane.IsDamaged)
            {
                //var rndTarg = this.Plane.Radar.FindRandomPlane();
                var rndTarg = this.Plane.Radar.FindNearestPlane();

                _targetPlane = rndTarg;

                if (_targetPlane != null)
                    Log.Msg($"Picked new target: {this.Plane.ID} -> {this.TargetPlane.ID} ");
            }
        }

        private void ConsiderFireMissileAtTarget()
        {
            const float MAX_DIST = 40000f;
            if (_fireMissileCooldown.IsRunning)
                return;

            if (this.Plane.Radar.HasLock && this.Plane.Radar.LockedObj != null)
            {
                var dist = this.Plane.Position.DistanceTo(this.Plane.Radar.LockedObj.Position);

                if (dist > MAX_DIST)
                    return;

                this.Plane.FireMissile(this.Plane.Radar.LockedObj);

                _fireMissileCooldown = new GameTimer(Helpers.Rnd.NextFloat(MIN_MISSILE_TIME, MAX_MISSILE_TIME));
                _fireMissileCooldown.Restart();

                Log.Msg("Firing Missile");
            }
        }

        private void ConsiderFireBurstAtTarget()
        {
            const float MIN_DIST = 2000f;
            const float MIN_OFFBORE = 10f;

            var plrDist = D2DPoint.Distance(TargetPlane.Position, this.Plane.Position);

            if (plrDist > MIN_DIST)
                return;

            var plrFOV = this.Plane.FOVToObject(TargetPlane);

            if (plrFOV <= MIN_OFFBORE && !_fireBurstCooldownTimer.IsRunning && !_fireBurstTimer.IsRunning)
            {
                Log.Msg("FIRING BURST AT PLAYER!");

                _fireBurstTimer.TriggerCallback = () => _fireBurstCooldownTimer.Restart();
                _fireBurstTimer.Restart();
            }
        }

        private void ConsiderDefendMissile()
        {
            var threat = Plane.Radar.FindNearestThreat();

            DefendingMissile = threat;

            this.Plane.IsDefending = DefendingMissile != null;
        }

        private void ConsiderDropDecoy()
        {
            if (!this.Plane.IsDefending)
                return;
            else
                this.Plane.DropDecoys();
        }

        public float GetAIGuidance()
        {
            var angle = Helpers.ClampAngle(Helpers.RadsToDegrees((float)Math.Sin(_sinePos)));
            var toRight = Helpers.IsPointingRight(this.Plane.Rotation);
            var groundPos = new D2DPoint(this.Plane.Position.X, 0f);
            var impactTime = Helpers.ImpactTime(this.Plane, groundPos);

            if (TargetPlane != null)
            {
                var dirToPlayer = TargetPlane.Position - this.Plane.Position;
                angle = dirToPlayer.Angle(true);
            }

            // Run away from missile?
            if (this.Plane.IsDefending && DefendingMissile != null)
            {
                var angleToThreat = (DefendingMissile.Position - this.Plane.Position).Angle(true);
                angle = Helpers.ClampAngle(angleToThreat + 90f);
            }

            // Try to lead the target if we are firing a burst.
            if (_fireBurstTimer.IsRunning && TargetPlane != null)
            {
                var aimAmt = LeadTarget(TargetPlane);
                angle = aimAmt;
            }

            // Pitch up if we get too low.
          

            if (this.Plane.Altitude < 4000f || impactTime < 20f)
            {
                _avoidingGround = true;
            }

            if (_avoidingGround && this.Plane.Altitude > 6000f)
            {
                _avoidingGround = false;
            }

            // Try to pitch up in the same direction we're pointing.
            if (_avoidingGround)
            {

                if (toRight)
                    angle = 300f;
                else
                    angle = 240f;
            }

            var velo = this.Plane.Velocity.Length();
            if (velo < 100f)
                _gainingVelo = true;
            else if (velo > 200f)
                _gainingVelo = false;

            if (_gainingVelo)
            {
                if (toRight)
                    angle = 0f;
                else
                    angle = 180f;
            }

            var finalAngle = angle;
            finalAngle = Helpers.ClampAngle(finalAngle);
            return finalAngle;
        }

        private float LeadTarget(GameObject target)
        {
            const float pValue = 5f;

            var los = target.Position - this.Plane.Position;
            var navigationTime = los.Length() / (this.Plane.Velocity.Length() * World.DT);
            var targRelInterceptPos = los + ((target.Velocity * World.DT) * navigationTime);

            targRelInterceptPos *= pValue;

            var leadRotation = ((target.Position + targRelInterceptPos) - this.Plane.Position).Angle(true);
            var targetRot = leadRotation;

            return targetRot;
        }
    }
}
