using PolyPlane.Helpers;
using unvell.D2DLib;

namespace PolyPlane.Rendering
{
    public abstract class Tree : IDisposable
    {
        public D2DPoint Position;
        public float Height;
        public float TotalHeight;
        public D2DColor TrunkColor;
        public D2DColor LeafColor;
        protected const float LIGHT_INTENSITY = 0.5f;
        protected const float SHADOW_LEN_SCALE = 2f;
        public const float TREE_SCALE = 4f;

        public Tree(D2DPoint position, float height, D2DColor trunkColor, D2DColor leafColor)
        {
            Position = position;
            Height = height;
            TrunkColor = trunkColor;
            LeafColor = leafColor;
        }

        public abstract void Render(RenderContext ctx, D2DColor timeOfDayColor, D2DColor shadowColor, float shadowAngle);

        public static float GetTreeShadowAngle()
        {
            var shadowAngle = Utilities.Lerp(-40f, 40f, Utilities.Factor(World.TimeOfDay, World.MAX_TIMEOFDAY));
            return shadowAngle;
        }

        public virtual void Dispose() { }

    }

    public class NormalTree : Tree
    {
        public float Radius;
        public float TrunkWidth;
        public readonly D2DPoint[] TrunkPoly;
        private D2DPoint[] _trunkTransPolyShadow;
        private D2DPoint[] _trunkTransPoly;
        private D2DPoint _leafYPos;
        private D2DSize _leafSize;
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
            _trunkTransPolyShadow = new D2DPoint[TrunkPoly.Length];

            TrunkPoly.Translate(_trunkTransPoly, 180f, this.Position, TREE_SCALE);

            _leafYPos = new D2DPoint(0f, (-this.Height * TREE_SCALE) - this.Radius);
            _leafSize = new D2DSize(Radius, Radius);
        }

        public override void Render(RenderContext ctx, D2DColor timeOfDayColor, D2DColor shadowColor, float shadowAngle)
        {
            if (_leafBrush == null)
                _leafBrush = ctx.Device.CreateRadialGradientBrush(D2DPoint.Zero, D2DPoint.Zero, this.Radius, this.Radius, [new D2DGradientStop(0f, this.LeafColor), new D2DGradientStop(1f, Utilities.LerpColor(this.LeafColor, D2DColor.Black, 0.2f))]);

            // Add time of day color
            var trunkColor = Utilities.LerpColor(this.TrunkColor, timeOfDayColor, 0.3f);
            var leafToDColor = new D2DColor(0.2f, timeOfDayColor);
            var shadowLeafPos = this.Position - _leafYPos;
            var normalLeafPos = this.Position + _leafYPos;

            // Draw shadows.

            // Rotate trunk shadow.
            TrunkPoly.Translate(_trunkTransPolyShadow, D2DPoint.Zero, shadowAngle, this.Position, TREE_SCALE, TREE_SCALE * SHADOW_LEN_SCALE);

            // Adjust the bottom two points of the shadow to line up with the bottom of the trunk.
            _trunkTransPolyShadow[0] = _trunkTransPoly[1];
            _trunkTransPolyShadow[1] = _trunkTransPoly[0];

            // Trunk shadow.
            ctx.FillPolygon(_trunkTransPolyShadow, shadowColor);

            // Leaf shadow.
            ctx.PushTransform();
            ctx.RotateTransform(shadowAngle, this.Position);
            ctx.ScaleTransform(1f, SHADOW_LEN_SCALE, this.Position);

            ctx.FillEllipse(new D2DEllipse(shadowLeafPos, _leafSize), shadowColor);

            ctx.PopTransform();

            // Draw tree.
            var trunkPos = this.Position + (-D2DPoint.UnitY * TotalHeight);
            ctx.FillPolygonWithLighting(_trunkTransPoly, trunkPos, trunkColor, LIGHT_INTENSITY);

            // Draw leaf gradient.
            ctx.PushTransform();
            ctx.TranslateTransform(normalLeafPos * ctx.CurrentScale);

            var leafEllipse = new D2DEllipse(D2DPoint.Zero, _leafSize);
            ctx.Gfx.FillEllipse(leafEllipse, _leafBrush);

            ctx.PopTransform();

            // Add ToD color overlay.
            leafEllipse.origin = normalLeafPos;
            ctx.FillEllipseWithLighting(leafEllipse, leafToDColor, LIGHT_INTENSITY);
        }

        public override void Dispose()
        {
            base.Dispose();

            _leafBrush?.Dispose();
        }
    }

    public class PineTree : Tree
    {
        public float Width;

        public readonly D2DPoint[] TopPoly;
        private D2DPoint[] _topTrans;
        private D2DPoint[] _topTransShadow;

        public readonly D2DPoint[] TrunkPoly;
        private D2DPoint[] _trunkTransPoly;
        private D2DPoint[] _trunkTransPolyShadow;

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
            _topTransShadow = new D2DPoint[TopPoly.Length];

            var trunkRect = new D2DRect(D2DPoint.Zero, new D2DSize(this.Width / 2f, this.Height));

            TrunkPoly =
            [
                new D2DPoint(trunkRect.right, trunkRect.top + (trunkRect.Height * 0.5f)),
                new D2DPoint(trunkRect.left, trunkRect.top + (trunkRect.Height * 0.5f)),
                new D2DPoint(trunkRect.left, trunkRect.bottom + (trunkRect.Height * 0.5f)),
                new D2DPoint(trunkRect.right, trunkRect.bottom + (trunkRect.Height * 0.5f)),
            ];

            _trunkTransPoly = new D2DPoint[TrunkPoly.Length];
            _trunkTransPolyShadow = new D2DPoint[TrunkPoly.Length];

            TopPoly.Translate(_topTrans, 180f, this.Position - new D2DPoint(0, this.Height), TREE_SCALE);
            TrunkPoly.Translate(_trunkTransPoly, 180f, this.Position, 1f);

            var shadowTopPos = this.Position + new D2DPoint(0, this.Height);
            TopPoly.Translate(_topTransShadow, 0f, shadowTopPos, TREE_SCALE);
        }

        public override void Render(RenderContext ctx, D2DColor timeOfDayColor, D2DColor shadowColor, float shadowAngle)
        {
            // Add time of day color
            var trunkColor = Utilities.LerpColor(this.TrunkColor, timeOfDayColor, 0.3f);
            var leafColor = Utilities.LerpColor(this.LeafColor, timeOfDayColor, 0.3f);

            // Draw shadow.
            TrunkPoly.Translate(_trunkTransPolyShadow, D2DPoint.Zero, shadowAngle, this.Position, 1f, SHADOW_LEN_SCALE);

            //Adjust the bottom two points of the shadow to line up with the bottom of the trunk.
            _trunkTransPolyShadow[0] = _trunkTransPoly[1];
            _trunkTransPolyShadow[1] = _trunkTransPoly[0];

            // Trunk shadow.
            ctx.FillPolygon(_trunkTransPolyShadow, shadowColor);

            // Top shadow.
            ctx.PushTransform();
            ctx.RotateTransform(shadowAngle, this.Position);
            ctx.ScaleTransform(1f, SHADOW_LEN_SCALE, this.Position);

            ctx.FillPolygon(_topTransShadow, shadowColor);

            ctx.PopTransform();

            // Draw tree.
            var centerPos = this.Position + (-D2DPoint.UnitY * (TotalHeight * 2f));
            ctx.FillPolygonWithLighting(_trunkTransPoly, centerPos, trunkColor, LIGHT_INTENSITY);
            ctx.FillPolygonWithLighting(_topTrans, centerPos, leafColor, LIGHT_INTENSITY);
        }
    }
}