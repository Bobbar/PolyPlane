using PolyPlane.GameObjects.Animations;
using PolyPlane.GameObjects.Interfaces;
using PolyPlane.Helpers;
using PolyPlane.Rendering;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public class Decoy : GameObject, ILightMapContributor
    {
        private const float Radius = 5f;
        private const float LifeSpan = 10f;

        private float _currentFlashRadius = 0f;
        private int _currentFrame;

        private FloatAnimation _flashAnimation;

        public Decoy() : base() 
        {
            InitStuff();
        }

        private void InitStuff()
        {
            this.Flags = GameObjectFlags.SpatialGrid | GameObjectFlags.BounceOffGround;
            this.Mass = 50f;
            this.RenderOrder = 1;

            _flashAnimation = new FloatAnimation(0f, 5f, 0.4f, EasingFunctions.EaseLinear, v => _currentFlashRadius = v);
            _flashAnimation.Start();
            _flashAnimation.ReverseOnLoop = true;
            _flashAnimation.Loop = true;
        }

        public void ReInit(FighterPlane owner, D2DPoint pos)
        {
            this.IsExpired = false;
            this.Age = 0f;
            this.IsNetObject = false;

            this.Position = pos;
            this.PlayerID = owner.PlayerID;
            this.Owner = owner;
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

            _flashAnimation.Start();
        }

        public void ReInit(FighterPlane owner, D2DPoint pos, D2DPoint velo)
        {
            this.IsExpired = false;
            this.Age = 0f;

            this.PlayerID = owner.PlayerID;
            this.Owner = owner;
            this.Position = pos;
            this.Velocity = velo;

            _flashAnimation.Start();
        }

        public override void DoUpdate(float dt)
        {
            base.DoUpdate(dt);

            _flashAnimation.Update(dt);

            this.Velocity += -this.Velocity * (dt * 0.6f);
            this.Velocity += ((World.Gravity * 2f) * dt);

            if (this.Age > LifeSpan)
                this.IsExpired = true;

            _currentFrame++;
        }

        public override void Render(RenderContext ctx)
        {
            base.Render(ctx);

            ctx.FillEllipse(new D2DEllipse(this.Position, Radius + _currentFlashRadius, Radius + _currentFlashRadius), D2DColor.Yellow);
        }

        public bool IsFlashing()
        {
            const int FLASH_FRAME1 = 21;
            const int FLASH_FRAME2 = 33;

            var isFlashFrame = _currentFrame % FLASH_FRAME1 == 0 || _currentFrame % FLASH_FRAME2 == 0;

            return isFlashFrame;
        }

        public override void Dispose()
        {
            base.Dispose();

            _flashAnimation.Stop();

            World.ObjectManager.ReturnDecoy(this);
        }

        float ILightMapContributor.GetLightRadius()
        {
            const float LIGHT_RADIUS = 350f;

            return LIGHT_RADIUS;
        }

        float ILightMapContributor.GetIntensityFactor()
        {
            return 1f;
        }

        bool ILightMapContributor.IsLightEnabled()
        {
            return IsFlashing();
        }

        D2DPoint ILightMapContributor.GetLightPosition()
        {
            return this.Position;
        }

        D2DColor ILightMapContributor.GetLightColor()
        {
            return D2DColor.White;
        }
    }
}
