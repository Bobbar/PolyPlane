using PolyPlane.Helpers;
using PolyPlane.Rendering;
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

            this.Position = owner.ExhaustPosition;
            this.Velocity = owner.Velocity;

            // Make the decoy shoot out from the top of the plane.
            const float EJECT_FORCE = 200f;
            var toRight = owner.FlipDirection == Direction.Right;
            var bottomOffset = 0f;

            // Alternate between top and bottom.
            if (owner.DecoysDropped % 2 == 0)
                bottomOffset = 180f;

            var rotVec = Utilities.AngleToVectorDegrees(owner.Rotation + bottomOffset + (toRight ? 0f : 180f));
            var topVec = new D2DPoint(rotVec.Y, -rotVec.X);
            this.Velocity += topVec * EJECT_FORCE;
        }

        public Decoy(FighterPlane owner, D2DPoint pos, D2DPoint velo) : base(pos, velo)
        {
            this.PlayerID = owner.PlayerID;
            this.Owner = owner;
        }

        public override void Update(float dt, float renderScale)
        {
            base.Update(dt, renderScale);

            this.Velocity *= 0.98f;

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
            base.Render(ctx);

            ctx.FillEllipse(new D2DEllipse(this.Position, _radius + _currentFlash, _radius + _currentFlash), D2DColor.Yellow);
        }
    }
}
