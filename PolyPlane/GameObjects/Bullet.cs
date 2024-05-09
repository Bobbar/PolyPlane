using PolyPlane.Rendering;
using PolyPlane.Helpers;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public class Bullet : GameObjectPoly
    {
        public static float Speed = 800f;
        public float Lifetime = 10f;
        public Action<D2DPoint> AddExplosionCallback { get; set; }

        private float _age = 0;

        private D2DPoint[] _poly = new D2DPoint[]
        {
            new D2DPoint(7,0),
            new D2DPoint(4,-3),
            new D2DPoint(0,-4),
            new D2DPoint(-8,-4),
            new D2DPoint(-8,4),
            new D2DPoint(0,4),
            new D2DPoint(4,3),
        };

        public Bullet() : base() { }

        public Bullet(D2DPoint pos) : base(pos) { }

        public Bullet(FighterPlane plane) : base(plane.GunPosition, plane.Velocity, plane.Rotation)
        {
            this.Owner = plane;
            this.PlayerID = plane.PlayerID;

            this.Polygon = new RenderPoly(_poly);
            this.Polygon.Update(this.Position, this.Rotation, World.RenderScale * this.RenderOffset);

            var velo = (Utilities.AngleToVectorDegrees(plane.Rotation, Bullet.Speed));
            velo += plane.Velocity;
            this.Velocity = velo;
        }

        public Bullet(D2DPoint pos, D2DPoint velo, float rotation) : base(pos, velo, rotation)
        {
            this.Polygon = new RenderPoly(_poly);
            this.Polygon.Update(this.Position, this.Rotation, World.RenderScale * this.RenderOffset);
        }

        public override void Update(float dt, D2DSize viewport, float renderScale)
        {
            base.Update(dt, viewport, renderScale);

            _age += dt;

            if (_age >= Lifetime)
                this.IsExpired = true;
        }

        public override void Render(RenderContext ctx)
        {
            ctx.DrawPolygon(this.Polygon.Poly, D2DColor.Black, 1f, D2DDashStyle.Solid, D2DColor.Yellow);
        }
    }
}
