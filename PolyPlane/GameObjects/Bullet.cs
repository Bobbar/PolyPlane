using PolyPlane.GameObjects.Interfaces;
using PolyPlane.GameObjects.Tools;
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

        public Bullet() : base()
        {
            this.Polygon = new RenderPoly(_poly);
        }

        public void ReInit(FighterPlane plane)
        {
            this.IsExpired = false;
            this.Age = 0f;

            this.Position = plane.GunPosition;
            this.Rotation = plane.Rotation;
            this.Owner = plane;
            this.PlayerID = plane.PlayerID;

            var velo = (Utilities.AngleToVectorDegrees(plane.Rotation, Bullet.SPEED));
            this.Velocity = velo + Utilities.AngularVelocity(plane, plane.Gun.Position, World.SUB_DT);

            this.Rotation = this.Velocity.Angle();
            this.Polygon.Update(this.Position, this.Rotation, World.RenderScale * this.RenderOffset);
        }

        public void ReInitNet(D2DPoint pos, D2DPoint velo, float rotation)
        {
            this.IsExpired = false;
            this.Age = 0f;

            this.Position = pos;
            this.Velocity = velo;
            this.Rotation = rotation;

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
