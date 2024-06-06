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
        private bool _veloSizing = true;
        private const int MAX_PARTS = 20;
        private const float MAX_AGE = 1f;

        public Vapor(GameObject obj, GameObject owner, D2DPoint offset, float radius, bool veloSizing = true)
        {
            _spawnTimer.Interval = MAX_AGE / MAX_PARTS;
            _veloSizing = veloSizing;

            this.Owner = owner;
            _refPos = new FixturePoint(obj, offset);
            _radius = radius;

            _spawnTimer.TriggerCallback = () => SpawnPart();
            _spawnTimer.Start();
        }

        public Vapor(GameObject obj, D2DPoint offset, float radius, D2DColor color)
        {
            _vaporColor = color;
            _spawnTimer.Interval = MAX_AGE / MAX_PARTS;
            _veloSizing = false;

            this.Owner = obj;
            _refPos = new FixturePoint(obj, offset);
            _radius = radius;

            _spawnTimer.TriggerCallback = () => SpawnPart();
            _spawnTimer.Start();
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
            }

            if (this.Owner != null && this.Owner.IsExpired)
                _spawnTimer.Stop();

            if (_parts.Count == 0 && !_spawnTimer.IsRunning)
                this.IsExpired = true;
        }

        public override void Render(RenderContext ctx)
        {
            base.Render(ctx);

            foreach (var part in _parts)
            {
                if (_veloSizing)
                {
                    // Let the parts move away from the source slightly before rendering.
                    if (part.Age > 0.2f)
                        part.Render(ctx);
                }
                else
                    part.Render(ctx);
            }
        }

        private void SpawnPart()
        {
            if (!this.Visible)
                return;

            D2DPoint newPos = this.Position;

            if (_refPos != null)
                newPos = _refPos.Position;

            D2DPoint newVelo = this.Velocity;
            float gforce = 0f;

            if (this.Owner != null)
            {
                newVelo = this.Owner.Velocity;

                if (this.Owner is FighterPlane plane)
                    gforce = plane.GForce;
            }

            float newRad = 0f;

            if (_veloSizing)
            {
                //var veloFact = Utilities.Factor(newVelo.Length(), 1000f);
                var veloFact = Utilities.Factor(gforce, 20f);

                newRad = _radius + Utilities.Rnd.NextFloat(-2f, 2f) + (veloFact * 14f);
            }
            else
            {
                newRad = _radius + Utilities.Rnd.NextFloat(-2f, 2f);
            }

            var newColor = _vaporColor;
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
                var alpha = _vaporColor.a * ageFact;
                var rad = (part.InitRadius * ageFact) + 1f;

                part.Radius = rad;
                part.Color = new D2DColor(alpha, _vaporColor);

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

                this.Velocity += -this.Velocity * (dt * 3f);

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
