using PolyPlane.GameObjects.Interfaces;
using PolyPlane.GameObjects.Tools;
using PolyPlane.Helpers;
using PolyPlane.Rendering;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public class Debris : GameObjectPoly, ICollidable, INoGameID
    {
        private D2DColor _color;
        private FlameEmitter _flame;
        private float _onGroundAge = 0f;
        private const float MAX_AGE = 70f;

        public Debris(GameObject owner, D2DPoint pos, D2DPoint velo, D2DColor color) : base(pos, velo)
        {
            this.Mass = 40f;
            this.RenderOrder = 3;
            this.Owner = owner;
            _color = color;
            this.Polygon = new RenderPoly(this, RandomPoly(8, 12));

            this.RotationSpeed = Utilities.Rnd.NextFloat(-200f, 200f);

            this.Velocity = velo * 0.7f;
            this.Velocity += Utilities.RandOPoint(100f);

            _flame = new FlameEmitter(this, D2DPoint.Zero, 3f);
            _flame.Owner = this.Owner;
        }

        public override void Update(float dt)
        {
            base.Update(dt);
            _flame.Update(dt);

            if (this.IsAwake)
                this.Velocity += (World.Gravity * 1f) * dt;

            this.Velocity += -this.Velocity * (dt * 0.01f);

            if (this.Altitude <= 1f)
            {
                _flame.StopSpawning();
                _onGroundAge += dt;
            }

            if (_onGroundAge > MAX_AGE)
                this.IsExpired = true;
        }

        public override void Render(RenderContext ctx)
        {
            base.Render(ctx);

            var ageAlpha = 1f - Utilities.FactorWithEasing(this.Age, MAX_AGE, EasingFunctions.EaseInExpo);
            ctx.DrawPolygon(this.Polygon, D2DColor.Black.WithAlpha(ageAlpha), 0.5f, D2DDashStyle.Solid, _color.WithAlpha(ageAlpha));
        }

        public override void Dispose()
        {
            base.Dispose();

            _flame.Dispose();
        }
    }
}
