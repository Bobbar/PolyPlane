﻿using PolyPlane.Helpers;
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

        public override void Update(float dt, float renderScale)
        {
            base.Update(dt, renderScale);
            _spawnTimer.Update(dt);
            UpdateParts(dt, renderScale);

            _refPos.Update(dt, renderScale);
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
                if (part.Age > 0.2f)
                    part.Render(ctx);
            }
        }

        private void SpawnPart()
        {
            D2DPoint newPos = _refPos.GameObject.Position;
            D2DPoint newVelo = _veloSmooth.Add(this.Velocity);

            float gforce = 0f;
            var veloMag = newVelo.Length();

            if (this.Owner != null)
            {
                if (this.Owner is FighterPlane plane)
                    gforce = plane.GForce;
            }

            // Start the vapor parts one frame backwards.
            newPos -= Utilities.AngleToVectorDegrees(newVelo.Angle(), newVelo.Length() * World.DT);

            var sVisFact = Utilities.Factor(veloMag - _visibleVelo, _visibleVelo);
            var sRadFact = Utilities.Factor(veloMag, _visibleVelo);

            var gVisFact = Utilities.FactorWithEasing(gforce - (_visibleGs * 0.5f), _visibleGs, EasingFunctions.EaseInCirc);
            var gRadFact = Utilities.Factor(gforce, _maxGs);

            var radFact = sRadFact + gRadFact;
            var visFact = sVisFact + gVisFact;

            var newRad = _radius + Utilities.Rnd.NextFloat(-2f, 2f) + (radFact * 20f);

            var newColor = _vaporColor;
            newColor.a = newColor.a * visFact;

            var newEllipse = new D2DEllipse(newPos, new D2DSize(newRad, newRad));
            var newPart = new VaporPart(newEllipse, newColor, newVelo);

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

                var ageFact = 1f - Utilities.Factor(part.Age, MAX_AGE);
                var radAmt = EasingFunctions.EaseOutSine(ageFact);
                var rad = (part.InitRadius * radAmt) + 0f;

                part.Radius = rad;

                if (part.Age > MAX_AGE)
                    _parts.RemoveAt(i);

                i++;
            }
        }


        private class VaporPart : GameObject
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

            public override void Update(float dt, float renderScale)
            {
                base.Update(dt, renderScale);

                this.Velocity += -this.Velocity * (dt * 2f);

                _ellipse.origin = this.Position;
            }

            public override void Render(RenderContext ctx)
            {
                base.Render(ctx);

                ctx.FillEllipse(_ellipse, Color);
            }
        }
    }
}
