﻿using System.Numerics;
using unvell.D2DLib;

namespace PolyPlane.Helpers
{
    public static class Extensions
    {
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

        public static D2DPoint ToD2DPoint(this Point pnt)
        {
            return new D2DPoint(pnt.X, pnt.Y);
        }

        public static D2DColor ToD2DColor(this Vector4 vec)
        {
            return new D2DColor(vec.X, vec.Y, vec.Z, vec.W);
        }

        public static Color ToGDIColor(this D2DColor color)
        {
            return Color.FromArgb((int)(255f * color.a), (int)(255f * color.r), (int)(255f * color.g), (int)(255f * color.b));
        }

        public static Vector4 ToVector4(this D2DColor color)
        {
            return new Vector4(color.a, color.r, color.g, color.b);
        }

        public static float Angle(this D2DPoint vector, bool clamp = true)
        {
            var angle = MathF.Atan2(vector.Y, vector.X) * Utilities.RADS_TO_DEGREES;

            if (clamp)
                angle = Utilities.ClampAngle(angle);

            return angle;
        }

        public static float AngleRads(this D2DPoint vector, bool clamp = false)
        {
            var angle = MathF.Atan2(vector.Y, vector.X);

            return angle;
        }

        public static float Cross(this D2DPoint vector, D2DPoint other) => Utilities.Cross(vector, other);

        public static float AngleBetween(this D2DPoint vector, D2DPoint other, bool clamp = true) => Utilities.AngleBetween(vector, other, clamp);

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
            if (rect.left > rect2.right || rect2.left > rect.right)
                return false;

            if (rect.top > rect2.bottom || rect2.top > rect.bottom)
                return false;

            return true;
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

        public static bool Contains(this D2DEllipse ellipse, float ellipseRotation, D2DPoint pos)
        {
            var mat = Matrix3x2.CreateRotation(-ellipseRotation * (MathF.PI / 180f), ellipse.origin);
            var transPos = D2DPoint.Transform(pos, mat);

            var p = MathF.Pow(transPos.X - ellipse.origin.X, 2f) / MathF.Pow(ellipse.radiusX, 2f) + MathF.Pow(transPos.Y - ellipse.origin.Y, 2f) / MathF.Pow(ellipse.radiusY, 2f);

            return p <= 1f;
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

        public static D2DColor ToD2DColor(this Color color)
        {
            return D2DColor.FromGDIColor(color);
        }

        public static D2DColor WithAlpha(this D2DColor color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        public static D2DColor WithBrightness(this D2DColor color, float factor)
        {
            color.r = Math.Clamp(color.r * factor, 0f, 1f);
            color.g = Math.Clamp(color.g * factor, 0f, 1f);
            color.b = Math.Clamp(color.b * factor, 0f, 1f);

            return color;
        }
    }
}
