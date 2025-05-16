using PolyPlane.GameObjects.Tools;
using PolyPlane.Helpers;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        private SKPaint _cachedPaint = new SKPaint() { Color = SKColors.Transparent, IsAntialias = true };


        public GLRenderContext()
        {
            LightMap = new LightMap();

        }


        public void SetCanvas(SKCanvas canvas)
        {
            _gfx = canvas;
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

        public void TranslateTransform(SKPoint pos)
        {
            _gfx.SetMatrix(_gfx.TotalMatrix.Add(SKMatrix.CreateTranslation(pos.X, pos.Y)));

            UpdateScale();
        }

        public void ScaleTransform(float xy)
        {
            _gfx.SetMatrix(_gfx.TotalMatrix.Add(SKMatrix.CreateScale(xy, xy)));

            UpdateScale();
        }

        public void ScaleTransform(float xy, SKPoint center)
        {
            _gfx.SetMatrix(_gfx.TotalMatrix.Add(SKMatrix.CreateScale(xy, xy, center.X, center.Y)));

            UpdateScale();
        }




        private void UpdateTimeOfDayLightFactor()
        {
            // Compute a TimeOfDay factor to be applied to all lighting intensity.
            // Decrease lighting intensity during the day.
            var factor = Math.Clamp(Utilities.FactorWithEasing(World.TimeOfDay, World.MAX_TIMEOFDAY - 5, EasingFunctions.EaseLinear), 0.5f, 1f);
            _currentLightingFactor = factor;
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
        //public D2DColor GetTimeOfDayColor()
        //{
        //    var todColor = InterpolateColorGaussian(World.TimeOfDayPallet, World.TimeOfDay, World.MAX_TIMEOFDAY);
        //    return todColor;
        //}

        public SKColor GetTimeOfDayColor()
        {
            var todColor = InterpolateColorGaussian(World.TimeOfDayPalletGL, World.TimeOfDay, World.MAX_TIMEOFDAY);
            return todColor;
        }


        /// <summary>
        /// Adds the current time of day color to the specified color.
        /// </summary>
        /// <param name="color"></param>
        /// <returns></returns>
        //public D2DColor AddTimeOfDayColor(D2DColor color)
        //{
        //    var todColor = GetTimeOfDayColor();
        //    return AddTimeOfDayColor(color, todColor);
        //}

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



        public void FillPolygon(RenderPoly poly, SKColor color)
        {
            using (var path = new SKPath())
            //using (var paint = new SKPaint() { Color = color, IsAntialias = true })
            {
                _cachedPaint.Color = color;
                var paint = _cachedPaint;

                path.AddPoly(poly.Poly.ToSkPoints(), true);

                _gfx.DrawPath(path, paint);
            }
        }

        public void DrawLine(SKPoint p0, SKPoint p1, SKColor color, float weight)
        {
            //using (var paint = new SKPaint() { Color = color, IsAntialias = true, StrokeWidth = weight })
            {

                _cachedPaint.Color = color;
                var paint = _cachedPaint;

                _gfx.DrawLine(p0, p1, paint);
            }

           
        }


        public void DrawRectangle(SKRect rect, SKPaint paint)
        {
            _gfx.DrawRect(rect, paint);
        }

        public void DrawRectangle(SKRect rect, SKColor color)
        {
            //using (var paint = new SKPaint() { Color = color, IsAntialias = true })
            {

                _cachedPaint.Color = color;
                var paint = _cachedPaint;


                _gfx.DrawRect(rect, paint);
            }
        }


        public void DrawCircle(SKPoint pos, float radius, SKColor color)
        {
            //using (var paint = new SKPaint() { Color = color, IsAntialias = true })
            {

                _cachedPaint.Color = color;
                var paint = _cachedPaint;

                DrawCircle(pos, radius, paint);
            }
        }

        public void DrawCircle(SKPoint pos, float radius, SKPaint paint)
        {
            _gfx.DrawCircle(pos, radius, paint);
        }

        public void DrawCircleWithLighting(SKPoint pos, float radius, SKColor color, float maxIntensity)
        {
            DrawCircleWithLighting(pos, radius, color, 0f, maxIntensity);
        }


        public void DrawCircleWithLighting(SKPoint pos, float radius, SKColor color, float minIntensity, float maxIntensity)
        {
            if (World.UseLightMap)
            {
                var lightedColor = LightMap.SampleColorSK(pos, color, minIntensity, maxIntensity * _currentLightingFactor);
                DrawCircle(pos, radius, lightedColor);

            }
            else
            {
                DrawCircle(pos, radius, color);
            }

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
    }
}
