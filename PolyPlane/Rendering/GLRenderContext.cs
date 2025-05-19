using PolyPlane.GameObjects.Tools;
using PolyPlane.Helpers;
using SkiaSharp;
using System.Numerics;
using unvell.D2DLib;

namespace PolyPlane.Rendering
{
    public sealed class GLRenderContext
    {
        public SKCanvas Gfx => _gfx;


        public D2DRect Viewport;

        public float CurrentScale
        {
            get
            {
                return _currentScale;
            }
        }

        public readonly LightMap LightMap;
        private float _currentLightingFactor = 1f;


        private const double GaussianSigma2 = 0.035;
        private readonly double GaussianSigma = Math.Sqrt(2.0 * Math.PI * GaussianSigma2);


        private SKCanvas _gfx;

        private Stack<D2DRect> _vpStack = new Stack<D2DRect>();

        private Stack<SKMatrix> _matrixStack = new Stack<SKMatrix>();

        private float _currentScale = 1.0f;
        private SKPaint _fillPaint = new SKPaint() { Color = SKColors.Transparent, IsAntialias = true };
        private SKPaint _strokedPaint = new SKPaint() { Color = SKColors.Transparent, IsAntialias = true, IsStroke = true };

        private SKColor _timeOfDayColor = SKColors.Transparent;


        private bool _enableRender = true;

        public GLRenderContext()
        {
            LightMap = new LightMap();

        }

        private SKPaint GetFillPaint(SKColor color)
        {
            _fillPaint.Color = color;

            return _fillPaint;
        }

        private SKPaint GetStrokedPaint(SKColor color, float strokeWeight, SKStrokeCap strokeCap = SKStrokeCap.Butt)
        {
            if (!_strokedPaint.IsStroke)
            {

            }

            _strokedPaint.Color = color;
            _strokedPaint.StrokeWidth = strokeWeight;
            _strokedPaint.StrokeCap = strokeCap;

            return _strokedPaint;
        }

        public void SetCanvas(SKCanvas canvas)
        {
            _gfx = canvas;
        }


        public void BeginRender(SKColor clearColor)
        {
            _gfx.Clear(clearColor);

            UpdateTimeOfDayColors();
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

     
        public void SetClip(SKPath path, SKClipOperation operation = SKClipOperation.Intersect, bool antialias = false)
        {
            _gfx.ClipPath(path, operation, antialias);
        }

        private void UpdateScale()
        {
            var trans = _gfx.TotalMatrix;
            var scaleX = (float)Math.Sqrt(trans.ScaleX * trans.ScaleX + trans.ScaleY * trans.ScaleY);
            _currentScale = scaleX;
        }

        public void PushTransform()
        {
            var mat = _gfx.TotalMatrix;

            _matrixStack.Push(mat);

            //_gfx.SetMatrix(SKMatrix.Identity);

            UpdateScale();
        }

        public void PopTransform()
        {
            _gfx.SetMatrix(_matrixStack.Pop());

            UpdateScale();
        }

        public void TranslateTransform(Vector2 pos)
        {
            _gfx.SetMatrix(_gfx.TotalMatrix.Add(SKMatrix.CreateTranslation(pos.X, pos.Y)));

            UpdateScale();
        }

        public void ScaleTransform(float xy)
        {
            _gfx.SetMatrix(_gfx.TotalMatrix.Add(SKMatrix.CreateScale(xy, xy)));

            UpdateScale();
        }

        public void ScaleTransform(float xy, Vector2 center)
        {
            _gfx.SetMatrix(_gfx.TotalMatrix.Add(SKMatrix.CreateScale(xy, xy, center.X, center.Y)));

            UpdateScale();
        }

        public void ScaleTransform(float x, float y, Vector2 center)
        {
            _gfx.SetMatrix(_gfx.TotalMatrix.Add(SKMatrix.CreateScale(x, y, center.X, center.Y)));

            UpdateScale();
        }

        public void RotateTransform(float angleDegrees)
        {
            _gfx.SetMatrix(_gfx.TotalMatrix.Add(SKMatrix.CreateRotationDegrees(angleDegrees)));

            UpdateScale();
        }

        public void RotateTransform(float angleDegrees, Vector2 center)
        {
            _gfx.SetMatrix(_gfx.TotalMatrix.Add(SKMatrix.CreateRotationDegrees(angleDegrees, center.X, center.Y)));

            UpdateScale();
        }

        public ContextState GetTemporaryState()
        {
            var state = new ContextState(_gfx);

            return state;
        }


        private void UpdateTimeOfDayColors()
        {
            // Compute a TimeOfDay factor to be applied to all lighting intensity.
            // Decrease lighting intensity during the day.
            var factor = Math.Clamp(Utilities.FactorWithEasing(World.TimeOfDay, World.MAX_TIMEOFDAY - 5, EasingFunctions.EaseLinear), 0.5f, 1f);
            _currentLightingFactor = factor;

            var todColor = InterpolateColorGaussian(World.TimeOfDayPalletGL, World.TimeOfDay, World.MAX_TIMEOFDAY);
            _timeOfDayColor = todColor;
        }

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
        /// Gets the shadow color for the current time of day.  (A darker variation of the time of day color)
        /// </summary>
        /// <returns></returns>
        public SKColor GetShadowColor()
        {
            var shadowColor = Utilities.LerpColorWithAlpha(GetTimeOfDayColor(), SKColors.Black, 0.7f, 102);
            return shadowColor;
        }


        /// <summary>
        /// Get the color for the current time of day from the time of day pallet.
        /// </summary>
        /// <returns></returns>
        public SKColor GetTimeOfDayColor()
        {
            return _timeOfDayColor;
        }


        /// <summary>
        /// Adds the current time of day color to the specified color.
        /// </summary>
        /// <param name="color"></param>
        /// <returns></returns>

        public SKColor AddTimeOfDayColor(SKColor color)
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


        public SKColor AddTimeOfDayColor(SKColor color, SKColor todColor)
        {
            const float AMT = 0.35f;
            return Utilities.LerpColor(color, todColor, AMT);
        }

        public void DrawText(string text, Vector2 pos, SKTextAlign align, SKFont font, SKColor color)
        {
            var paint = GetFillPaint(color);

            DrawText(text, pos, align, font, paint);
        }

        public void DrawTextMultiLine(string text, Vector2 pos, SKTextAlign align, SKFont font, SKColor color)
        {
            var paint = GetFillPaint(color);
            var lines = text.Split('\n');

            foreach (var line in lines)
            {
                DrawText(line, pos, align, font, paint);
            }
        }

        public void DrawTextMultiLine(string text, Vector2 pos, SKTextAlign align, SKFont font, SKPaint paint)
        {
            var lines = text.Split('\n');
            var linePos = pos;

            foreach (var line in lines)
            {
                DrawText(line, linePos, align, font, paint);

                linePos += new Vector2(0f, font.Size + 1);
            }
        }


        public void DrawText(string text, Vector2 pos, SKTextAlign align, SKFont font, SKPaint paint)
        {
            _gfx.DrawText(text, pos, align, font, paint);
        }


        public void DrawRectangle(SKRect rect, SKColor color, float strokeWeight = 1f)
        {
            var paint = GetStrokedPaint(color, strokeWeight);

            DrawRectangle(rect, paint);
        }

        public void FillRectangle(SKRect rect, SKColor color)
        {
            var paint = GetFillPaint(color);

            DrawRectangle(rect, paint);
        }

        public void DrawRectangle(SKRect rect, SKPaint paint)
        {
            if (_enableRender)
                _gfx.DrawRect(rect, paint);
        }

        public void FillPolygon(RenderPoly poly, SKColor color)
        {
            FillPolygon(poly.Poly, color);
        }

        public void FillPolygon(Vector2[] poly, SKColor color)
        {
            var paint = GetFillPaint(color);

            FillPolygon(poly, paint);
        }

        public void FillPolygon(Vector2[] poly, SKPaint paint)
        {
            using (var path = new SKPath())
            {
                path.AddPoly(poly.ToSkPoints(), true);

                DrawPath(path, paint);
            }
        }


        public void FillPathWithLighting(SKPath path, Vector2 sampleLocation, SKColor color, SKColor strokeColor, float strokeWeight, float maxIntensity)
        {
            if (World.UseLightMap)
            {
                var lightedColor = LightMap.SampleColorSK(sampleLocation, color, 0f, maxIntensity * _currentLightingFactor);
                FillPath(path, lightedColor, strokeColor, strokeWeight);
            }
            else
            {
                FillPath(path, color, strokeColor, strokeWeight);
            }
        }

        public void FillPolygonWithLighting(RenderPoly poly, Vector2 sampleLocation, SKColor color, SKColor strokeColor, float strokeWeight, float maxIntensity)
        {
            if (World.UseLightMap)
            {
                var lightedColor = LightMap.SampleColorSK(sampleLocation, color, 0f, maxIntensity * _currentLightingFactor);
                FillPolygon(poly, lightedColor, strokeColor, strokeWeight);
            }
            else
            {
                FillPolygon(poly, color, strokeColor, strokeWeight);
            }
        }

        public void FillPolygonWithLighting(Vector2[] poly, Vector2 sampleLocation, SKColor color, float maxIntensity)
        {
            if (World.UseLightMap)
            {
                var lightedColor = LightMap.SampleColorSK(sampleLocation, color, 0f, maxIntensity * _currentLightingFactor);
                FillPolygon(poly, lightedColor);
            }
            else
            {
                FillPolygon(poly, color);
            }
        }

        public void FillPolygon(RenderPoly poly, SKColor color, SKColor strokeColor, float strokeWeight)
        {
            using (var path = new SKPath())
            {
                path.AddPoly(poly.Poly.ToSkPoints(), true);

                FillPath(path, color, strokeColor, strokeWeight);
            }
        }

        public void FillPath(SKPath path, SKColor color, SKColor strokeColor, float strokeWeight)
        {
            var paint = GetFillPaint(color);

            DrawPath(path, paint);

            paint = GetStrokedPaint(strokeColor, strokeWeight);

            DrawPath(path, paint);
        }

        public void DrawPath(SKPath path, SKPaint paint)
        {
            if (_enableRender)
                _gfx.DrawPath(path, paint);
        }


        public void DrawLineWithLighting(Vector2 p0, Vector2 p1, SKColor color, float maxIntensity, float weight)
        {
            if (World.UseLightMap)
            {
                var lightedColor = LightMap.SampleColorSK((p0 + p1) * 0.5f, color, 0f, maxIntensity * _currentLightingFactor);
                DrawLine(p0, p1, lightedColor, weight);
            }
            else
            {
                DrawLine(p0, p1, color, weight);
            }
        }

        public void DrawLineWithLighting(Vector2 p0, Vector2 p1, SKColor color, float maxIntensity, float weight, SKStrokeCap capStyle = SKStrokeCap.Square)
        {
            if (World.UseLightMap)
            {
                var lightedColor = LightMap.SampleColorSK((p0 + p1) * 0.5f, color, 0f, maxIntensity * _currentLightingFactor);
                DrawLine(p0, p1, lightedColor, weight, capStyle);
            }
            else
            {
                DrawLine(p0, p1, color, weight, capStyle);
            }
        }


        public void DrawLine(Vector2 p0, Vector2 p1, SKColor color, float weight)
        {
            var paint = GetStrokedPaint(color, weight);

            DrawLine(p0, p1, paint);
        }

        public void DrawLine(Vector2 p0, Vector2 p1, SKColor color, float weight, SKStrokeCap capStyle = SKStrokeCap.Butt)
        {
            var paint = GetStrokedPaint(color, weight, capStyle);

            DrawLine(p0, p1, paint);
        }

        public void DrawLine(Vector2 p0, Vector2 p1, SKPaint paint)
        {
            if (_enableRender)
                _gfx.DrawLine(p0, p1, paint);
        }

        public void FillCircle(Vector2 pos, float radius, SKColor color)
        {
            var paint = GetFillPaint(color);
            var scale = this.CurrentScale;
            var viewRad = radius * scale;

            if (World.FastPrimitives && viewRad > World.FAST_PRIMITIVE_MIN_SIZE || !World.FastPrimitives)
            {
                FillCircle(pos, radius, paint);
            }
            else
            {
                var r = SKRect.Create(pos.X - radius, pos.Y - radius, radius * 2f, radius * 2f);
                DrawRectangle(r, paint);
            }
        }

        public void DrawCircle(Vector2 pos, float radius, SKColor color)
        {
            var paint = GetFillPaint(color);

            FillCircle(pos, radius, paint);
        }

        public void DrawCircle(Vector2 pos, float radius, SKColor color, float strokeWidth)
        {
            var paint = GetStrokedPaint(color, strokeWidth);

            FillCircle(pos, radius, paint);
        }


        public void FillCircle(Vector2 pos, float radius, SKPaint paint)
        {
            if (_enableRender)
                _gfx.DrawCircle(pos, radius, paint);
        }

        public void FillCircleWithLighting(Vector2 pos, float radius, SKColor color, float maxIntensity)
        {
            FillCircleWithLighting(pos, radius, color, 0f, maxIntensity);
        }

        public void FillCircleWithLighting(Vector2 pos, float radius, Vector2 sampleLocation, SKColor color, float minIntensity, float maxIntensity)
        {
            if (World.UseLightMap)
            {
                var lightedColor = LightMap.SampleColorSK(sampleLocation, color, minIntensity, maxIntensity * _currentLightingFactor);
                FillCircle(pos, radius, lightedColor);

            }
            else
            {
                FillCircle(pos, radius, color);
            }

        }

        public void FillCircleWithLighting(Vector2 pos, float radius, SKColor color, float minIntensity, float maxIntensity)
        {
            if (World.UseLightMap)
            {
                var lightedColor = LightMap.SampleColorSK(pos, color, minIntensity, maxIntensity * _currentLightingFactor);
                FillCircle(pos, radius, lightedColor);

            }
            else
            {
                FillCircle(pos, radius, color);
            }

        }

        public void FillEllipse(Vector2 pos, SKSize size, SKColor color)
        {
            var paint = GetFillPaint(color);

            FillEllipse(pos, size, paint);
        }

        public void FillEllipse(Vector2 pos, SKSize size, SKPaint paint)
        {
            if (_enableRender)
                _gfx.DrawOval(pos, size, paint);
        }


        public void FillEllipseWithLighting(Vector2 pos, SKSize size, SKColor color, float maxIntensity)
        {
            if (World.UseLightMap)
            {
                var lightedColor = LightMap.SampleColorSK(pos, color, 0f, maxIntensity * _currentLightingFactor);
                FillEllipse(pos, size, lightedColor);

            }
            else
            {
                FillEllipse(pos, size, color);
            }
        }

        public void FillEllipseWithLighting(Vector2 pos, SKSize size, Vector2 sampleLocation, SKColor color, float maxIntensity)
        {
            if (World.UseLightMap)
            {
                var lightedColor = LightMap.SampleColorSK(sampleLocation, color, 0f, maxIntensity * _currentLightingFactor);
                FillEllipse(pos, size, lightedColor);

            }
            else
            {
                FillEllipse(pos, size, color);
            }
        }

        public void FillEllipseWithLighting(Vector2 pos, SKSize size, SKColor color, float minIntensity, float maxIntensity)
        {
            if (World.UseLightMap)
            {
                var lightedColor = LightMap.SampleColorSK(pos, color, minIntensity, maxIntensity * _currentLightingFactor);
                FillEllipse(pos, size, lightedColor);

            }
            else
            {
                FillEllipse(pos, size, color);
            }
        }

        public void DrawEllipse(Vector2 pos, SKSize size, SKColor color, float strokeWidth)
        {
            var paint = GetStrokedPaint(color, strokeWidth);

            FillEllipse(pos, size, paint);
        }

        public void DrawProgressBar(Vector2 pos, SKSize size, SKColor boderColor, SKColor fillColor, float percent)
        {
            FillRectangle(SKRect.Create(pos.X - size.Width * 0.5f, pos.Y, size.Width * percent, size.Height), fillColor);
            DrawRectangle(SKRect.Create(pos - new Vector2(size.Width * 0.5f, 0f), size), boderColor);
        }


        private SKColor InterpolateColorGaussian(SKColor[] colors, float value, float maxValue)
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

                r += color.Red * percent / total;
                g += color.Green * percent / total;
                b += color.Blue * percent / total;
            }

            return new SKColor((byte)r, (byte)g, (byte)b, 255);
        }



    }

    public struct ContextState : IDisposable
    {
        private SKCanvas _canvas;

        public ContextState(SKCanvas canvas)
        {
            _canvas = canvas;
            _canvas.Save();
        }

        public void Dispose()
        {
            _canvas.Restore();
        }
    }
}
