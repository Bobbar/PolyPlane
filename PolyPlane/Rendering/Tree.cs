using PolyPlane.Helpers;
using unvell.D2DLib;

namespace PolyPlane.Rendering
{
    public abstract class Tree
    {
        public D2DPoint Position;
        public float Height;
        public float TotalHeight;
        public D2DColor TrunkColor;
        public D2DColor LeafColor;

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
            var shadowColor = Utilities.LerpColorWithAlpha(timeOfDayColor, D2DColor.Black, 0.7f, 0.4f);
            return shadowColor;
        }

        protected float GetShadowAngle()
        {
            var shadowAngle = Utilities.Lerp(-40f, 40f, Utilities.Factor(World.TimeOfDay, World.MAX_TIMEOFDAY));
            return shadowAngle;
        }
    }

    public class NormalTree : Tree
    {
        public float Radius;
        public float TrunkWidth;
        public readonly D2DPoint[] TrunkPoly;
        private D2DPoint[] _trunkTransPoly;

        private D2DRadialGradientBrush _leafBrush = null;

        public NormalTree(D2DPoint pos, float height, float radius, float trunkWidth, D2DColor trunkColor, D2DColor leafColor) : base(pos, height, trunkColor, leafColor)
        {
            Radius = radius;
            TrunkWidth = trunkWidth;
            TotalHeight = height + radius;

            TrunkPoly =
            [
                new D2DPoint(-TrunkWidth, 0),
                new D2DPoint(TrunkWidth, 0),
                new D2DPoint(TrunkWidth * 0.25f, this.Height),
                new D2DPoint(-TrunkWidth * 0.25f, this.Height),
            ];

            _trunkTransPoly = new D2DPoint[TrunkPoly.Length];
        }

        public override void Render(RenderContext ctx, D2DColor timeOfDayColor, float scale)
        {
            if (_leafBrush == null)
                _leafBrush = ctx.Device.CreateRadialGradientBrush(D2DPoint.Zero, D2DPoint.Zero, this.Radius, this.Radius, [new D2DGradientStop(0f, this.LeafColor), new D2DGradientStop(1f, Utilities.LerpColor(this.LeafColor, D2DColor.Black, 0.2f))]);

            // Add time of day color
            var trunkColor = Utilities.LerpColor(this.TrunkColor, timeOfDayColor, 0.3f);

            // Add time of day color to leafs.
            var leafToDColor = new D2DColor(0.2f, timeOfDayColor);
            var shadowColor = GetShadowColor(timeOfDayColor);
            var leafYPos = new D2DPoint(0f, (-this.Height * scale) - this.Radius);
            var shadowLeafPos = this.Position - leafYPos;
            var normalLeafPos = this.Position + leafYPos;
            var shadowAngle = GetShadowAngle();
            var size = new D2DSize(this.Radius, this.Radius);

            // Draw shadow.
            ctx.PushTransform();
            ctx.RotateTransform(shadowAngle, this.Position);

            Utilities.ApplyTranslation(TrunkPoly, _trunkTransPoly, D2DPoint.Zero, 0f, this.Position, scale, scale * 2f);

            // Adjust the bottom two points of the shadow to line up with the bottom of the trunk.
            _trunkTransPoly[0] = Utilities.ApplyTranslation(TrunkPoly[0], -shadowAngle, this.Position, scale);
            _trunkTransPoly[1] = Utilities.ApplyTranslation(TrunkPoly[1], -shadowAngle, this.Position, scale);

            ctx.Gfx.DrawPolygon(_trunkTransPoly, shadowColor, 0f, D2DDashStyle.Solid, shadowColor);

            ctx.ScaleTransform(1f, 2f, this.Position);

            ctx.Gfx.FillEllipse(new D2DEllipse(shadowLeafPos, size), shadowColor);

            ctx.PopTransform();

            // Apply lighting color.
            if (World.UseLightMap)
            {
                // Center of trunk pos.
                var trunkPos = this.Position + (-D2DPoint.UnitY * (TotalHeight * 1f));
                trunkColor = ctx.LightMap.SampleColor(trunkPos, trunkColor, 0f, 0.4f);
                leafToDColor = ctx.LightMap.SampleColor(normalLeafPos, leafToDColor, 0f, 0.4f);
            }

            // Draw tree.
            Utilities.ApplyTranslation(TrunkPoly, _trunkTransPoly, 180f, this.Position, scale);

            ctx.Gfx.DrawPolygon(_trunkTransPoly, trunkColor, 0f, D2DDashStyle.Solid, trunkColor);

            ctx.PushTransform();
            ctx.TranslateTransform(normalLeafPos * ctx.CurrentScale);

            var leafEllipse = new D2DEllipse(D2DPoint.Zero, size);
            ctx.Gfx.FillEllipse(leafEllipse, _leafBrush);
            ctx.Gfx.FillEllipse(leafEllipse, leafToDColor);

            ctx.PopTransform();
        }
    }

    public class PineTree : Tree
    {
        public float Width;

        public readonly D2DPoint[] TopPoly;
        private D2DPoint[] _topTrans;

        public readonly D2DPoint[] TrunkPoly;
        private D2DPoint[] _trunkTransPoly;

        public PineTree(D2DPoint pos, float height, float width, D2DColor trunkColor, D2DColor leafColor) : base(pos, height, trunkColor, leafColor)
        {
            Width = width;
            TotalHeight = height;

            TopPoly =
            [
                new D2DPoint(-(this.Width / 2f), 0),
                new D2DPoint((this.Width / 2f), 0),
                new D2DPoint(0, this.Height),
            ];

            _topTrans = new D2DPoint[TopPoly.Length];


            var trunkRect = new D2DRect(D2DPoint.Zero, new D2DSize(this.Width / 2f, this.Height));

            TrunkPoly =
            [
                new D2DPoint(trunkRect.right, trunkRect.top + (trunkRect.Height * 0.5f)),
                new D2DPoint(trunkRect.left, trunkRect.top + (trunkRect.Height * 0.5f)),
                new D2DPoint(trunkRect.left, trunkRect.bottom + (trunkRect.Height * 0.5f)),
                new D2DPoint(trunkRect.right, trunkRect.bottom + (trunkRect.Height * 0.5f)),
            ];

            _trunkTransPoly = new D2DPoint[TrunkPoly.Length];
        }

        public override void Render(RenderContext ctx, D2DColor timeOfDayColor, float scale)
        {
            // Add time of day color
            var trunkColor = Utilities.LerpColor(this.TrunkColor, timeOfDayColor, 0.3f);
            var leafColor = Utilities.LerpColor(this.LeafColor, timeOfDayColor, 0.3f);

            var shadowTopPos = this.Position + new D2DPoint(0, this.Height);
            var shadowColor = GetShadowColor(timeOfDayColor);
            var shadowAngle = GetShadowAngle();


            // Draw shadow.
            ctx.PushTransform();
            ctx.RotateTransform(shadowAngle, this.Position);

            Utilities.ApplyTranslation(TrunkPoly, _trunkTransPoly, D2DPoint.Zero, 0f, this.Position, 1f, 2f);

            //Adjust the bottom two points of the shadow to line up with the bottom of the trunk.
            _trunkTransPoly[0] = Utilities.ApplyTranslation(TrunkPoly[0], -shadowAngle, this.Position, 1f);
            _trunkTransPoly[1] = Utilities.ApplyTranslation(TrunkPoly[1], -shadowAngle, this.Position, 1f);

            ctx.DrawPolygon(_trunkTransPoly, shadowColor, 0f, D2DDashStyle.Solid, shadowColor);

            ctx.ScaleTransform(1f, 2f, this.Position);
            Utilities.ApplyTranslation(TopPoly, _topTrans, 0f, shadowTopPos, scale);

            ctx.DrawPolygon(_topTrans, shadowColor, 0f, D2DDashStyle.Solid, shadowColor);

            ctx.PopTransform();

            // Apply lighting color.
            if (World.UseLightMap)
            {
                // Center position.
                var pos = this.Position + (-D2DPoint.UnitY * (TotalHeight * 2f));
                trunkColor = ctx.LightMap.SampleColor(pos, trunkColor, 0f, 0.4f);
                leafColor = ctx.LightMap.SampleColor(pos, leafColor, 0f, 0.4f);
            }

            // Draw tree.
            Utilities.ApplyTranslation(TopPoly, _topTrans, 180f, this.Position - new D2DPoint(0, this.Height), scale);
            Utilities.ApplyTranslation(TrunkPoly, _trunkTransPoly, 180f, this.Position, 1f);

            ctx.DrawPolygon(_trunkTransPoly, trunkColor, 0f, D2DDashStyle.Solid, trunkColor);
            ctx.DrawPolygon(_topTrans, leafColor, 0f, D2DDashStyle.Solid, leafColor);
        }
    }
}