using PolyPlane.Rendering;
using PolyPlane.Helpers;
using unvell.D2DLib;

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

        public Decoy(FighterPlane owner) : base()
        {
            this.PlayerID = owner.PlayerID;
            this.Owner = owner;

            this.Position = owner.Position;
            this.Velocity = owner.Velocity;

            // Make the decoy shoot out from the top of the plane.
            const float EJECT_FORCE = 100f;
            var toRight = owner.FlipDirection == Direction.Right;
            var rotVec = Utilities.AngleToVectorDegrees(owner.Rotation + (toRight ? 0f : 180f));
            var topVec = new D2DPoint(rotVec.Y, -rotVec.X);
            this.Velocity += topVec * EJECT_FORCE;
        }

        public override void Update(float dt, D2DSize viewport, float renderScale)
        {
            base.Update(dt, viewport, renderScale);

            this.Velocity *= 0.998f;

            this.Velocity += ((World.Gravity * 2f) * dt);

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
    }
}
