﻿using PolyPlane.Rendering;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public class Explosion : GameObjectPoly
    {
        public float MaxRadius { get; set; } = 100f;
        public float Duration { get; set; } = 1f;

        private float _currentRadius = 0f;
        private float _age = 0f;
        private D2DColor _color = new D2DColor(0.4f, D2DColor.Orange);

        public Explosion(D2DPoint pos, float maxRadius, float duration) : base(pos)
        {
            this.MaxRadius = maxRadius;
            this.Duration = duration;

            _color.r = _rnd.NextFloat(0.8f, 1f);
        }

        public override void Update(float dt, D2DSize viewport, float renderScale)
        {
            base.Update(dt, viewport, renderScale);

            _currentRadius = MaxRadius *  EasingFunctions.EaseOutBack(_age / Duration);

            _age += dt;

            if (_age >= Duration)
                this.IsExpired = true;
        }

        public override void Render(RenderContext ctx)
        {
            ctx.FillEllipse(new D2DEllipse(this.Position, new D2DSize(_currentRadius, _currentRadius)), _color);
        }

        public override bool Contains(D2DPoint pnt)
        {
            var dist = D2DPoint.Distance(pnt, this.Position);

            return dist < _currentRadius;
        }
    }
}
