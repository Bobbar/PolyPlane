using PolyPlane.GameObjects.Fixtures;
using PolyPlane.GameObjects.Tools;
using PolyPlane.Helpers;
using unvell.D2DLib;

namespace PolyPlane.GameObjects.Particles
{
    public abstract class ParticleEmitter : FixturePoint
    {
        private const float DEFAULT_INTERVAL = 0.4f;

        private GameTimer _spawnTimer;
        private ParticleType _particleType = ParticleType.Flame;

        private float _minRadius = 1f;
        private float _maxRadius = 1f;

        public ParticleEmitter(GameObject owner, D2DPoint posOffset, float minRadius, float maxRadius, bool startImmediately = true, ParticleType particleType = ParticleType.Flame) : base(owner, posOffset)
        {
            _particleType = particleType;
            _minRadius = minRadius;
            _maxRadius = maxRadius;

            _spawnTimer = AddTimer(DEFAULT_INTERVAL, true);

            _spawnTimer.TriggerCallback = SpawnParticle;
            _spawnTimer.StartCallback = SpawnParticle;

            if (startImmediately)
                _spawnTimer.Start();
        }

        public abstract D2DColor GetStartColor();
        public abstract D2DColor GetEndColor();

        public override void DoUpdate(float dt)
        {
            base.DoUpdate(dt);

            if (Owner != null && Owner.IsExpired)
                _spawnTimer.Stop();
        }

        private void SpawnParticle()
        {
            if (World.IsNetGame && World.IsServer)
                return;

            // Make sure our position is synced to ensure a correct angular velocity result.
            this.SyncWithOwner();

            _spawnTimer.Interval = DEFAULT_INTERVAL + Utilities.Rnd.NextFloat(-0.1f, 0.1f);

            D2DPoint newPos = Position;
            D2DPoint newVelo = Utilities.AngularVelocity(Owner, Position);

            newVelo += Utilities.RandOPoint(10f);

            var startColor = GetStartColor();
            var endColor = GetEndColor();
            var radius = Utilities.Rnd.NextFloat(_minRadius, _maxRadius);

            Particle.SpawnParticle(Owner, newPos, newVelo, radius, startColor, endColor, _particleType);
        }

        /// <summary>
        /// Stop spawning new flame parts.
        /// </summary>
        public void StopSpawning()
        {
            _spawnTimer.Stop();
        }

        /// <summary>
        /// Start spawning new flame parts.
        /// </summary>
        public void StartSpawning()
        {
            _spawnTimer.Start();
        }
    }
}
