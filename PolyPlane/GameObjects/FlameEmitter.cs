using PolyPlane.GameObjects.Fixtures;
using PolyPlane.GameObjects.Interfaces;
using PolyPlane.GameObjects.Tools;
using PolyPlane.Helpers;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public class FlameEmitter : GameObject, INoGameID
    {
        public float Radius { get; set; }

        private const float DEFAULT_INTERVAL = 0.4f;

        private FixturePoint _refPos = null;
        private GameTimer _spawnTimer = new GameTimer(0.4f, true);
        private bool _hasFlame = true;

        public FlameEmitter(GameObject obj, D2DPoint offset, float radius = 10f) : base(obj.Position, obj.Velocity)
        {
            this.Owner = obj;

            Radius = radius;

            _refPos = new FixturePoint(obj, offset);
            _refPos.Update(0f);
            this.Position = _refPos.Position;
            this.Rotation = _refPos.Rotation;
            this.Velocity = _refPos.Velocity;

            _spawnTimer.TriggerCallback = () => SpawnPart();
            _spawnTimer.Start();
        }

        public FlameEmitter(GameObject obj, D2DPoint offset, bool hasFlame = true) : base(obj.Position, obj.Velocity)
        {
            _hasFlame = hasFlame;

            this.Owner = obj;

            Radius = Utilities.Rnd.NextFloat(4f, 15f);

            _refPos = new FixturePoint(obj, offset);
            _refPos.Update(0f);
            this.Position = _refPos.Position;
            this.Rotation = _refPos.Rotation;
            this.Velocity = _refPos.Velocity;

            _spawnTimer.TriggerCallback = () => SpawnPart();

            if (hasFlame)
            {
                _spawnTimer.Start();
            }
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

        public override void Update(float dt)
        {
            // Spawn a new particle on first frame as needed.
            if (this.Age == 0f && _hasFlame)
                SpawnPart();

            base.Update(dt);
            _spawnTimer.Update(dt);

            _refPos.Update(dt);
            this.Position = _refPos.Position;
            this.Rotation = _refPos.Rotation;
            this.Velocity = _refPos.Velocity;

            if (this.Owner != null && this.Owner.IsExpired)
                _spawnTimer.Stop();
        }

        public override void FlipY()
        {
            base.FlipY();
            this._refPos.FlipY();
        }

        private void SpawnPart()
        {
            if (World.IsNetGame && World.IsServer)
                return;

            _spawnTimer.Interval = DEFAULT_INTERVAL + Utilities.Rnd.NextFloat(-0.1f, 0.1f);

            _refPos.Update(0f);
            D2DPoint newPos = _refPos.Position;
            D2DPoint newVelo = _refPos.Velocity;
            newVelo += Utilities.RandOPoint(10f);

            var endColor = World.GraySmokeColor;

            // If we are attached to a plane, change the end color to black when disabled.
            var rootObj = this.FindRootObject();
            if (rootObj is FighterPlane plane && plane.IsDisabled)
                endColor = World.BlackSmokeColor;

            var newRad = this.Radius + Utilities.Rnd.NextFloat(-3f, 3f);
            var startColor = World.GetRandomFlameColor();

            Particle.SpawnParticle(this.Owner, newPos, newVelo, newRad, startColor, endColor);
        }

        public override void Dispose()
        {
            base.Dispose();

            _spawnTimer.Stop();
        }
    }
}
