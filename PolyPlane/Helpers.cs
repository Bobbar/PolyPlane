using PolyPlane.GameObjects;
using System.Numerics;
using unvell.D2DLib;

namespace PolyPlane
{
    public static class Helpers
    {

        public static Random Rnd = new Random();
        public const float DEGREES_TO_RADS = (float)Math.PI / 180f;

        public static float Lerp(float value1, float value2, float amount)
        {
            return value1 + (value2 - value1) * amount;
        }
        public static float Lerp(float value1, float value2, float factor1, float factor2)
        {
            return value1 + (value2 - value1) * Factor(factor1, factor2);
        }



        public static float LerpAngle(float value1, float value2, float amount)
        {
            float delta = Repeat((value2 - value1), 360);
            if (delta > 180)
                delta -= 360;

            var ret = value1 + delta * Clamp01(amount);

            ret = ClampAngle(ret);

            return ret;
        }

        public static float Repeat(float t, float length)
        {
            return Clamp(t - (float)Math.Floor(t / length) * length, 0.0f, length);
        }

        public static float Clamp(float value, float min, float max)
        {
            if (value < min)
                value = min;
            else if (value > max)
                value = max;
            return value;
        }

        public static float Clamp01(float value)
        {
            if (value < 0F)
                return 0F;
            else if (value > 1F)
                return 1F;
            else
                return value;
        }

        public static D2DPoint LerpPoints(D2DPoint a, D2DPoint b, float amount)
        {
            var lerpX = Lerp(a.X, b.X, amount);
            var lerpY = Lerp(a.Y, b.Y, amount);

            return new D2DPoint(lerpX, lerpY);
        }

        public static float Factor(float value1, float value2)
        {
            return Math.Clamp(value1 / value2, 0f, 1f);
        }

        public static float RadsToDegrees(float rads)
        {
            return rads * (180f / (float)Math.PI);
        }

        public static float DegreesToRads(float degrees)
        {
            return degrees * ((float)Math.PI / 180f);
        }

        public static D2DPoint AngleToVectorRads(float angle)
        {
            var vec = new D2DPoint((float)Math.Cos(angle), (float)Math.Sin(angle));
            return vec;
        }

        public static D2DPoint AngleToVectorDegrees(float angle, float length = 1f)
        {
            //var rads = angle * ((float)Math.PI / 180f);
            var rads = angle * DEGREES_TO_RADS;

            var vec = new D2DPoint((float)Math.Cos(rads), (float)Math.Sin(rads));

            return vec * length;
        }

        public static D2DPoint AngleToVectorDegreesD(double angle)
        {
            var rads = angle * (Math.PI / 180d);
            var vec = new D2DPoint((float)Math.Cos(rads), (float)Math.Sin(rads));
            return vec;
        }

        public static float AngleBetween(D2DPoint vector, D2DPoint other, bool clamp = false)
        {
            var angA = vector.Angle(clamp);
            var angB = other.Angle(clamp);

            var angle = AngleDiff(angA, angB);

            return angle;
        }

        public static float AngleDiff(float a, float b)
        {
            var normDeg = ModSign((a - b), 360f);

            var absDiffDeg = Math.Min(360f - normDeg, normDeg);

            return absDiffDeg;
        }

        public static float AngleDiffSmallest(float a, float b)
        {
            return 180f - Math.Abs(Math.Abs(a - b) % (2f * 180f) - 180f);
        }

        public static double AngleDiffD(double a, double b)
        {
            var normDeg = ModSignD((a - b), 360d);

            var absDiffDeg = Math.Min(360d - normDeg, normDeg);

            return absDiffDeg;
        }

        public static float ModSign(float a, float n)
        {
            return a - (float)Math.Floor(a / n) * n;
        }

        public static double ModSignD(double a, double n)
        {
            return a - Math.Floor(a / n) * n;
        }

        public static float FMod(float a, float n)
        {
            return a % n;
        }

        public static float ClampAngle(float angle)
        {
            var ret = angle % 360f;

            if (ret < 0f)
                ret += 360f;

            return ret;
        }

        public static float ClampAngle180(float angle)
        {
            var ret = angle % 360f;

            ret = (ret + 360f) % 360f;

            if (ret > 180f)
                ret -= 360f;

            return ret;
        }

        public static double ClampAngleD(double angle)
        {
            var ret = angle % 360d;

            if (ret < 0d)
                ret += 360d;

            return ret;
        }

        public static float Cross(D2DPoint vector1, D2DPoint vector2)
        {
            return (vector1.X * vector2.Y) - (vector1.Y * vector2.X);
        }

        public static T CycleEnum<T>(T e) where T : struct, IConvertible
        {
            var t = e.GetType();
            var len = Enum.GetNames(t).Length;
            var vals = Enum.GetValues(t) as T[];

            var cur = (int)Convert.ChangeType(e, e.GetType());
            int next = cur;
            next = (next + 1) % len;

            return vals[next];
        }

        public static D2DPoint ApplyTranslation(D2DPoint src, float rotation, D2DPoint translation, float scale = 1f)
        {
            var mat = Matrix3x2.CreateScale(scale);
            mat *= Matrix3x2.CreateRotation(rotation * (float)(Math.PI / 180f), D2DPoint.Zero);
            mat *= Matrix3x2.CreateTranslation(translation);

            return D2DPoint.Transform(src, mat);
        }

        public static void ApplyTranslation(D2DPoint[] src, D2DPoint[] dst, float rotation, D2DPoint translation, float scale = 1f)
        {
            var mat = Matrix3x2.CreateScale(scale);
            mat *= Matrix3x2.CreateRotation(rotation * (float)(Math.PI / 180f), D2DPoint.Zero);
            mat *= Matrix3x2.CreateTranslation(translation);

            for (int i = 0; i < dst.Length; i++)
            {
                var transPnt = D2DPoint.Transform(src[i], mat);
                dst[i] = transPnt;
            }
        }

        public static D2DColor LerpColor(D2DColor color1, D2DColor color2, float amount)
        {
            var newColor = new D2DColor(
                color1.a + ((color2.a - color1.a) * amount),
                color1.r + ((color2.r - color1.r) * amount),
                color1.g + ((color2.g - color1.g) * amount),
                color1.b + ((color2.b - color1.b) * amount));

            return newColor;
        }

        public static bool IsPosInFOV(GameObject obj, D2DPoint pos, float fov)
        {
            var dir = pos - obj.Position;
            var angle = dir.Angle(true);
            var diff = Helpers.AngleDiff(obj.Rotation, angle);

            return diff <= (fov * 0.5f);
        }

        public static D2DPoint RandOPoint(float minMax)
        {
            return new D2DPoint(Rnd.NextFloat(-minMax, minMax), Rnd.NextFloat(-minMax, minMax));
        }

        public static D2DPoint RandOPoint(float minX, float maxX, float minY, float maxY)
        {
            return new D2DPoint(Rnd.NextFloat(minX, maxX), Rnd.NextFloat(minY, maxY));
        }

        public static D2DPoint RandOPointInPoly(D2DPoint[] poly)
        {
            var max = new D2DPoint(float.MinValue, float.MinValue);
            var min = new D2DPoint(float.MaxValue, float.MaxValue);

            foreach (var pnt in poly)
            {
                max.X = Math.Max(max.X, pnt.X);
                max.Y = Math.Max(max.Y, pnt.Y);

                min.X = Math.Min(min.X, pnt.X);
                min.Y = Math.Min(min.Y, pnt.Y);
            }

            var rndPnt = RandOPoint(min.X, max.X, min.Y, max.Y);

            while (!PointInPoly(rndPnt, poly))
                rndPnt = RandOPoint(min.X, max.X, min.Y, max.Y);

            return rndPnt;
        }

        public static bool PointInPoly(D2DPoint pnt, D2DPoint[] poly)
        {
            int i, j = 0;
            bool c = false;
            for (i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if (((poly[i].Y > pnt.Y) != (poly[j].Y > pnt.Y)) && (pnt.X < (poly[j].X - poly[i].X) * (pnt.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X))
                    c = !c;
            }

            return c;
        }

        public static float ImpactTime(GameObjects.Plane plane, Missile missile)
        {
            var dist = plane.Position.DistanceTo(missile.Position);
            var navTime = ImpactTime(dist, (plane.Velocity.Length() + missile.Velocity.Length()), 1f);
            return navTime;
        }

        public static float ImpactTime(GameObjects.Plane plane, D2DPoint pos)
        {
            var dist = plane.Position.DistanceTo(pos);
            var navTime = ImpactTime(dist, (plane.Velocity.Length()), 1f);
            return navTime;
        }

        public static float ImpactTime(float dist, float velo, float accel)
        {
            var finalVelo = (float)Math.Sqrt((Math.Pow(velo, 2f) + 2f * accel * dist));

            return (finalVelo - velo) / accel;
        }

        public static bool IsPointingRight(float angle)
        {
            var rot180 = ClampAngle180(angle);
            if (rot180 > -90f && rot180 < 0f || rot180 < 90f && rot180 > 0f)
                return true;
            else if (rot180 > 90f && rot180 < 180f || rot180 > -180f && rot180 < -90f)
                return false;

            return false;
        }

    }
}
