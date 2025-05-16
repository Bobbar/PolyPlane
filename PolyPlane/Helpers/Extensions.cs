using SkiaSharp;
using System.Numerics;
using unvell.D2DLib;

namespace PolyPlane.Helpers
{
    public static class Extensions
    {
        public static D2DPoint Add(this D2DPoint point, D2DPoint other)
        {
            return new D2DPoint(point.X + other.X, point.Y + other.Y);
        }

        public static D2DPoint Add(this D2DPoint point, float value)
        {
            return new D2DPoint(point.X + value, point.Y + value);
        }

        public static D2DPoint Subtract(this D2DPoint point, D2DPoint other)
        {
            return new D2DPoint(point.X - other.X, point.Y - other.Y);
        }

        public static D2DPoint Subtract(this D2DPoint point, float value)
        {
            return new D2DPoint(point.X - value, point.Y - value);
        }

        public static float NextFloat(this Random rnd, float min, float max)
        {
            return (float)rnd.NextDouble() * (max - min) + min;
        }

        public static D2DPoint Normalized(this D2DPoint vector)
        {
            // Avoid NaN for zero length vectors.
            if (vector.LengthSquared() == 0f)
                return D2DPoint.Zero;

            return D2DPoint.Normalize(vector);
        }

        public static D2DPoint Tangent(this D2DPoint point, bool clockwise = true)
        {
            if (clockwise)
                return new D2DPoint(point.Y, -point.X);
            else
                return new D2DPoint(-point.Y, point.X);
        }

        public static D2DPoint AbsDiff(this D2DPoint point, D2DPoint other)
        {
            return new D2DPoint(Math.Abs(point.X - other.X), Math.Abs(point.Y - other.Y));
        }

        public static D2DPoint ToD2DPoint(this Point pnt)
        {
            return new D2DPoint(pnt.X, pnt.Y);
        }

        public static D2DColor ToD2DColor(this Vector4 vec)
        {
            return new D2DColor(vec.X, vec.Y, vec.Z, vec.W);
        }

        public static Vector4 ToVector4(this D2DColor color)
        {
            return new Vector4(color.a, color.r, color.g, color.b);
        }

        public static float Angle(this D2DPoint vector, bool clamp = true)
        {
            var angle = (float)Math.Atan2(vector.Y, vector.X) * Utilities.RADS_TO_DEGREES;

            if (clamp)
                angle = Utilities.ClampAngle(angle);

            return angle;
        }

        public static float AngleRads(this D2DPoint vector, bool clamp = false)
        {
            var angle = (float)Math.Atan2(vector.Y, vector.X);

            return angle;
        }

        public static float Cross(this D2DPoint vector, D2DPoint other)
        {
            return Utilities.Cross(vector, other);
        }

        public static float AngleBetween(this D2DPoint vector, D2DPoint other, bool clamp = true)
        {
            return Utilities.AngleBetween(vector, other, clamp);
        }

        public static float AngleTo(this D2DPoint vector, D2DPoint other, bool clamp = false)
        {
            var dir = vector - other;
            var angle = dir.Angle();
            return angle;
        }

        public static float DistanceTo(this D2DPoint pos, D2DPoint other)
        {
            return D2DPoint.Distance(pos, other);
        }

        public static float DistanceSquaredTo(this D2DPoint pos, D2DPoint other)
        {
            return D2DPoint.DistanceSquared(pos, other);
        }

        public static bool Contains(this D2DRect rect, D2DPoint pnt)
        {
            return rect.Contains(pnt.X, pnt.Y);
        }

        public static bool Contains(this D2DRect rect, float x, float y)
        {
            return rect.X <= x &&
            x < rect.X + rect.Width &&
            rect.Y <= y &&
            y < rect.Y + rect.Height;
        }

        public static bool Contains(this D2DRect rect, D2DRect rect2)
        {
            if (rect.X <= rect2.X &&
            rect2.X + rect2.Width <= rect.X + rect.Width &&
            rect.Y <= rect2.Y &&
            rect2.Y + rect2.Height <= rect.Y + rect.Height)
                return true;

            // Check the 4 corners.
            if (rect.Contains(rect2.left, rect2.top))
                return true;

            if (rect.Contains(rect2.right, rect2.top))
                return true;

            if (rect.Contains(rect2.left, rect2.bottom))
                return true;

            if (rect.Contains(rect2.right, rect2.bottom))
                return true;

            return false;
        }

        public static bool Contains(this D2DRect rect, D2DPoint[] poly)
        {
            for (int i = 0; i < poly.Length; i++)
            {
                var idx1 = Utilities.WrapIndex(i, poly.Length);
                var idx2 = Utilities.WrapIndex(i + 1, poly.Length);

                var pnt1 = poly[idx1];
                var pnt2 = poly[idx2];

                if (rect.Contains(pnt1, pnt2))
                    return true;
            }

            return false;
        }

        public static bool Contains(this D2DRect rect, D2DPoint lineA, D2DPoint lineB)
        {
            return LineClipping.CohenSutherlandLineClip(lineA, lineB, rect);
        }

        public static bool Contains(this D2DRect rect, D2DEllipse ellipse)
        {
            return rect.Inflate(ellipse.radiusX * 2f, ellipse.radiusY * 2f).Contains(ellipse.origin);
        }

        public static bool Contains(this D2DRect rect, D2DPoint pos, float radius)
        {
            return rect.Inflate(radius, radius).Contains(pos);
        }

        public static D2DPoint AspectRatioFactor(this D2DRect rect)
        {
            var rH = rect.Height / rect.Width;
            var rW = rect.Width / rect.Height;

            var rMax = Math.Max(rH, rW);

            var ratio = new D2DPoint(1f, 1f);

            if (rect.Width > rect.Height)
                ratio.Y = rMax;
            else
                ratio.X = rMax;

            return ratio;
        }

        public static D2DRect Inflate(this D2DRect rect, float width, float height, bool keepAspectRatio = false)
        {
            if (keepAspectRatio)
            {
                var ratio = AspectRatioFactor(rect);
                var rWidth = width * ratio.X;
                var rHeight = height * ratio.Y;
                return new D2DRect(rect.left - (rWidth * 0.5f), rect.top - (rHeight * 0.5f), rect.Width + rWidth, rect.Height + rHeight);
            }
            else
            {
                return new D2DRect(rect.left - (width * 0.5f), rect.top - (height * 0.5f), rect.Width + width, rect.Height + height);
            }
        }

        public static D2DRect Deflate(this D2DRect rect, float width, float height)
        {
            return new D2DRect(rect.left + (width * 0.5f), rect.top + (height * 0.5f), rect.Width - width, rect.Height - height);
        }

        public static D2DPoint[] ToPoints(this D2DRect rect)
        {
            var points = new D2DPoint[4];
            points[0] = new D2DPoint(rect.left, rect.top);
            points[1] = new D2DPoint(rect.right, rect.top);
            points[2] = new D2DPoint(rect.right, rect.bottom);
            points[3] = new D2DPoint(rect.left, rect.bottom);
            return points;
        }

        public static D2DColor ToD2DColor(this Color color)
        {
            return D2DColor.FromGDIColor(color);
        }

        public static D2DColor WithAlpha(this D2DColor color, float alpha)
        {
            return new D2DColor(alpha, color);
        }

        public static D2DColor WithBrightness(this D2DColor color, float factor)
        {
            float r = Math.Clamp(color.r * factor, 0f, 1f);
            float g = Math.Clamp(color.g * factor, 0f, 1f);
            float b = Math.Clamp(color.b * factor, 0f, 1f);

            return new D2DColor(color.a, r, g, b);
        }

        public static SKPoint[] ToSkPoints(this D2DPoint[] points)
        {
            var skPoints = new SKPoint[points.Length];

            for (int i = 0; i < skPoints.Length; i++)
                skPoints[i] = points[i].ToSKPoint();

            return skPoints;
        }

        public static SKPoint ToSKPoint(this D2DPoint point)
        {
            return new SKPoint(point.X, point.Y);
        }

        public static SKColor ToSKColor(this D2DColor color)
        {
            return new SKColor((byte)(color.r * 255), (byte)(color.g * 255), (byte)(color.b * 255), (byte)(color.a * 255));
        }

        public static SKMatrix Add(this SKMatrix matrix, SKMatrix other)
        {
            return SKMatrix.Concat(matrix, other);
        }

        public static Vector4 ToVector4(this SKColor color)
        {
            return new Vector4(color.Alpha / 255f, color.Red / 255f, color.Green / 255f, color.Blue / 255f);
        }
     
        public static SKColor ToSKColor(this Vector4 color)
        {
            return new SKColor((byte)(255f * color.Y), (byte)(255f * color.Z), (byte)(255f * color.W), (byte)(255f * color.X));
        }

    }
}
