using PolyPlane.Helpers;
using PolyPlane.Rendering;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public class Flame : GameObject
    {
        public float Radius { get; set; }

        public const int MAX_PARTS = 50;
        public const float MAX_AGE = 20f;

        private List<FlamePart> _parts = new List<FlamePart>();
        private D2DColor _flameColor = new D2DColor(0.6f, D2DColor.Yellow);
        private D2DColor _blackSmoke = new D2DColor(0.6f, D2DColor.Black);
        private D2DColor _graySmoke = new D2DColor(0.6f, D2DColor.Gray);
        private FixturePoint _refPos = null;
        private GameTimer _spawnTimer = new GameTimer(0.1f, true);

        public Flame(GameObject obj, D2DPoint offset, float radius = 10f) : base(obj.Position, obj.Velocity)
        {
            _spawnTimer.Interval = MAX_AGE / MAX_PARTS;

            this.Owner = obj;
            Radius = radius;
            _refPos = new FixturePoint(obj, offset);
            _refPos.Update(World.DT, World.RenderScale * obj.RenderOffset);

            _spawnTimer.TriggerCallback = () => SpawnPart();
            _spawnTimer.Start();

            this.Position = _refPos.Position;
            this.Rotation = _refPos.Rotation;

            SpawnPart();
        }

        public Flame(GameObject obj, D2DPoint offset, D2DPoint velo, float radius = 10f) : base(obj.Position, velo)
        {
            _spawnTimer.Interval = MAX_AGE / MAX_PARTS;

            Radius = radius;
            _refPos = new FixturePoint(obj, offset);
            _refPos.Update(World.DT, World.RenderScale * obj.RenderOffset);

            _spawnTimer.TriggerCallback = () => SpawnPart();
            _spawnTimer.Start();

            this.Position = _refPos.Position;
            this.Rotation = _refPos.Rotation;

            SpawnPart();
        }

        public Flame(GameObject obj, D2DPoint offset, bool hasFlame = true) : base(obj.Position, obj.Velocity)
        {
            _spawnTimer.Interval = MAX_AGE / MAX_PARTS;

            this.Owner = obj;
            Radius = Utilities.Rnd.NextFloat(4f, 15f);
            _refPos = new FixturePoint(obj, offset);
            _refPos.Update(World.DT, World.RenderScale * obj.RenderOffset);

            if (hasFlame)
            {
                _spawnTimer.TriggerCallback = () => SpawnPart();
                _spawnTimer.Start();
            }

            this.Position = _refPos.Position;
            this.Rotation = _refPos.Rotation;

            SpawnPart();
        }

        /// <summary>
        /// Stop spawning new flame parts.
        /// </summary>
        public void StopSpawning()
        {
            _spawnTimer.Stop();
        }

        public override void Update(float dt, float renderScale)
        {
            base.Update(dt, renderScale);
            _spawnTimer.Update(dt);
            UpdateParts(dt, renderScale);

            if (_refPos != null)
            {
                _refPos.Update(dt, renderScale);
                this.Position = _refPos.Position;
                this.Rotation = _refPos.Rotation;
            }

            if (this.Owner != null && this.Owner.IsExpired)
                _spawnTimer.Stop();

            if (_parts.Count == 0 && !_spawnTimer.IsRunning)
                this.IsExpired = true;

        }

        public override void Render(RenderContext ctx)
        {
            base.Render(ctx);

            _parts.ForEach(p => p.Render(ctx));
        }

        public void FlipY()
        {
            this._refPos.FlipY();
        }

        private void SpawnPart()
        {
            D2DPoint newPos = this.Position;

            if (_refPos != null)
                newPos = _refPos.Position;

            D2DPoint newVelo = this.Velocity;

            if (this.Owner != null)
                newVelo = this.Owner.Velocity;

            newVelo += Utilities.RandOPoint(10f);

            var endColor = _blackSmoke;

            if (_refPos.GameObject is FighterPlane plane && !plane.IsDisabled)
                endColor = _graySmoke;

            var newRad = this.Radius + Utilities.Rnd.NextFloat(-3f, 3f);
            var newColor = new D2DColor(_flameColor.a, 1f, Utilities.Rnd.NextFloat(0f, 0.86f), _flameColor.b);
            var newEllipse = new D2DEllipse(newPos, new D2DSize(newRad, newRad));
            var newPart = new FlamePart(newEllipse, newColor, endColor, newVelo);

            if (_parts.Count < MAX_PARTS)
                _parts.Add(newPart);
            else
            {
                _parts.RemoveAt(0);
                _parts.Add(newPart);
            }
        }

        private void UpdateParts(float dt, float renderScale)
        {
            int i = 0;
            while (i < _parts.Count)
            {
                var part = _parts[i];
                part.Update(dt, renderScale);

                var ageFactFade = 1f - Utilities.Factor(part.Age, MAX_AGE);
                var ageFactSmoke = Utilities.Factor(part.Age, MAX_AGE * 3f);
                var alpha = _flameColor.a * ageFactFade;

                part.Color = new D2DColor(alpha, Utilities.LerpColor(part.Color, part.EndColor, ageFactSmoke));

                if (part.Age > MAX_AGE)
                    _parts.RemoveAt(i);

                i++;
            }
        }

        private class FlamePart : GameObject
        {
            public D2DEllipse Ellipse => _ellipse;
            public D2DColor Color { get; set; }
            public D2DColor EndColor { get; set; }

            private D2DEllipse _ellipse;

            private const float MIN_RISE_RATE = -50f;
            private const float MAX_RISE_RATE = -70f;


            private D2DPoint _riseRate = new D2DPoint(0f, -50f);


            public FlamePart(D2DEllipse ellipse, D2DColor color, D2DPoint velo) : base(ellipse.origin, velo)
            {
                _ellipse = ellipse;
                Color = color;

                _riseRate = new D2DPoint(0f, Utilities.Rnd.NextFloat(MAX_RISE_RATE, MIN_RISE_RATE));
            }

            public FlamePart(D2DEllipse ellipse, D2DColor color, D2DColor endColor, D2DPoint velo) : base(ellipse.origin, velo)
            {
                _ellipse = ellipse;
                Color = color;
                EndColor = endColor;

                _riseRate = new D2DPoint(0f, Utilities.Rnd.NextFloat(MAX_RISE_RATE, MIN_RISE_RATE));
            }

            public override void Update(float dt, float renderScale)
            {
                base.Update(dt, renderScale);

                this.Velocity += -this.Velocity * 0.9f * dt;

                this.Velocity += _riseRate * dt;

                _ellipse.origin = this.Position;
            }

            public override void Render(RenderContext ctx)
            {
                base.Render(ctx);

                ctx.FillEllipse(Ellipse, Color);
                //ctx.FillRectangle(new D2DRect(_ellipse.origin, new D2DSize(_ellipse.radiusX, _ellipse.radiusY)), Color);
            }
        }
    }
}
