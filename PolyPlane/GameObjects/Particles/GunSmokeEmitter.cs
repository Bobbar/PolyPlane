using PolyPlane.GameObjects.Fixtures;
using PolyPlane.GameObjects.Interfaces;
using PolyPlane.GameObjects.Tools;
using PolyPlane.Helpers;
using unvell.D2DLib;

namespace PolyPlane.GameObjects.Particles
{
    public class GunSmokeEmitter : FixturePoint, INoGameID
    {
        private GameTimer _spawnTimer;
        private D2DColor _smokeColor = new D2DColor(0.3f, D2DColor.White);
        private const int MAX_PARTS = 20;
        private const float MAX_AGE = 1f;
        private const float RADIUS = 8f;

        public GunSmokeEmitter(GameObject obj, D2DPoint offset, D2DColor color) : base(obj, offset)
        {
            _smokeColor = color;

            _spawnTimer = AddTimer(0.05f, true);
            _spawnTimer.Interval = MAX_AGE / MAX_PARTS;

            _spawnTimer.TriggerCallback = () => SpawnPart(RADIUS);
            _spawnTimer.Start();
        }

        public override void DoUpdate(float dt)
        {
            base.DoUpdate(dt);

            if (Owner != null && Owner.IsExpired)
                _spawnTimer.Stop();

            IsExpired = Owner.IsExpired;
        }

        public void AddPuff()
        {
            SpawnPart(RADIUS + 7f);
        }

        private void SpawnPart(float radius)
        {
            if (!Visible)
                return;

            base.DoUpdate(0f);

            D2DPoint newPos = Position;
            D2DPoint newVelo = Velocity;

            var newRad = radius + Utilities.Rnd.NextFloat(-2f, 2f);
            var newColor = _smokeColor;

            var newPart = Particle.SpawnParticle(Owner, newPos, newVelo, newRad, newColor, newColor, ParticleType.Smoke);
            newPart.MaxAge = MAX_AGE;
            newPart.RenderOrder = 6;
        }
    }
}
