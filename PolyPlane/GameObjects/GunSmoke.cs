﻿using PolyPlane.GameObjects.Fixtures;
using PolyPlane.GameObjects.Interfaces;
using PolyPlane.GameObjects.Tools;
using PolyPlane.Helpers;
using PolyPlane.Rendering;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public class GunSmoke : GameObject, INoGameID
    {
        private FixturePoint _refPos;
        private List<SmokePart> _parts = new List<SmokePart>();
        private GameTimer _spawnTimer = new GameTimer(0.05f, true);
        private float _radius = 5f;
        private D2DColor _smokeColor = new D2DColor(0.3f, D2DColor.White);
        private const int MAX_PARTS = 20;
        private const float MAX_AGE = 1f;

        public GunSmoke(GameObject obj, D2DPoint offset, float radius, D2DColor color)
        {
            _smokeColor = color;
            _spawnTimer.Interval = MAX_AGE / MAX_PARTS;

            this.Owner = obj;
            _refPos = new FixturePoint(obj, offset);
            _radius = radius;

            _spawnTimer.TriggerCallback = () => SpawnPart();
            _spawnTimer.Start();
        }

        public override void Update(float dt)
        {
            base.Update(dt);
            _spawnTimer.Update(dt);
            UpdateParts(dt);

            if (_refPos != null)
            {
                _refPos.Update(dt);
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
                part.Render(ctx);
        }

        public void AddPuff()
        {
            if (_parts.Count == 0)
                return;

            _parts.Last().Radius = _radius + 7f;
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

            var newRad = _radius + Utilities.Rnd.NextFloat(-2f, 2f);

            //if (this.CurrentFrame % 10 == 0)
            //    newRad += 10f;

            var newColor = _smokeColor;
            var newEllipse = new D2DEllipse(newPos, new D2DSize(newRad, newRad));
            var newPart = new SmokePart(newEllipse, newColor, newVelo);

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
                var alpha = _smokeColor.a * ageFact;

                part.Color = new D2DColor(alpha, _smokeColor);

                if (part.Age > MAX_AGE)
                    _parts.RemoveAt(i);

                i++;
            }
        }


        private class SmokePart : GameObject, INoGameID
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

            public SmokePart(D2DEllipse ellipse, D2DColor color, D2DPoint velo) : base(ellipse.origin, velo)
            {
                _ellipse = ellipse;
                InitRadius = this.Radius;
                Color = color;
            }

            public override void Update(float dt)
            {
                base.Update(dt);

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
