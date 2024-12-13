using PolyPlane.GameObjects.Tools;
using unvell.D2DLib;

namespace PolyPlane.Rendering
{
    /// <summary>
    /// Provides overloads of common graphics methods which include automagic viewport clamping for performance.
    /// </summary>
    public class RenderContext
    {
        public D2DGraphics Gfx;
        public D2DDevice Device;
        public D2DRect Viewport;
        public LightMap LightMap;

        public float CurrentScale
        {
            get
            {
                if (Gfx != null)
                    return Gfx.GetTransform().M11;

                return 1f;
            }
        }

        private Stack<D2DRect> _vpStack = new Stack<D2DRect>();
        private D2DSolidColorBrush _cachedBrush;

        public RenderContext(D2DGraphics gfx, D2DDevice device)
        {
            Gfx = gfx;
            Device = device;
            LightMap = new LightMap();

            _cachedBrush = device.CreateSolidColorBrush(D2DColor.Transparent);
        }

        public void PushViewPort(D2DRect viewport)
        {
            _vpStack.Push(Viewport);

            Viewport = viewport;
        }

        public void PopViewPort()
        {
            Viewport = _vpStack.Pop();
        }

        public void FillEllipseWithLighting(D2DEllipse ellipse, D2DColor color, float maxIntensity)
        {
            FillEllipseWithLighting(ellipse, color, LightMap.Colors.DefaultLightingColor, 0f, maxIntensity);
        }

        public void FillEllipseWithLighting(D2DEllipse ellipse, D2DColor color, D2DColor lightColor, float minIntensity, float maxIntensity)
        {
            if (World.UseLightMap) 
            {
                var lightedColor = LightMap.SampleColor(ellipse.origin, color, lightColor, minIntensity, maxIntensity);
                FillEllipse(ellipse, lightedColor);
            }
            else
            {
                FillEllipse(ellipse, color);
            }
        }

        public void FillEllipse(D2DEllipse ellipse, D2DColor color)
        {
            // Use cached brush for performance.
            _cachedBrush.Color = color;

            // Perf hack:
            // Check the final radius after scaling and switch to drawing rectangles below a certain size.
            // Drawing rectangles is much faster than ellipses.
            var viewRad = ellipse.radiusX * this.CurrentScale;
            if (World.FastPrimitives && viewRad > World.FAST_PRIMITIVE_MIN_SIZE || !World.FastPrimitives)
            {
                FillEllipse(ellipse, _cachedBrush);
            }
            else
            {
                FillRectangle(new D2DRect(ellipse.origin, new D2DSize(ellipse.radiusX * 2f, ellipse.radiusY * 2f)), _cachedBrush);
            }
        }

        public void FillEllipse(D2DEllipse ellipse, D2DBrush brush)
        {
            Gfx.FillEllipseClamped(Viewport, ellipse, brush);
        }

        public void FillEllipseSimple(D2DPoint pos, float radius, D2DColor color)
        {
            Gfx.FillEllipseClamped(Viewport, new D2DEllipse(pos, new D2DSize(radius, radius)), color);
        }

        public void FillEllipseSimple(D2DPoint pos, float radius, D2DBrush brush)
        {
            Gfx.FillEllipseClamped(Viewport, new D2DEllipse(pos, new D2DSize(radius, radius)), brush);
        }

        public void DrawEllipse(D2DEllipse ellipse, D2DColor color, float weight = 1f, D2DDashStyle dashStyle = D2DDashStyle.Solid)
        {
            Gfx.DrawEllipseClamped(Viewport, ellipse, color, weight, dashStyle);
        }

        public void DrawLine(D2DPoint start, D2DPoint end, D2DColor color, float weight = 1f, D2DDashStyle dashStyle = D2DDashStyle.Solid, D2DCapStyle startCap = D2DCapStyle.Flat, D2DCapStyle endCap = D2DCapStyle.Flat)
        {
            Gfx.DrawLineClamped(Viewport, start, end, color, weight, dashStyle, startCap, endCap);
        }

        public void DrawPolygon(D2DPoint[] points, D2DColor strokeColor, float strokeWidth, D2DDashStyle dashStyle, D2DColor fillColor)
        {
            Gfx.DrawPolygonClamped(Viewport, points, strokeColor, strokeWidth, dashStyle, fillColor);
        }

        public void DrawPolygonWithLighting(RenderPoly poly, D2DPoint centerPos, D2DColor strokeColor, float strokeWidth, D2DDashStyle dashStyle, D2DColor fillColor, float maxIntensity)
        {
            if (World.UseLightMap)
            {
                var lightedColor = LightMap.SampleColor(centerPos, fillColor, LightMap.Colors.DefaultLightingColor, 0, maxIntensity);
                DrawPolygon(poly, strokeColor, strokeWidth, dashStyle, lightedColor);
            }
            else
            {
                DrawPolygon(poly, strokeColor, strokeWidth, dashStyle, fillColor);
            }
        }

        public void DrawPolygon(RenderPoly poly, D2DColor strokeColor, float strokeWidth, D2DDashStyle dashStyle, D2DColor fillColor)
        {
            Gfx.DrawPolygonClamped(Viewport, poly, strokeColor, strokeWidth, dashStyle, fillColor);
        }

        public void DrawPolygon(D2DPoint[] points, D2DColor strokeColor, float strokeWidth, D2DDashStyle dashStyle, D2DBrush fillBrush)
        {
            Gfx.DrawPolygonClamped(Viewport, points, strokeColor, strokeWidth, dashStyle, fillBrush);
        }

        public void FillRectangle(D2DRect rect, D2DColor color)
        {
            Gfx.FillRectangleClamped(Viewport, rect, color);
        }

        public void FillRectangle(D2DRect rect, D2DBrush brush)
        {
            Gfx.FillRectangleClamped(Viewport, rect, brush);
        }

        public void FillRectangle(float x, float y, float width, float height, D2DColor color)
        {
            Gfx.FillRectangleClamped(Viewport, x, y, width, height, color);
        }

        public void DrawTextCenter(string text, D2DColor color, string fontName, float fontSize, D2DRect rect)
        {
            Gfx.DrawTextCenterClamped(Viewport, text, color, fontName, fontSize, rect);
        }

        public void DrawRectangle(D2DRect rect, D2DColor color, float strokeWidth = 1f, D2DDashStyle dashStyle = D2DDashStyle.Solid)
        {
            Gfx.DrawRectangleClamped(Viewport, rect, color, strokeWidth, dashStyle);
        }

        public void DrawText(string text, D2DColor color, string fontName, float fontSize, D2DRect rect, DWriteTextAlignment halign = DWriteTextAlignment.Leading, DWriteParagraphAlignment valign = DWriteParagraphAlignment.Near)
        {
            Gfx.DrawTextClamped(Viewport, text, color, fontName, fontSize, rect, halign, valign);
        }

        public void DrawText(string text, D2DColor color, string fontName, float fontSize, float x, float y, DWriteTextAlignment halign = DWriteTextAlignment.Leading, DWriteParagraphAlignment valign = DWriteParagraphAlignment.Near)
        {
            Gfx.DrawTextClamped(Viewport, text, color, fontName, fontSize, new D2DRect(x, y, 99999f, 99999f), halign, valign);
        }

        public void DrawText(string text, D2DBrush brush, D2DTextFormat format, D2DRect rect)
        {
            Gfx.DrawTextClamped(Viewport, text, brush, format, rect);
        }

        public void DrawArrow(D2DPoint start, D2DPoint end, D2DColor color, float weight, float arrowLen = 10f)
        {
            Gfx.DrawArrowClamped(Viewport, start, end, color, weight, arrowLen);
        }

        public void DrawArrowStroked(D2DPoint start, D2DPoint end, D2DColor color, float weight, D2DColor strokeColor, float strokeWeight)
        {
            Gfx.DrawArrowStrokedClamped(Viewport, start, end, color, weight, strokeColor, strokeWeight);
        }
    }
}
