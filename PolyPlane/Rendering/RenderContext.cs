using PolyPlane.GameObjects.Tools;
using PolyPlane.Helpers;
using unvell.D2DLib;

namespace PolyPlane.Rendering
{
    /// <summary>
    /// Provides overloads of common graphics methods which include automagic viewport clipping for performance.
    /// </summary>
    public class RenderContext : IDisposable
    {
        public readonly D2DGraphics Gfx;
        public readonly D2DDevice Device;
        public D2DRect Viewport;
        public readonly LightMap LightMap;

        public float LightMapAlpha
        {
            get
            {
                return _lightMapAlpha;
            }
        }

        public float CurrentScale
        {
            get
            {
                return _currentScale;
            }
        }

        private Stack<D2DRect> _vpStack = new Stack<D2DRect>();
        private D2DSolidColorBrush _cachedBrush;
        private float _currentScale = 1f;
        private float _currentLightingFactor = 1f;
        private float _lightMapAlpha = 0.02f;
        private D2DColor _timeOfDayColor = D2DColor.Transparent;

        private const double GaussianSigma2 = 0.035;
        private readonly double GaussianSigma = Math.Sqrt(2.0 * Math.PI * GaussianSigma2);

        public RenderContext(D2DGraphics gfx, D2DDevice device)
        {
            Gfx = gfx;
            Device = device;
            LightMap = new LightMap();

            _cachedBrush = device.CreateSolidColorBrush(D2DColor.Transparent);

            UpdateTimeOfDayColors();
        }

        private void UpdateTimeOfDayColors()
        {
            // Compute a TimeOfDay factor to be applied to all lighting intensity.
            // Decrease lighting intensity during the day.
            var factor = Math.Clamp(Utilities.FactorWithEasing(World.TimeOfDay, World.MAX_TIMEOFDAY - 5, EasingFunctions.EaseLinear), 0.5f, 1f);
            _currentLightingFactor = factor;

            var todColor = InterpolateColorGaussian(World.TimeOfDayPallet, World.TimeOfDay, World.MAX_TIMEOFDAY);
            _timeOfDayColor = todColor;

            var lightMapAlpha = Utilities.Lerp(0.02f, 0.23f, Utilities.Factor(World.TimeOfDay, World.MAX_TIMEOFDAY));
            _lightMapAlpha = lightMapAlpha;
        }

        /// <summary>
        /// Get the sun angle for the current time of day.
        /// </summary>
        /// <returns></returns>
        public float GetTimeOfDaySunAngle()
        {
            const float TOD_ANGLE_START = 45f;
            const float TOD_ANGLE_END = 135f;

            var todAngle = Utilities.Lerp(TOD_ANGLE_START, TOD_ANGLE_END, Utilities.Factor(World.TimeOfDay, World.MAX_TIMEOFDAY));

            return todAngle;
        }

        /// <summary>
        /// Get the color for the current time of day from the time of day pallet.
        /// </summary>
        /// <returns></returns>
        public D2DColor GetTimeOfDayColor()
        {
            return _timeOfDayColor;
        }

        /// <summary>
        /// Adds the current time of day color to the specified color.
        /// </summary>
        /// <param name="color"></param>
        /// <returns></returns>
        public D2DColor AddTimeOfDayColor(D2DColor color)
        {
            var todColor = GetTimeOfDayColor();
            return AddTimeOfDayColor(color, todColor);
        }

        /// <summary>
        /// Blend the specified color with the specified time of day color.  Used to make sure all time of day coloring is consistent.
        /// </summary>
        /// <param name="color"></param>
        /// <param name="todColor"></param>
        /// <returns></returns>
        public D2DColor AddTimeOfDayColor(D2DColor color, D2DColor todColor)
        {
            const float AMT = 0.35f;
            return Utilities.LerpColor(color, todColor, AMT);
        }

        /// <summary>
        /// Gets the shadow color for the current time of day.  (A darker variation of the time of day color)
        /// </summary>
        /// <returns></returns>
        public D2DColor GetShadowColor()
        {
            var shadowColor = Utilities.LerpColorWithAlpha(GetTimeOfDayColor(), D2DColor.Black, 0.7f, 0.4f);
            return shadowColor;
        }

        private D2DColor GetLightMapColor(D2DPoint location, D2DColor initColor, float minIntensity, float maxIntensity)
        {
            return LightMap.SampleColor(location, initColor, minIntensity * _currentLightingFactor, maxIntensity * _currentLightingFactor);
        }

        /// <summary>
        /// Disable viewport clipping for all future render calls.
        /// </summary>
        public void DisableClipping()
        {
            GraphicsExtensions.DisableClipping = true;
        }

        /// <summary>
        /// Enable viewport clipping for all future render calls.
        /// </summary>
        public void EnableClipping()
        {
            GraphicsExtensions.DisableClipping = false;
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

        public void BeginRender(D2DBitmap bitmap)
        {
            Gfx.BeginRender(bitmap);
            UpdateTimeOfDayColors();

            // Reset on-screen/off-screen object stats.
            GraphicsExtensions.OnScreen = 0;
            GraphicsExtensions.OffScreen = 0;
        }

        public void BeginRender(D2DColor color)
        {
            Gfx.BeginRender(color);
            UpdateTimeOfDayColors();

            // Reset on-screen/off-screen object stats.
            GraphicsExtensions.OnScreen = 0;
            GraphicsExtensions.OffScreen = 0;
        }

        public void EndRender()
        {
            Gfx.EndRender();
        }

        public void PushTransform()
        {
            Gfx.PushTransform();
        }

        public void PopTransform()
        {
            Gfx.PopTransform();

            UpdateScale();
        }

        public void TranslateTransform(D2DPoint pos)
        {
            TranslateTransform(pos.X, pos.Y);
        }

        public void TranslateTransform(float x, float y)
        {
            Gfx.TranslateTransform(x, y);

            UpdateScale();
        }

        public void ScaleTransform(float xy)
        {
            Gfx.ScaleTransform(xy, xy);
            UpdateScale();
        }

        public void ScaleTransform(float xy, D2DPoint center)
        {
            ScaleTransform(xy, xy, center);
        }

        public void ScaleTransform(float x, float y, D2DPoint center)
        {
            Gfx.ScaleTransform(x, y, center);
            UpdateScale();
        }

        public void RotateTransform(float angle, D2DPoint center)
        {
            Gfx.RotateTransform(angle, center);
            UpdateScale();
        }

        public void PushLayer(D2DLayer layer, D2DRect rectBounds, D2DGeometry? geometry = null, D2DBrush? opacityBrush = null)
        {
            Gfx.PushLayer(layer, rectBounds, geometry, opacityBrush);
        }

        public void PopLayer()
        {
            Gfx.PopLayer();
        }

        public void PushClip(D2DRect rect)
        {
            Gfx.PushClip(rect);
        }

        public void PopClip()
        {
            Gfx.PopClip();
        }

        private void UpdateScale()
        {
            var trans = Gfx.GetTransform();
            var scaleX = MathF.Sqrt(trans.M11 * trans.M11 + trans.M12 * trans.M12);
            _currentScale = scaleX;
        }

        public void FillEllipseWithLighting(D2DEllipse ellipse, D2DPoint sampleLocation, D2DColor color, float maxIntensity, bool clipped = true)
        {
            FillEllipseWithLighting(ellipse, sampleLocation, color, 0f, maxIntensity, clipped);
        }

        public void FillEllipseWithLighting(D2DEllipse ellipse, D2DColor color, float maxIntensity, bool clipped = true)
        {
            FillEllipseWithLighting(ellipse, color, 0f, maxIntensity, clipped);
        }

        public void FillEllipseWithLighting(D2DEllipse ellipse, D2DPoint sampleLocation, D2DColor color, float minIntensity, float maxIntensity, bool clipped = true)
        {
            if (World.UseLightMap)
            {
                var lightedColor = GetLightMapColor(sampleLocation, color, minIntensity, maxIntensity);
                FillEllipse(ellipse, lightedColor, clipped);
            }
            else
            {
                FillEllipse(ellipse, color, clipped);
            }
        }

        public void FillEllipseWithLighting(D2DEllipse ellipse, D2DColor color, float minIntensity, float maxIntensity, bool clipped = true)
        {
            if (World.UseLightMap)
            {
                var lightedColor = GetLightMapColor(ellipse.origin, color, minIntensity, maxIntensity);
                FillEllipse(ellipse, lightedColor, clipped);
            }
            else
            {
                FillEllipse(ellipse, color, clipped);
            }
        }

        public void FillEllipse(D2DEllipse ellipse, D2DColor color, bool clipped = true)
        {
            // Skip rendering of alpha is low enough to be invisible.
            if (color.a <= World.MIN_RENDER_ALPHA)
                return;

            // Use cached brush for performance.
            _cachedBrush.Color = color;

            // Perf hack:
            // Check the final radius after scaling and switch to drawing rectangles below a certain size.
            // Drawing rectangles is much faster than ellipses.
            var scale = this.CurrentScale;
            var viewRad = Math.Max(ellipse.radiusX * scale, ellipse.radiusY * scale);
            if (World.FastPrimitives && viewRad > World.FAST_PRIMITIVE_MIN_SIZE || !World.FastPrimitives)
            {
                FillEllipse(ellipse, _cachedBrush, clipped);
            }
            else
            {
                // Just skip rendering entirely for extremely small ellipses.
                if (viewRad > World.MIN_ELLIPSE_RENDER_SIZE)
                    FillRectangle(new D2DRect(ellipse.origin, new D2DSize(ellipse.radiusX * 2f, ellipse.radiusY * 2f)), _cachedBrush);
            }
        }

        public void FillEllipse(D2DEllipse ellipse, D2DBrush brush, bool clipped = true)
        {
            if (clipped)
                Gfx.FillEllipseClipped(Viewport, ellipse, brush);
            else
            {
                Gfx.FillEllipse(ellipse, brush);
                GraphicsExtensions.OnScreen++;
            }
        }

        public void FillEllipseSimple(D2DPoint pos, float radius, D2DColor color, bool clipped = true)
        {
            if (clipped)
                Gfx.FillEllipseClipped(Viewport, new D2DEllipse(pos, new D2DSize(radius, radius)), color);
            else
                Gfx.FillEllipse(new D2DEllipse(pos, new D2DSize(radius, radius)), color);
        }

        public void FillEllipseSimple(D2DPoint pos, float radius, D2DBrush brush, bool clipped = true)
        {
            if (clipped)
                Gfx.FillEllipseClipped(Viewport, new D2DEllipse(pos, new D2DSize(radius, radius)), brush);
            else
                Gfx.FillEllipse(new D2DEllipse(pos, new D2DSize(radius, radius)), brush);
        }

        public void DrawEllipse(D2DEllipse ellipse, D2DColor color, float weight = 1f, D2DDashStyle dashStyle = D2DDashStyle.Solid)
        {
            Gfx.DrawEllipseClipped(Viewport, ellipse, color, weight, dashStyle);
        }

        public void DrawLine(D2DPoint start, D2DPoint end, D2DColor color, float weight = 1f, D2DDashStyle dashStyle = D2DDashStyle.Solid, D2DCapStyle startCap = D2DCapStyle.Flat, D2DCapStyle endCap = D2DCapStyle.Flat)
        {
            Gfx.DrawLineClipped(Viewport, start, end, color, weight, dashStyle, startCap, endCap);
        }

        public void DrawLineWithLighting(D2DPoint start, D2DPoint end, D2DColor color, float maxIntensity, float weight = 1f, D2DDashStyle dashStyle = D2DDashStyle.Solid, D2DCapStyle startCap = D2DCapStyle.Flat, D2DCapStyle endCap = D2DCapStyle.Flat)
        {
            if (World.UseLightMap)
            {
                var lightedColor = GetLightMapColor((start + end) * 0.5f, color, 0f, maxIntensity);
                DrawLine(start, end, lightedColor, weight, dashStyle, startCap, endCap);
            }
            else
            {
                DrawLine(start, end, color, weight, dashStyle, startCap, endCap);
            }
        }

        public void DrawTriangle(D2DPoint position, D2DColor color, D2DColor fillColor, float scale = 1f)
        {
            Gfx.DrawTriangle(position, color, fillColor, scale);
        }

        public void DrawCrosshair(D2DPoint pos, float weight, D2DColor color, float innerRadius, float outerRadius)
        {
            Gfx.DrawCrosshair(pos, weight, color, innerRadius, outerRadius);
        }

        public void DrawPolygon(RenderPoly poly, D2DColor strokeColor, float strokeWidth, D2DColor fillColor)
        {
            DrawPolygon(poly.Poly, strokeColor, strokeWidth, D2DDashStyle.Solid, fillColor);
        }

        public void DrawPolygon(D2DPoint[] points, D2DColor strokeColor, float strokeWidth, D2DDashStyle dashStyle, D2DColor fillColor)
        {
            Gfx.DrawPolygonClipped(Viewport, points, strokeColor, strokeWidth, dashStyle, fillColor);
        }

        public void FillPolygon(D2DPoint[] points, D2DColor fillColor, bool clipped = true)
        {
            if (clipped)
                Gfx.DrawPolygonClipped(Viewport, points, D2DColor.Transparent, 0f, D2DDashStyle.Solid, fillColor);
            else
                Gfx.DrawPolygon(points, D2DColor.Transparent, 0f, D2DDashStyle.Solid, fillColor);
        }

        public void FillPolygon(D2DPoint[] points, D2DBrush fillBrush, bool clipped = true)
        {
            if (clipped)
                Gfx.DrawPolygonClipped(Viewport, points, D2DColor.Transparent, 0f, D2DDashStyle.Solid, fillBrush);
            else
                Gfx.DrawPolygon(points, D2DColor.Transparent, 0f, D2DDashStyle.Solid, fillBrush);
        }

        public void FillPolygon(RenderPoly poly, D2DColor fillColor)
        {
            Gfx.DrawPolygonClipped(Viewport, poly.Poly, D2DColor.Transparent, 0f, D2DDashStyle.Solid, fillColor);
        }

        public void FillPolygonWithLighting(D2DPoint[] points, D2DPoint centerPos, D2DColor fillColor, float maxIntensity, bool clipped = true)
        {
            if (World.UseLightMap)
            {
                var lightedColor = GetLightMapColor(centerPos, fillColor, 0, maxIntensity);
                FillPolygon(points, lightedColor, clipped);
            }
            else
            {
                FillPolygon(points, fillColor, clipped);
            }
        }

        public void DrawPolygonWithLighting(RenderPoly poly, D2DPoint centerPos, D2DColor strokeColor, float strokeWidth, D2DColor fillColor, float maxIntensity)
        {
            DrawPolygonWithLighting(poly.Poly, centerPos, strokeColor, strokeWidth, D2DDashStyle.Solid, fillColor, maxIntensity);
        }

        public void DrawPolygonWithLighting(D2DPoint[] points, D2DPoint centerPos, D2DColor strokeColor, float strokeWidth, D2DDashStyle dashStyle, D2DColor fillColor, float maxIntensity)
        {
            if (World.UseLightMap)
            {
                var lightedColor = GetLightMapColor(centerPos, fillColor, 0, maxIntensity);
                DrawPolygon(points, strokeColor, strokeWidth, dashStyle, lightedColor);
            }
            else
            {
                DrawPolygon(points, strokeColor, strokeWidth, dashStyle, fillColor);
            }
        }

        public void FillRectangle(D2DRect rect, D2DColor color, bool clipped = true)
        {
            if (clipped)
                Gfx.FillRectangleClipped(Viewport, rect, color);
            else
                Gfx.FillRectangle(rect, color);
        }

        public void FillRectangle(D2DRect rect, D2DBrush brush, bool clipped = true)
        {
            if (clipped)
                Gfx.FillRectangleClipped(Viewport, rect, brush);
            else
                Gfx.FillRectangle(rect, brush);
        }

        public void FillRectangle(float x, float y, float width, float height, D2DColor color)
        {
            Gfx.FillRectangleClipped(Viewport, x, y, width, height, color);
        }

        public void DrawTextCenter(string text, D2DColor color, string fontName, float fontSize, D2DRect rect)
        {
            Gfx.DrawTextCenterClipped(Viewport, text, color, fontName, fontSize, rect);
        }

        public void DrawRectangle(D2DRect rect, D2DColor color, float strokeWidth = 1f, D2DDashStyle dashStyle = D2DDashStyle.Solid)
        {
            Gfx.DrawRectangleClipped(Viewport, rect, color, strokeWidth, dashStyle);
        }

        public void DrawText(string text, D2DSolidColorBrush brush, D2DTextFormat format, D2DRect rect)
        {
            Gfx.DrawText(text, brush, format, rect);
        }

        public void DrawText(string text, D2DColor color, D2DTextFormat format, D2DRect rect)
        {
            _cachedBrush.Color = color;
            DrawText(text, _cachedBrush, format, rect);
        }

        public void DrawArrow(D2DPoint start, D2DPoint end, D2DColor color, float weight, float arrowLen = 10f)
        {
            Gfx.DrawArrowClipped(Viewport, start, end, color, weight, arrowLen);
        }

        public void DrawProgressBar(D2DPoint position, D2DSize size, D2DColor borderColor, D2DColor fillColor, float percent)
        {
            Gfx.DrawProgressBarClipped(Viewport, position, size, borderColor, fillColor, percent);
        }

        public D2DSize MeasureText(string text, string fontName, float fontSize, D2DSize placeSize)
        {
            return Gfx.MeasureText(text, fontName, fontSize, placeSize);
        }

        private D2DColor InterpolateColorGaussian(D2DColor[] colors, float value, float maxValue)
        {
            var x = Math.Min(1.0f, value / maxValue);

            double r = 0.0, g = 0.0, b = 0.0;
            double total = 0.0;
            double step = 1.0 / (double)(colors.Length - 1);
            double mu = 0.0;

            for (int i = 0; i < colors.Length; i++)
            {
                total += Math.Exp(-(x - mu) * (x - mu) / (2.0 * GaussianSigma2)) / GaussianSigma;
                mu += step;
            }

            mu = 0.0;
            for (int i = 0; i < colors.Length; i++)
            {
                var color = colors[i];
                double percent = Math.Exp(-(x - mu) * (x - mu) / (2.0 * GaussianSigma2)) / GaussianSigma;
                mu += step;

                r += color.r * percent / total;
                g += color.g * percent / total;
                b += color.b * percent / total;
            }

            return new D2DColor(1f, (float)r, (float)g, (float)b);
        }

        public void Dispose()
        {
            LightMap?.Dispose();
        }
    }
}
