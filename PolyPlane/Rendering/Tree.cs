using unvell.D2DLib;

namespace PolyPlane.Rendering
{
    public abstract class Tree
    {
        public D2DPoint Position;
        public float Height;
        public D2DColor TrunkColor;
        public D2DColor LeafColor;

        public Tree() { }

        public Tree(D2DPoint position, float height)
        {
            Position = position;
            Height = height;
        }

        public Tree(D2DPoint position, float height, D2DColor trunkColor, D2DColor leafColor)
        {
            Position = position;
            Height = height;
            TrunkColor = trunkColor;
            LeafColor = leafColor;
        }

        public abstract void Render(RenderContext ctx, D2DColor timeOfDayColor, float scale);

        protected D2DColor GetShadowColor(D2DColor timeOfDayColor)
        {
            var shadowColor = new D2DColor(0.4f, Helpers.LerpColor(timeOfDayColor, D2DColor.Black, 0.7f));
            return shadowColor;
        }
    }

    public class NormalTree : Tree
    {
        public float Radius;
        public float TrunkWidth;

        private D2DRadialGradientBrush _leafBrush = null;

        public NormalTree() { }

        public NormalTree(D2DPoint pos, float height, float radius) : base(pos, height)
        {
            Radius = radius;
        }

        public NormalTree(D2DPoint pos, float height, float radius, float trunkWidth, D2DColor trunkColor, D2DColor leafColor) : base(pos, height, trunkColor, leafColor)
        {
            Radius = radius;
            TrunkWidth = trunkWidth;
        }

        public override void Render(RenderContext ctx, D2DColor timeOfDayColor, float scale)
        {

            if (_leafBrush == null)
                _leafBrush = ctx.Device.CreateRadialGradientBrush(D2DPoint.Zero, D2DPoint.Zero, this.Radius, this.Radius, [new D2DGradientStop(0f, this.LeafColor), new D2DGradientStop(1f, Helpers.LerpColor(this.LeafColor, D2DColor.Black, 0.2f))]);

            var trunk = new D2DPoint[]
            {
                new D2DPoint(-TrunkWidth, 0),
                new D2DPoint(TrunkWidth, 0),
                new D2DPoint(TrunkWidth * 0.25f, this.Height),
                new D2DPoint(-TrunkWidth * 0.25f, this.Height),
            };

            var trunkTrans = new D2DPoint[trunk.Length];
            Array.Copy(trunk, trunkTrans, trunk.Length);

            // Add time of day color
            var trunkColor = Helpers.LerpColor(this.TrunkColor, timeOfDayColor, 0.3f);

            // Draw shadows.
            ctx.Gfx.PushTransform();

            var shadowColor = GetShadowColor(timeOfDayColor);
            var shadowLeaf = this.Position - new D2DPoint(0, (-this.Height * scale) - (this.Radius));
            var shadowAngle = Helpers.Lerp(-40f, 40f, Helpers.Factor(World.TimeOfDay, World.MAX_TIMEOFDAY));
            var shadowPosOffset = new D2DPoint(0f, -4f); // Small offset to shift the shadow polys "up" to hide the corners under the tree.

            ctx.Gfx.RotateTransform(shadowAngle, this.Position);
            ctx.Gfx.ScaleTransform(1f, 2f, this.Position);
            Helpers.ApplyTranslation(trunk, trunkTrans, 0f, this.Position + shadowPosOffset, scale);

            ctx.DrawPolygon(trunkTrans, shadowColor, 1f, D2DDashStyle.Solid, shadowColor);
            ctx.FillEllipse(new D2DEllipse(shadowLeaf + shadowPosOffset, new D2DSize(this.Radius, this.Radius)), shadowColor);
            ctx.Gfx.PopTransform();

            // Draw tree.
            Helpers.ApplyTranslation(trunk, trunkTrans, 180f, this.Position, scale);

            var leafPos = this.Position + new D2DPoint(0, (-this.Height * scale) - this.Radius);
            ctx.DrawPolygon(trunkTrans, trunkColor, 1f, D2DDashStyle.Solid, trunkColor);

            ctx.Gfx.PushTransform();
            ctx.Gfx.TranslateTransform(leafPos.X * ctx.CurrentScale, leafPos.Y * ctx.CurrentScale);

            ctx.Gfx.FillEllipse(new D2DEllipse(D2DPoint.Zero, new D2DSize(this.Radius, this.Radius)), _leafBrush);

            // Add time of day color to leafs.
            var todOverlay = new D2DColor(0.2f, timeOfDayColor);
            ctx.Gfx.FillEllipse(new D2DEllipse(D2DPoint.Zero, new D2DSize(this.Radius, this.Radius)), todOverlay);

            ctx.Gfx.PopTransform();
        }
    }

    public class PineTree : Tree
    {
        public float Width;

        public PineTree() { }

        public PineTree(D2DPoint pos, float height, float width) : base(pos, height)
        {
            Width = width;
        }

        public PineTree(D2DPoint pos, float height, float width, D2DColor trunkColor, D2DColor leafColor) : base(pos, height, trunkColor, leafColor)
        {
            Width = width;
        }

        public override void Render(RenderContext ctx, D2DColor timeOfDayColor, float scale)
        {
            var pineTop = new D2DPoint[]
         {
                new D2DPoint(-(this.Width / 2f), 0),
                new D2DPoint((this.Width / 2f), 0),
                new D2DPoint(0, this.Height),
         };

            var pineTopTrans = new D2DPoint[pineTop.Length];

            // Add time of day color
            var trunkColor = Helpers.LerpColor(this.TrunkColor, timeOfDayColor, 0.3f);
            var leafColor = Helpers.LerpColor(this.LeafColor, timeOfDayColor, 0.3f);
            var topPos = this.Position - new D2DPoint(0, this.Height / 2f);

            // Draw shadow.
            ctx.Gfx.PushTransform();

            var shadowTrunkPos = this.Position + new D2DPoint(0, this.Height / 2f);
            var shadowTopPos = this.Position + new D2DPoint(0, this.Height);
            var shadowColor = GetShadowColor(timeOfDayColor);
            var shadowAngle = Helpers.Lerp(-40f, 40f, Helpers.Factor(World.TimeOfDay, World.MAX_TIMEOFDAY));

            ctx.Gfx.RotateTransform(shadowAngle, this.Position);
            ctx.Gfx.ScaleTransform(1f, 2f, this.Position);
            Helpers.ApplyTranslation(pineTop, pineTopTrans, 0f, shadowTopPos, scale);

            ctx.FillRectangle(new D2DRect(shadowTrunkPos, new D2DSize(this.Width / 2f, this.Height * 1f)), shadowColor);
            ctx.DrawPolygon(pineTopTrans, shadowColor, 1f, D2DDashStyle.Solid, shadowColor);
            ctx.Gfx.PopTransform();

            // Draw tree.
            Helpers.ApplyTranslation(pineTop, pineTopTrans, 180f, this.Position - new D2DPoint(0, this.Height), scale);
            ctx.FillRectangle(new D2DRect(topPos, new D2DSize(this.Width / 2f, this.Height * 1f)), trunkColor);
            ctx.DrawPolygon(pineTopTrans, leafColor, 1f, D2DDashStyle.Solid, leafColor);
        }
    }
}