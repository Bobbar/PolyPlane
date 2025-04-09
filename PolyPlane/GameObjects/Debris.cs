using PolyPlane.GameObjects.Interfaces;
using PolyPlane.GameObjects.Particles;
using PolyPlane.GameObjects.Tools;
using PolyPlane.Helpers;
using PolyPlane.Rendering;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public sealed class Debris : GameObject, IPolygon, INoGameID
    {
        private D2DColor _color;
        private FlameEmitter _flame;
        private float _onGroundAge = 0f;
        private const float MAX_AGE = 70f;

        public RenderPoly Polygon { get; set; }

        public Debris() : base()
        {
            this.Flags = GameObjectFlags.Pushable | GameObjectFlags.SpatialGrid | GameObjectFlags.BounceOffGround | GameObjectFlags.CanSleep;
            this.Mass = 40f;
            this.RenderOrder = 3;
            this.Polygon = new RenderPoly(this, Utilities.RandomPoly(8, 12));

            _flame = new FlameEmitter(this, D2DPoint.Zero, 2f, 4f, false);
        }

        public void ReInit(GameObject owner, D2DPoint pos, D2DPoint velo, D2DColor color)
        {
            this.IsExpired = false;
            this.IsAwake = true;
            this.Age = 0f;
            this.Position = pos;

            _onGroundAge = 0f;

            this.Owner = owner;
            _color = color;

            this.RotationSpeed = Utilities.Rnd.NextFloat(-200f, 200f);

            this.Velocity = velo * 0.7f;
            this.Velocity += Utilities.RandOPoint(100f);

            _flame.IsExpired = false;
            _flame.StartSpawning();
        }

        public override void DoUpdate(float dt)
        {
            base.DoUpdate(dt);

            _flame.Update(dt);

            if (this.IsAwake)
                this.Velocity += (World.Gravity * 1f) * dt;

            this.Velocity += -this.Velocity * (dt * 0.01f);

            if (this.Altitude <= 10f)
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

            var ageAlpha = 1f - Utilities.FactorWithEasing(_onGroundAge, MAX_AGE, EasingFunctions.In.EaseExpo);
            var color = _color.WithAlpha(ageAlpha);

            ctx.DrawPolygonWithLighting(this.Polygon, this.Position, D2DColor.Black.WithAlpha(ageAlpha), 0.3f, color, maxIntensity: 0.5f);
        }

        public override void Dispose()
        {
            base.Dispose();

            World.ObjectManager.ReturnDebris(this);
        }
    }
}
