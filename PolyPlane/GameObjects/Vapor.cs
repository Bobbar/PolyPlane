using PolyPlane.GameObjects.Fixtures;
using PolyPlane.GameObjects.Interfaces;
using PolyPlane.GameObjects.Tools;
using PolyPlane.Helpers;
using PolyPlane.Rendering;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public class Vapor : GameObject, INoGameID
    {
        private FixturePoint _refPos;
        private List<VaporPart> _parts = new List<VaporPart>();
        private GameTimer _spawnTimer = new GameTimer(0.05f, true);
        private float _radius = 5f;
        private D2DColor _vaporColor = new D2DColor(0.3f, D2DColor.White);
        private const int MAX_PARTS = 30;
        private const float MAX_AGE = 1f;
        private float _visibleGs = 0f;
        private float _visibleVelo = 0f;
        private float _maxGs = 0f;
        private SmoothPoint _veloSmooth = new SmoothPoint(10);

        public Vapor(GameObject obj, GameObject owner, D2DPoint offset, float radius, float visibleGs, float visibleVelo, float maxGs)
        {
            _spawnTimer.Interval = MAX_AGE / MAX_PARTS;

            this.Owner = owner;
            _refPos = new FixturePoint(obj, offset);
            _radius = radius;
            _visibleGs = visibleGs;
            _visibleVelo = visibleVelo;
            _maxGs = maxGs;

            _spawnTimer.TriggerCallback = () => SpawnPart();
            _spawnTimer.Start();
        }

        public override void Update(float dt)
        {
            base.Update(dt);
            _spawnTimer.Update(dt);
            UpdateParts(dt);

            _refPos.Update(dt);
            this.Position = _refPos.Position;
            this.Velocity = _refPos.Velocity;

            if (this.Owner != null && this.Owner.IsExpired)
                _spawnTimer.Stop();

            if (this.Owner is FighterPlane plane)
            {
                if (plane.IsDisabled)
                    _spawnTimer.Stop();
                else
                    _spawnTimer.Start();
            }

            if (_parts.Count == 0 && !_spawnTimer.IsRunning)
                this.IsExpired = true;
        }

        public override void Render(RenderContext ctx)
        {
            base.Render(ctx);

            foreach (var part in _parts)
            {
                // Let the parts move away from the source slightly before rendering.
                if (part.Age > 0.1f)
                    part.Render(ctx);
            }
        }

        public override void FlipY()
        {
            base.FlipY();
            _refPos.FlipY();
        }

        private void SpawnPart()
        {
            _refPos.Update(0f);
            D2DPoint newPos = _refPos.GameObject.Position;
            D2DPoint newVelo = _veloSmooth.Add(this.Velocity);

            // Start the vapor parts one frame backwards.
            newPos -= newVelo.Normalized() * newVelo.Length() * World.DT;

            float gforce = 0f;
            var veloMag = newVelo.Length();

            if (this.Owner != null)
            {
                if (this.Owner is FighterPlane plane)
                    gforce = plane.GForce;
            }

            var sVisFact = Utilities.Factor(veloMag - _visibleVelo, _visibleVelo);
            var sRadFact = Utilities.Factor(veloMag, _visibleVelo);

            var gVisFact = Utilities.FactorWithEasing(gforce - (_visibleGs * 0.5f), _visibleGs, EasingFunctions.EaseInCirc);
            var gRadFact = Utilities.Factor(gforce, _maxGs);

            var radFact = sRadFact + gRadFact;
            var visFact = sVisFact + gVisFact;

            var newRad = _radius + Utilities.Rnd.NextFloat(-2f, 2f) + (radFact * 20f);

            var newColor = _vaporColor;
            newColor.a = newColor.a * visFact * Math.Clamp(World.SampleNoise(newPos), 0.3f, 1f);

            var newEllipse = new D2DEllipse(newPos, new D2DSize(newRad, newRad));
            var newPart = new VaporPart(newEllipse, newColor, newVelo);
            newPart.Owner = this.Owner;

            if (_parts.Count < MAX_PARTS)
                _parts.Add(newPart);
            else
            {
                _parts.RemoveAt(0);
                _parts.Add(newPart);
            }
        }

        private void UpdateParts(float dt)
        {
            int i = 0;
            while (i < _parts.Count)
            {
                var part = _parts[i];
                part.Update(dt);

                var ageFact = 1f - Utilities.Factor(part.Age, MAX_AGE);
                var radAmt = EasingFunctions.EaseOutSine(ageFact);
                var rad = (part.InitRadius * radAmt) + 0f;

                part.Radius = rad;

                if (part.Age > MAX_AGE)
                    _parts.RemoveAt(i);

                i++;
            }
        }


        private class VaporPart : GameObject, INoGameID
        {
            public D2DColor Color;

            public float InitRadius = 0f;

            public float Radius
            {
                get
                {
                    return _ellipse.radiusX;
                }

                set
                {
                    _ellipse.radiusX = value;
                    _ellipse.radiusY = value;
                }
            }

            private D2DEllipse _ellipse;

            public VaporPart(D2DEllipse ellipse, D2DColor color, D2DPoint velo) : base(ellipse.origin, velo)
            {
                _ellipse = ellipse;
                InitRadius = this.Radius;
                Color = color;
            }

            public override void Update(float dt)
            {
                base.Update(dt);

                this.Velocity += -this.Velocity * (dt * 2f);

                _ellipse.origin = this.Position;
            }

            public override void Render(RenderContext ctx)
            {
                base.Render(ctx);
                const float MIN_VELO = 800f;
                const float VELO_MOVE_AMT = 40f;

                // Move the ellipse backwards as velo increases.
                var veloFact = Utilities.Factor(this.Owner.Velocity.Length() - MIN_VELO, MIN_VELO);
                var ellip = new D2DEllipse(_ellipse.origin - Utilities.AngleToVectorDegrees(this.Owner.Rotation, VELO_MOVE_AMT * veloFact), new D2DSize(_ellipse.radiusX, _ellipse.radiusY));
                ctx.FillEllipse(ellip, Color);
            }
        }
    }
}
