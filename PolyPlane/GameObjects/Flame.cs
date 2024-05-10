using PolyPlane.Helpers;
using PolyPlane.Rendering;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public class Flame : GameObject
    {
        public float Radius { get; set; }
        public D2DSize HoleSize { get; set; }

        private const int MAX_PARTS = 50;
        private const float MAX_AGE = 20f;
        private const float MIN_HOLE_SZ = 2f;
        private const float MAX_HOLE_SZ = 6f;

        private List<FlamePart> _parts = new List<FlamePart>();
        private D2DColor _flameColor = new D2DColor(0.6f, D2DColor.Yellow);
        private D2DColor _blackSmoke = new D2DColor(0.6f, D2DColor.Black);
        private D2DColor _graySmoke = new D2DColor(0.6f, D2DColor.Gray);
        private float _rotOffset = 0f;
        private FixturePoint _refPos = null;
        private GameTimer _spawnTimer = new GameTimer(0.1f, true);

        public Flame(GameObject obj, D2DPoint offset, float radius = 10f) : base(obj.Position, obj.Velocity)
        {
            _spawnTimer.Interval = MAX_AGE / MAX_PARTS;

            this.Owner = obj;
            Radius = radius;
            _refPos = new FixturePoint(obj, offset);

            _spawnTimer.TriggerCallback = () => SpawnPart();
            _spawnTimer.Start();

            HoleSize = new D2DSize(Utilities.Rnd.NextFloat(MIN_HOLE_SZ, MAX_HOLE_SZ), Utilities.Rnd.NextFloat(MIN_HOLE_SZ, MAX_HOLE_SZ));
            _rotOffset = Utilities.Rnd.NextFloat(0f, 360f);
        }

        public Flame(GameObject obj, D2DPoint offset, bool hasFlame = true) : base(obj.Position, obj.Velocity)
        {
            _spawnTimer.Interval = MAX_AGE / MAX_PARTS;

            this.Owner = obj;
            Radius = Utilities.Rnd.NextFloat(4f, 15f);
            _refPos = new FixturePoint(obj, offset);

            if (hasFlame)
            {
                _spawnTimer.TriggerCallback = () => SpawnPart();
                _spawnTimer.Start();
            }

            HoleSize = new D2DSize(Utilities.Rnd.NextFloat(MIN_HOLE_SZ, MAX_HOLE_SZ), Utilities.Rnd.NextFloat(MIN_HOLE_SZ, MAX_HOLE_SZ));
            _rotOffset = Utilities.Rnd.NextFloat(0f, 360f);
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
                this.Rotation = _refPos.Rotation + _rotOffset;
            }

            if (this.Owner != null && this.Owner.IsExpired)
                _spawnTimer.Stop();

            if (_parts.Count == 0 && !_spawnTimer.IsRunning)
                this.IsExpired = true;

        }

        public override void Render(RenderContext ctx)
        {
            //ctx.Gfx.FillEllipseSimple(this.Position, 3f, D2DColor.Red);

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

            if (_refPos.GameObject is FighterPlane plane && !plane.IsDamaged)
                endColor = _graySmoke;

            var newRad = this.Radius + Utilities.Rnd.NextFloat(-3f, 3f);
            var newColor = new D2DColor(_flameColor.a, 1f, Utilities.Rnd.NextFloat(0f, 0.86f), _flameColor.b);
            var newEllipse = new D2DEllipse(newPos, new D2DSize(newRad, newRad));
            var newPart = new FlamePart(newEllipse, newColor, endColor, newVelo);
            //newPart.IsNetObject = this.IsNetObject;
            newPart.SkipFrames = this.IsNetObject ? 1 : World.PHYSICS_SUB_STEPS;

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
                part.Update(dt, renderScale, skipFrames: false);

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

            public float Age { get; set; }

            private D2DEllipse _ellipse;

            private D2DPoint _riseRate = new D2DPoint(0f, -50f);


            public FlamePart(D2DEllipse ellipse, D2DColor color, D2DPoint velo) : base(ellipse.origin, velo)
            {
                _ellipse = ellipse;
                Color = color;
            }

            public FlamePart(D2DEllipse ellipse, D2DColor color, D2DColor endColor, D2DPoint velo) : base(ellipse.origin, velo)
            {
                _ellipse = ellipse;
                Color = color;
                EndColor = endColor;
            }

            public override void Update(float dt, float renderScale)
            {
                base.Update(dt, renderScale);

                this.Velocity += -this.Velocity * 0.9f * dt;

                this.Velocity += _riseRate * dt;

                _ellipse.origin = this.Position;

                this.Age += dt;
            }

            public override void Render(RenderContext ctx)
            {
                ctx.FillEllipse(Ellipse, Color);
                //ctx.FillRectangle(new D2DRect(_ellipse.origin, new D2DSize(_ellipse.radiusX, _ellipse.radiusY)), Color);
            }
        }
    }
}
