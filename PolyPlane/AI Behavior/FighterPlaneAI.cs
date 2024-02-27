using PolyPlane.GameObjects;

namespace PolyPlane.AI_Behavior
{
    public class FighterPlaneAI : IAIBehavior
    {
        public Plane Plane => _plane;

        public Plane PlayerPlane => _playerPlane;


        private Plane _plane;
        private Plane _playerPlane;
        private float _AIDirOffset = 0f;
        private float _sinePos = 0f;

        private GameTimer _fireBurstTimer = new GameTimer(2f);
        private GameTimer _fireBurstCooldownTimer = new GameTimer(6f);
        private readonly float _maxSpeed = 1000f;

        public FighterPlaneAI(Plane plane, Plane playerPlane)
        {
            _plane = plane;
            _playerPlane = playerPlane;

            InitStuff();
        }

        private void InitStuff()
        {
            Plane.ThrustOn = true;
            Plane.AutoPilotOn = true;
        }

        public void Update(float dt)
        {
            _sinePos += 0.3f * dt;

            _fireBurstTimer.Update(dt);
            _fireBurstCooldownTimer.Update(dt);

            ConsiderFireAtPlayer();

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

        private void ConsiderFireAtPlayer()
        {
            const float MIN_DIST = 1000f;
            const float MIN_OFFBORE = 30f;

            var plrDist = D2DPoint.Distance(PlayerPlane.Position, this.Plane.Position);

            if (plrDist > MIN_DIST)
                return;

            var plrFOV = this.Plane.FOVToObject(PlayerPlane);

            if (plrFOV <= MIN_OFFBORE && !_fireBurstCooldownTimer.IsRunning && !_fireBurstTimer.IsRunning)
            {
                //Debug.WriteLine("FIRING BURST AT PLAYER!");
                _fireBurstTimer.TriggerCallback = () => _fireBurstCooldownTimer.Restart();
                _fireBurstTimer.Restart();
            }
        }

        public float GetAIGuidance()
        {
            var dir = Helpers.ClampAngle(Helpers.RadsToDegrees((float)Math.Sin(_sinePos)) + _AIDirOffset);
            var dirToPlayer = this.Plane.Position - PlayerPlane.Position;
            var angle = dirToPlayer.Angle(true);

            if (PlayerPlane.HasCrashed)
                angle = dir;

            // Run away from player?
            if (this.Plane.IsDefending)
                angle = Helpers.ClampAngle(angle + 180f);

            // Pitch up if we get too low.
            if (this.Plane.Altitude < 3000f)
                angle = 90f;

            return angle;
        }
    }
}
