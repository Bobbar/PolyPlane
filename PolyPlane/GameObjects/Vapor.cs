using PolyPlane.Rendering;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public class Vapor : GameObject
    {
        private FixturePoint _refPos;
        private List<VaporPart> _parts = new List<VaporPart>();
        private GameTimer _spawnTimer = new GameTimer(0.05f, true);
        private float _radius = 5f;
        private D2DColor _vaporColor = new D2DColor(0.3f, D2DColor.White);

        private const int MAX_PARTS = 20;
        private const float MAX_AGE = 1f;

        public Vapor(GameObject obj, D2DPoint offset, float radius)
        {
            _spawnTimer.Interval = MAX_AGE / MAX_PARTS;

            this.Owner = obj;
            _refPos = new FixturePoint(obj, offset);
            _radius = radius;

            _spawnTimer.TriggerCallback = () => SpawnPart();
            _spawnTimer.Start();
        }

        public override void Update(float dt, D2DSize viewport, float renderScale)
        {
            base.Update(dt, viewport, renderScale);
            _spawnTimer.Update(dt);
            UpdateParts(dt, viewport, renderScale);

            if (_refPos != null)
            {
                _refPos.Update(dt, viewport, renderScale);
                this.Position = _refPos.Position;
            }

            if (this.Owner != null && this.Owner.IsExpired)
                _spawnTimer.Stop();

            if (_parts.Count == 0 && !_spawnTimer.IsRunning)
                this.IsExpired = true;
        }

        public override void Render(RenderContext ctx)
        {
            _parts.ForEach(p => p.Render(ctx));
        }

        private void SpawnPart()
        {
            if (!this.Visible)
                return;

            D2DPoint newPos = this.Position;

            if (_refPos != null)
                newPos = _refPos.Position;

            D2DPoint newVelo = this.Velocity;

            if (this.Owner != null)
                newVelo = this.Owner.Velocity;


            var veloFact = Helpers.Factor(newVelo.Length(), 1000f);
            var newRad = _radius + Helpers.Rnd.NextFloat(-2f, 2f) + (veloFact * 14f);
            var newColor = _vaporColor;
            var newEllipse = new D2DEllipse(newPos, new D2DSize(newRad, newRad));
            var newPart = new VaporPart(newEllipse, newColor, newVelo);

            newPart.SkipFrames = this.IsNetObject ? 1 : World.PHYSICS_STEPS;

            if (_parts.Count < MAX_PARTS)
                _parts.Add(newPart);
            else
            {
                _parts.RemoveAt(0);
                _parts.Add(newPart);
            }
        }

        private void UpdateParts(float dt, D2DSize viewport, float renderScale)
        {
            int i = 0;
            while (i < _parts.Count)
            {
                var part = _parts[i];
                part.Update(dt, viewport, renderScale, skipFrames: false);

                var ageFactFade = 1f - Helpers.Factor(part.Age, MAX_AGE);
                var alpha = _vaporColor.a * ageFactFade;

                part.Color = new D2DColor(alpha, _vaporColor);

                if (part.Age > MAX_AGE)
                    _parts.RemoveAt(i);

                i++;
            }
        }


        private class VaporPart : GameObject
        {
            public D2DColor Color;
            public float Age;

            private D2DEllipse _ellipse;

            public VaporPart(D2DEllipse ellipse, D2DColor color, D2DPoint velo) : base(ellipse.origin, velo)
            {
                _ellipse = ellipse;
                Color = color;
            }

            public override void Update(float dt, D2DSize viewport, float renderScale)
            {
                base.Update(dt, viewport, renderScale);

                this.Velocity += -this.Velocity * 0.9999f * dt;

                _ellipse.origin = this.Position;

                Age += dt;
            }

            public override void Render(RenderContext ctx)
            {
                ctx.FillEllipse(_ellipse, Color);
            }
        }
    }
}
