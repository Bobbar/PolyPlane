using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public class Debris : GameObjectPoly
    {
        private D2DColor _color;
        private Flame _flame;

        public Debris(D2DPoint pos, D2DPoint velo, D2DColor color) : base(pos, velo) 
        {
            _color = color;
            this.Polygon = new RenderPoly(GameObjectPoly.RandomPoly(8, 12));

            this.RotationSpeed = Helpers.Rnd.NextFloat(-200f, 200f);

            this.Velocity = velo * 0.7f;
            this.Velocity += Helpers.RandOPoint(100f);

            _flame = new Flame(this, D2DPoint.Zero, 3f);
        }

        public override void Update(float dt, D2DSize viewport, float renderScale)
        {
            base.Update(dt, viewport, renderScale);
            _flame.Update(dt, viewport, renderScale, skipFrames: false);

            this.Velocity += (World.Gravity * 3f) * dt;

            this.Velocity *= new D2DPoint(0.999f, 1f);

            if (this.Altitude <= 0)
                this.IsExpired = true;
        }

        public override void Render(RenderContext ctx)
        {
            _flame.Render(ctx);
            ctx.DrawPolygon(this.Polygon.Poly, D2DColor.Black, 1f, D2DDashStyle.Solid, _color);
        }
    }
}
