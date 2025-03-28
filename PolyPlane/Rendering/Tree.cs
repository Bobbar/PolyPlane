using PolyPlane.Helpers;
using unvell.D2DLib;

namespace PolyPlane.Rendering
{
    public abstract class Tree : IDisposable
    {
        public D2DPoint Position;
        public float TotalHeight;

        protected float Height;
        protected D2DColor TrunkColor;
        protected D2DColor LeafColor;
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
        private float _radius;
        private float _trunkWidth;
        private readonly D2DPoint[] _trunkPoly;
        private D2DPoint[] _trunkTransPolyShadow;
        private D2DPoint[] _trunkTransPoly;
        private D2DPoint _leafYPos;
        private D2DPoint _shadowLeafPos;
        private D2DPoint _normalLeafPos;
        private D2DSize _leafSize;
        private D2DRadialGradientBrush _leafBrush = null;
        private D2DLinearGradientBrush _trunkOverlayBrush = null;


        public NormalTree(D2DPoint pos, float height, float radius, float trunkWidth, D2DColor trunkColor, D2DColor leafColor) : base(pos, height, trunkColor, leafColor)
        {
            _radius = radius;
            _trunkWidth = trunkWidth;
            TotalHeight = height + radius;

            _trunkPoly =
            [
                new D2DPoint(-_trunkWidth, 0),
                new D2DPoint(_trunkWidth, 0),
                new D2DPoint(_trunkWidth * 0.25f, this.Height),
                new D2DPoint(-_trunkWidth * 0.25f, this.Height),
            ];

            _trunkTransPoly = new D2DPoint[_trunkPoly.Length];
            _trunkTransPolyShadow = new D2DPoint[_trunkPoly.Length];

            _trunkPoly.Translate(_trunkTransPoly, 180f, this.Position, TREE_SCALE);

            _leafYPos = new D2DPoint(0f, (-Height * TREE_SCALE) - (_radius - 0.1f));
            _shadowLeafPos = this.Position - _leafYPos;
            _normalLeafPos = this.Position + _leafYPos;
            _leafSize = new D2DSize(_radius, _radius);
        }

        private void InitBrushes(RenderContext ctx)
        {
            if (_leafBrush == null)
                _leafBrush = ctx.Device.CreateRadialGradientBrush(D2DPoint.Zero, D2DPoint.Zero, this._radius, this._radius, [new D2DGradientStop(0f, this.LeafColor), new D2DGradientStop(1f, Utilities.LerpColor(this.LeafColor, D2DColor.Black, 0.2f))]);

            // Leaf trunk occlusion overlay.
            if (_trunkOverlayBrush == null)
            {
                var trunkLeafRatio = (_radius / Height);
                var endStopPos = trunkLeafRatio - (trunkLeafRatio * 0.7f);
                var startPos = _normalLeafPos + new D2DPoint(0f, _radius);
                var startStop = new D2DGradientStop(0f, D2DColor.Black.WithAlpha(0.4f));
                var endStop = new D2DGradientStop(endStopPos, D2DColor.Transparent);

                _trunkOverlayBrush = ctx.Device.CreateLinearGradientBrush(startPos, this.Position, [startStop, endStop]);
            }
        }

        public override void Render(RenderContext ctx, D2DColor timeOfDayColor, D2DColor shadowColor, float shadowAngle)
        {
            InitBrushes(ctx);

            // Add time of day color
            var trunkColor = Utilities.LerpColor(this.TrunkColor, timeOfDayColor, 0.3f);
            var leafToDColor = new D2DColor(0.2f, timeOfDayColor);

            // Draw shadows.

            // Rotate trunk shadow.
            _trunkPoly.Translate(_trunkTransPolyShadow, D2DPoint.Zero, shadowAngle, this.Position, TREE_SCALE, TREE_SCALE * SHADOW_LEN_SCALE);

            // Adjust the bottom two points of the shadow to line up with the bottom of the trunk.
            _trunkTransPolyShadow[0] = _trunkTransPoly[1];
            _trunkTransPolyShadow[1] = _trunkTransPoly[0];

            // Trunk shadow.
            ctx.FillPolygon(_trunkTransPolyShadow, shadowColor);

            // Leaf shadow.
            ctx.PushTransform();
            ctx.RotateTransform(shadowAngle, this.Position);
            ctx.ScaleTransform(1f, SHADOW_LEN_SCALE, this.Position);

            ctx.FillEllipse(new D2DEllipse(_shadowLeafPos, _leafSize), shadowColor);

            ctx.PopTransform();

            // Normal trunk.
            var trunkPos = this.Position + (-D2DPoint.UnitY * TotalHeight);
            ctx.FillPolygonWithLighting(_trunkTransPoly, trunkPos, trunkColor, LIGHT_INTENSITY);

            // Trunk occlusion overlay.
            ctx.FillPolygon(_trunkTransPoly, _trunkOverlayBrush);

            // Leaf gradient.
            ctx.PushTransform();
            ctx.TranslateTransform(_normalLeafPos * ctx.CurrentScale);

            var leafEllipse = new D2DEllipse(D2DPoint.Zero, _leafSize);
            ctx.Gfx.FillEllipse(leafEllipse, _leafBrush);

            ctx.PopTransform();

            // Add ToD color overlay.
            leafEllipse.origin = _normalLeafPos;
            ctx.FillEllipseWithLighting(leafEllipse, leafToDColor, LIGHT_INTENSITY);
        }

        public override void Dispose()
        {
            base.Dispose();

            _leafBrush?.Dispose();
            _trunkOverlayBrush?.Dispose();
        }
    }

    public class PineTree : Tree
    {
        private float _width;

        private readonly D2DPoint[] _topPoly;
        private D2DPoint[] _topTrans;
        private D2DPoint[] _topTransShadow;

        private readonly D2DPoint[] _trunkPoly;
        private D2DPoint[] _trunkTransPoly;
        private D2DPoint[] _trunkTransPolyShadow;

        private D2DLinearGradientBrush _trunkOverlayBrush = null;

        public PineTree(D2DPoint pos, float height, float width, D2DColor trunkColor, D2DColor leafColor) : base(pos, height, trunkColor, leafColor)
        {
            _width = width;
            TotalHeight = height;

            _topPoly =
            [
                new D2DPoint(-(this._width / 2f), 0),
                new D2DPoint((this._width / 2f), 0),
                new D2DPoint(0, this.Height),
            ];

            _topTrans = new D2DPoint[_topPoly.Length];
            _topTransShadow = new D2DPoint[_topPoly.Length];

            var trunkRect = new D2DRect(D2DPoint.Zero, new D2DSize(this._width / 2f, this.Height));

            _trunkPoly =
            [
                new D2DPoint(trunkRect.right, trunkRect.top + (trunkRect.Height * 0.5f)),
                new D2DPoint(trunkRect.left, trunkRect.top + (trunkRect.Height * 0.5f)),
                new D2DPoint(trunkRect.left, trunkRect.bottom + (trunkRect.Height * 0.5f)),
                new D2DPoint(trunkRect.right, trunkRect.bottom + (trunkRect.Height * 0.5f)),
            ];

            _trunkTransPoly = new D2DPoint[_trunkPoly.Length];
            _trunkTransPolyShadow = new D2DPoint[_trunkPoly.Length];

            _topPoly.Translate(_topTrans, 180f, this.Position - new D2DPoint(0, this.Height), TREE_SCALE);
            _trunkPoly.Translate(_trunkTransPoly, 180f, this.Position, 1f);

            var shadowTopPos = this.Position + new D2DPoint(0, this.Height);
            _topPoly.Translate(_topTransShadow, 0f, shadowTopPos, TREE_SCALE);
        }

        public override void Render(RenderContext ctx, D2DColor timeOfDayColor, D2DColor shadowColor, float shadowAngle)
        {
            // Leaf trunk occlusion overlay.
            if (_trunkOverlayBrush == null)
            {
                var endStopPos = 0.7f;
                var startPos = this.Position - new D2DPoint(0, this.Height);
                var startStop = new D2DGradientStop(0f, D2DColor.Black.WithAlpha(0.5f));
                var endStop = new D2DGradientStop(endStopPos, D2DColor.Transparent);

                _trunkOverlayBrush = ctx.Device.CreateLinearGradientBrush(startPos, this.Position, [startStop, endStop]);
            }

            // Add time of day color
            var trunkColor = Utilities.LerpColor(this.TrunkColor, timeOfDayColor, 0.3f);
            var leafColor = Utilities.LerpColor(this.LeafColor, timeOfDayColor, 0.3f);

            // Rotate trunk shadow poly.
            _trunkPoly.Translate(_trunkTransPolyShadow, D2DPoint.Zero, shadowAngle, this.Position, 1f, SHADOW_LEN_SCALE);

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

            // Normal trunk.
            ctx.FillPolygonWithLighting(_trunkTransPoly, centerPos + new D2DPoint(0, this.Height), trunkColor, LIGHT_INTENSITY);

            // Trunk occlusion overlay.
            ctx.FillPolygon(_trunkTransPoly, _trunkOverlayBrush);

            // Normal top.
            ctx.FillPolygonWithLighting(_topTrans, centerPos, leafColor, LIGHT_INTENSITY);
        }

        public override void Dispose()
        {
            base.Dispose();

            _trunkOverlayBrush?.Dispose();
        }
    }
}