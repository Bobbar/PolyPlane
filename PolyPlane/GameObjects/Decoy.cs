using PolyPlane.GameObjects.Interfaces;
using PolyPlane.Helpers;
using PolyPlane.Rendering;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public class Decoy : GameObject, ICollidable
    {
        private float _radius = 5f;
        private float _flashAmt = 5f;
        private float _currentFlash = 0f;
        private float _flashRate = 20f;
        private float _lifeSpan = 10f;
        private float _direction = 1f;

        public Decoy(FighterPlane owner, D2DPoint pos) : base(pos)
        {
            this.PlayerID = owner.PlayerID;
            this.Owner = owner;
            this.Velocity = owner.Velocity;
            this.RenderOrder = 1;

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
            this.RenderOrder = 1;
        }

        public override void Update(float dt, float renderScale)
        {
            base.Update(dt, renderScale);

            this.Velocity += -this.Velocity * (dt * 0.6f);
            this.Velocity += ((World.Gravity * 2f) * dt);

            _currentFlash += _direction * (_flashRate * dt);

            if (_currentFlash >= _flashAmt || _currentFlash <= 0f)
                _direction *= -1f;

            if (this.Age > _lifeSpan)
                this.IsExpired = true;
        }

        public override void Render(RenderContext ctx)
        {
            base.Render(ctx);

            ctx.FillEllipse(new D2DEllipse(this.Position, _radius + _currentFlash, _radius + _currentFlash), D2DColor.Yellow);
        }
    }
}
