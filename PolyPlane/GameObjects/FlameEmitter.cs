using PolyPlane.GameObjects.Fixtures;
using PolyPlane.GameObjects.Interfaces;
using PolyPlane.GameObjects.Tools;
using PolyPlane.Helpers;
using PolyPlane.Rendering;
using System.Diagnostics;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public class FlameEmitter : GameObject
    {
        public float Radius { get; set; }

        public const int MAX_PARTS = 50;
        public const float MAX_AGE = 20f;

        public static readonly D2DColor DefaultFlameColor = new D2DColor(0.6f, D2DColor.Yellow);
        public static readonly D2DColor BlackSmokeColor = new D2DColor(0.6f, D2DColor.Black);
        public static readonly D2DColor GraySmokeColor = new D2DColor(0.6f, D2DColor.Gray);

        private FixturePoint _refPos = null;
        private GameTimer _spawnTimer = new GameTimer(0.1f, true);

        public FlameEmitter(GameObject obj, D2DPoint offset, float radius = 10f) : base(obj.Position, obj.Velocity)
        {
            _spawnTimer.Interval = MAX_AGE / MAX_PARTS;

            this.Owner = obj;
            this.PlayerID = obj.PlayerID;

            Radius = radius;
            _refPos = new FixturePoint(obj, offset);

            _spawnTimer.TriggerCallback = () => SpawnPart();
            _spawnTimer.Start();

            Update(World.DT);

            SpawnPart();
        }

        public FlameEmitter(GameObject obj, D2DPoint offset, bool hasFlame = true) : base(obj.Position, obj.Velocity)
        {
            _spawnTimer.Interval = MAX_AGE / MAX_PARTS;

            this.Owner = obj;
            this.PlayerID = obj.PlayerID;

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

            _refPos.Update(World.DT);
            D2DPoint newPos = _refPos.Position;
            D2DPoint newVelo = _refPos.Velocity;
            newVelo += Utilities.RandOPoint(10f);

            var endColor = BlackSmokeColor;

            if (_refPos.GameObject is FighterPlane plane && !plane.IsDisabled)
                endColor = GraySmokeColor;

            var newRad = this.Radius + Utilities.Rnd.NextFloat(-3f, 3f);
            var newPart = World.ObjectManager.RentFlamePart(this.PlayerID);
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

    public class FlamePart : GameObject, ICollidable
    {
        public D2DEllipse Ellipse => _ellipse;
        public D2DColor Color { get; set; }
        public D2DColor StartColor { get; set; }
        public D2DColor EndColor { get; set; }

        private D2DEllipse _ellipse;

        private const float MIN_RISE_RATE = -50f;
        private const float MAX_RISE_RATE = -70f;
        private const float WIND_SPEED = 20f; // Fake wind effect amount.

        private D2DPoint _riseRate;

        public FlamePart()
        {
            _ellipse = new D2DEllipse();
            this.RenderOrder = 0;
        }

        public void ReInit(D2DPoint pos, float radius, D2DColor endColor, D2DPoint velo)
        {
            this.Age = 0f;
            this.IsExpired = false;

            _ellipse.origin = pos;
            _ellipse.radiusX = radius;
            _ellipse.radiusY = radius;

            var newColor = new D2DColor(FlameEmitter.DefaultFlameColor.a, 1f, Utilities.Rnd.NextFloat(0f, 0.86f), FlameEmitter.DefaultFlameColor.b);

            Color = newColor;
            StartColor = newColor;
            EndColor = endColor;

            _riseRate = new D2DPoint(0f, Utilities.Rnd.NextFloat(MAX_RISE_RATE, MIN_RISE_RATE));

            this.Position = _ellipse.origin;
            this.Velocity = velo;
        }

        public override void Update(float dt)
        {
            base.Update(dt);

            var ageFactFade = 1f - Utilities.Factor(this.Age, FlameEmitter.MAX_AGE);
            var ageFactSmoke = Utilities.Factor(this.Age, FlameEmitter.MAX_AGE * 3f);
            var alpha = StartColor.a * ageFactFade;

            this.Color = Utilities.LerpColorWithAlpha(this.Color, this.EndColor, ageFactSmoke, alpha);
            this.Velocity += -this.Velocity * 0.9f * dt;
            this.Velocity += _riseRate * dt;

            // Simulate the particles being blown by the wind.
            _riseRate.X = WIND_SPEED * Utilities.FactorWithEasing(this.Age, FlameEmitter.MAX_AGE, EasingFunctions.EaseOutSine);

            _ellipse.origin = this.Position;

            if (this.Age > FlameEmitter.MAX_AGE)
                this.IsExpired = true;
        }

        public override void Render(RenderContext ctx)
        {
            base.Render(ctx);

            ctx.FillEllipse(Ellipse, Color);
        }

        public override void Dispose()
        {
            base.Dispose();

            World.ObjectManager.ReturnFlamePart(this);
        }
    }
}
