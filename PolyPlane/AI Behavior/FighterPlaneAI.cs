using PolyPlane.GameObjects;
using System.Diagnostics;

namespace PolyPlane.AI_Behavior
{
    public class FighterPlaneAI : IAIBehavior
    {
        public Plane Plane => _plane;

        public Plane TargetPlane => _targetPlane;

        public Missile DefendingMissile = null;

        private Plane _plane;
        private Plane _targetPlane;
        private float _AIDirOffset = 0f;
        private float _sinePos = 0f;

        private GameTimer _fireBurstTimer = new GameTimer(2f);
        private GameTimer _fireBurstCooldownTimer = new GameTimer(6f);

        private readonly float MIN_MISSILE_TIME = 10f;
        private readonly float MAX_MISSILE_TIME = 40f;

        private GameTimer _fireMissileCooldown = new GameTimer(6f);

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

        public void Update(float dt)
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

            const float MIN_VELO = 300f;

            var velo = this.Plane.Velocity.Length();

            if (this.Plane.Altitude < 3000f)
                _AIDirOffset = 90f;

            if (velo < MIN_VELO && this.Plane.Altitude > 3000f)
                _AIDirOffset = 20f;

            if (velo > _maxSpeed)
                this.Plane.ThrustOn = false;
            else
                this.Plane.ThrustOn = true;
        }


        private void ConsiderNewTarget()
        {
            if (this.TargetPlane == null || this.TargetPlane.IsExpired || this.TargetPlane.HasCrashed)
            {
                var rndTarg = this.Plane.Radar.FindRandomPlane();

                _targetPlane = rndTarg;

                if (_targetPlane != null)
                    Debug.WriteLine($"Picked new target: {this.Plane.ID} -> {this.TargetPlane.ID} ");
            }
        }

        private void ConsiderFireMissileAtTarget()
        {
            if (_fireMissileCooldown.IsRunning)
                return;

            if (this.Plane.Radar.HasLock && this.Plane.Radar.LockedObj != null)
            {
                this.Plane.FireMissile(this.Plane.Radar.LockedObj);

                _fireMissileCooldown = new GameTimer(Helpers.Rnd.NextFloat(MIN_MISSILE_TIME, MAX_MISSILE_TIME));
                _fireMissileCooldown.Restart();



                Debug.WriteLine("Firing Missile");

            }
        }

        private void ConsiderFireBurstAtTarget()
        {
            const float MIN_DIST = 1000f;
            const float MIN_OFFBORE = 30f;

            var plrDist = D2DPoint.Distance(TargetPlane.Position, this.Plane.Position);

            if (plrDist > MIN_DIST)
                return;

            var plrFOV = this.Plane.FOVToObject(TargetPlane);

            if (plrFOV <= MIN_OFFBORE && !_fireBurstCooldownTimer.IsRunning && !_fireBurstTimer.IsRunning)
            {
                Debug.WriteLine("FIRING BURST AT PLAYER!");
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
            var angle = Helpers.ClampAngle(Helpers.RadsToDegrees((float)Math.Sin(_sinePos)) + _AIDirOffset);

            if (TargetPlane != null)
            {
                var dirToPlayer = this.Plane.Position - TargetPlane.Position;
                angle = dirToPlayer.Angle(true);
            }


            // Run away from missile?
            if (this.Plane.IsDefending && DefendingMissile != null)
            {
                angle = Helpers.ClampAngle(((this.Plane.Position - DefendingMissile.Position).Angle(true)) + 180f);
            }

            // Pitch up if we get too low.
            if (this.Plane.Altitude < 3000f)
                angle = 90f;

            return angle;
        }
    }
}
