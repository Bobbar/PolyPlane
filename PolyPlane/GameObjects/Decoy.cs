﻿using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public class Decoy : GameObjectPoly
    {
        private float _radius = 5f;
        private float _flashAmt = 5f;
        private float _currentFlash = 0f;
        private float _flashRate = 20f;
        private float _lifeSpan = 10f;
        private float _age = 0f;
        private float _direction = 1f;

        public Decoy(GameObject owner) : base()
        {
            this.Owner = owner;

            this.Position = owner.Position;
            this.Velocity = owner.Velocity;
        }

        public override void Update(float dt, D2DSize viewport, float renderScale)
        {
            base.Update(dt, viewport, renderScale);

            this.Velocity *= 0.95f;

            _currentFlash += _direction * (_flashRate * dt);

            if (_currentFlash >= _flashAmt || _currentFlash <= 0f)
                _direction *= -1f;

            _age += dt;

            if (_age > _lifeSpan)
                this.IsExpired = true;
        }

        public override void Render(RenderContext ctx)
        {
            ctx.FillEllipse(new D2DEllipse(this.Position, _radius + _currentFlash, _radius + _currentFlash), D2DColor.Yellow);
        }

        //public override bool Contains(D2DPoint pnt)
        //{
        //    var dist = this.Position.DistanceTo(pnt);
        //    return dist <= _radius + _flashAmt;
        //}
    }
}
