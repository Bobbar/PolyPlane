using PolyPlane.GameObjects.Fixtures;
using PolyPlane.GameObjects.Interfaces;
using PolyPlane.GameObjects.Tools;
using PolyPlane.Helpers;
using PolyPlane.Rendering;
using System.Diagnostics;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public class FlameEmitter : GameObject, INoGameID
    {
        public float Radius { get; set; }

        public const int MAX_PARTS = 75;
        public const float MAX_AGE = 30f;

        public static readonly D2DColor DefaultFlameColor = new D2DColor(0.6f, D2DColor.Yellow);
        public static readonly D2DColor BlackSmokeColor = new D2DColor(0.6f, D2DColor.Black);
        public static readonly D2DColor GraySmokeColor = new D2DColor(0.6f, D2DColor.Gray);

        private FixturePoint _refPos = null;
        private GameTimer _spawnTimer = new GameTimer(0.1f, true);
        private float _interval = 0f;

        public FlameEmitter(GameObject obj, D2DPoint offset, float radius = 10f) : base(obj.Position, obj.Velocity)
        {
            _interval = MAX_AGE / MAX_PARTS;
            _spawnTimer.Interval = _interval;

            this.Owner = obj;

            Radius = radius;
            _refPos = new FixturePoint(obj, offset);

            _spawnTimer.TriggerCallback = () => SpawnPart();
            _spawnTimer.Start();

            Update(World.DT);

            SpawnPart();
        }

        public FlameEmitter(GameObject obj, D2DPoint offset, bool hasFlame = true) : base(obj.Position, obj.Velocity)
        {
            _interval = MAX_AGE / MAX_PARTS;
            _spawnTimer.Interval = _interval;

            this.Owner = obj;

            Radius = Utilities.Rnd.NextFloat(4f, 15f);
            _refPos = new FixturePoint(obj, offset);

            _spawnTimer.TriggerCallback = () => SpawnPart();

            Update(World.DT);

            if (hasFlame)
            {
                _spawnTimer.Start();
                SpawnPart();
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
            
            _spawnTimer.Interval = _interval + Utilities.Rnd.NextFloat(-0.1f, 0.1f);

            _refPos.Update(World.DT);
            D2DPoint newPos = _refPos.Position;
            D2DPoint newVelo = _refPos.Velocity;
            newVelo += Utilities.RandOPoint(10f);

            var endColor = BlackSmokeColor;

            if (_refPos.GameObject is FighterPlane plane && !plane.IsDisabled)
                endColor = GraySmokeColor;

            var newRad = this.Radius + Utilities.Rnd.NextFloat(-3f, 3f);
            var newPart = World.ObjectManager.RentFlamePart();
            newPart.ReInit(newPos, newRad, endColor, newVelo);
            newPart.Owner = this;

            World.ObjectManager.EnqueueFlame(newPart);
        }

        public override void Dispose()
        {
            base.Dispose();

            _spawnTimer.Stop();
        }
    }
}
