using unvell.D2DLib;

namespace PolyPlane
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

        public static D2DPoint Normalized(this D2DPoint point)
        {
            return D2DPoint.Normalize(point);
        }

        public static D2DPoint AbsDiff(this D2DPoint point, D2DPoint other)
        {
            return new D2DPoint(Math.Abs(point.X - other.X), Math.Abs(point.Y - other.Y));
        }

        public static float Angle(this D2DPoint vector, bool clamp = false)
        {
            var angle = (float)Math.Atan2(vector.Y, vector.X) * (180f / (float)Math.PI);

            if (clamp)
                angle = Helpers.ClampAngle(angle);

            return angle;
        }

        public static float AngleRads(this D2DPoint vector, bool clamp = false)
        {
            var angle = (float)Math.Atan2(vector.Y, vector.X);

            if (clamp)
                angle = Helpers.ClampAngle(angle);

            return angle;
        }

        public static double AngleD(this D2DPoint vector, bool clamp = false)
        {
            var angle = Math.Atan2(vector.Y, vector.X) * (180d / Math.PI);

            if (clamp)
                angle = Helpers.ClampAngleD(angle);

            return angle;
        }

        public static float Cross(this D2DPoint vector, D2DPoint other)
        {
            return Helpers.Cross(vector, other);
        }

        public static float AngleBetween(this D2DPoint vector, D2DPoint other, bool clamp = false)
        {
            return Helpers.AngleBetween(vector, other, clamp);
        }

        public static float AngleTo(this D2DPoint vector, D2DPoint other, bool clamp = false)
        {
            var dir = other - vector;
            var angle = dir.Angle(true);
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
            return rect.X <= pnt.X &&
            pnt.X < rect.X + rect.Width &&
            rect.Y <= pnt.Y &&
            pnt.Y < rect.Y + rect.Height;
        }

        public static bool Contains(this D2DRect rect, D2DRect rect2)
        {
            return (rect.X <= rect2.X) &&
            ((rect2.X + rect2.Width) <= (rect.X + rect.Width)) &&
            (rect.Y <= rect2.Y) &&
            ((rect2.Y + rect2.Height) <= (rect.Y + rect.Height));
        }

        public static bool Contains(this D2DRect rect, D2DPoint[] poly)
        {
            foreach (D2DPoint p in poly)
            {
                if (rect.Contains(p))
                    return true;
            }

            return false;
        }

        public static bool Contains(this D2DRect rect, D2DEllipse ellipse)
        {
            return rect.Contains(ellipse.origin);
        }

        public static D2DRect Inflate(this D2DRect rect, float width, float height)
        {
            return new D2DRect(rect.left - width, rect.top - height, rect.Width + 2f * width, rect.Height + 2f * height);
        }

        public static T Shift<T>(this List<T> list)
        {
            if (list.Count == 0)
                return default(T);

            T item = list[0];
            list.RemoveAt(0);
            return item;
        }

    }
}
