using PolyPlane.Helpers;
using PolyPlane.Rendering;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public class Bullet : GameObjectPoly, ICollidable
    {
        public const float SPEED = 800f;
        public float Lifetime = 10f;

        private static readonly D2DPoint[] _poly = new D2DPoint[]
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

            var velo = (Utilities.AngleToVectorDegrees(plane.Rotation, Bullet.SPEED));
            velo += plane.Velocity;
            this.Velocity = velo;

            // Make sure the rotation is aligned with the resulting velocity.
            this.Rotation = this.Velocity.Angle(true);
        }

        public Bullet(D2DPoint pos, D2DPoint velo, float rotation) : base(pos, velo, rotation)
        {
            this.Polygon = new RenderPoly(_poly);
            this.Polygon.Update(this.Position, this.Rotation, World.RenderScale * this.RenderOffset);
        }

        public override void Update(float dt, float renderScale)
        {
            base.Update(dt, renderScale);

            if (this.Age >= Lifetime)
                this.IsExpired = true;
        }

        public override void Render(RenderContext ctx)
        {
            base.Render(ctx);

            ctx.DrawPolygon(this.Polygon.Poly, D2DColor.Black, 1f, D2DDashStyle.Solid, D2DColor.Yellow);
        }
    }
}
