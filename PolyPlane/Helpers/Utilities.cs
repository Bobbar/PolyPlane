using PolyPlane.AI_Behavior;
using PolyPlane.GameObjects;
using PolyPlane.GameObjects.Fixtures;
using PolyPlane.GameObjects.Managers;
using System.Net;
using System.Numerics;
using System.Runtime.CompilerServices;
using unvell.D2DLib;

namespace PolyPlane.Helpers
{
    public static class Utilities
    {
        [ThreadStatic]
        private static Random _rnd;

        public static Random Rnd
        {
            get
            {
                if (_rnd == null)
                    _rnd = new Random();

                return _rnd;
            }
        }

        public const float DEGREES_TO_RADS = (float)(Math.PI / 180d);
        public const float RADS_TO_DEGREES = (float)(180d / Math.PI);

        public static float Damp(float a, float b, float lambda, float dt)
        {
            return Lerp(a, b, 1f - MathF.Exp(-lambda * dt));
        }

        public static float Lerp(float value1, float value2, float amount)
        {
            return value1 + (value2 - value1) * amount;
        }

        public static float LerpAngle(float value1, float value2, float amount)
        {
            float delta = Repeat(value2 - value1, 360f);
            if (delta > 180f)
                delta -= 360f;

            var ret = value1 + delta * Clamp01(amount);

            ret = ClampAngle(ret);

            return ret;
        }

        /// <summary>
        /// Scales the specified value to the specified new range while maintaining ratio.
        /// </summary>
        /// <param name="value">Original value.</param>
        /// <param name="oldMin">Old range min.</param>
        /// <param name="oldMax">Old range max.</param>
        /// <param name="newMin">New range min.</param>
        /// <param name="newMax">New range max.</param>
        /// <returns></returns>
        public static float ScaleToRange(float value, float oldMin, float oldMax, float newMin, float newMax)
        {
            var newVal = (((value - oldMin) * (newMax - newMin)) / (oldMax - oldMin)) + newMin;
            newVal = Math.Clamp(newVal, newMin, newMax);

            return newVal;
        }

        public static float Repeat(float t, float length)
        {
            return Math.Clamp(t - MathF.Floor(t / length) * length, 0.0f, length);
        }

        public static float Clamp01(float value)
        {
            return Math.Clamp(value, 0f, 1f);
        }

        public static D2DPoint LerpPoints(D2DPoint a, D2DPoint b, float amount)
        {
            return D2DPoint.Lerp(a, b, amount);
        }

        public static float Factor(float value, float maxValue)
        {
            return Math.Clamp(value / maxValue, 0f, 1f);
        }

        public static float FactorWithEasing(float value1, float value2, Func<float, float> easeFunc)
        {
            return easeFunc(Math.Clamp(value1 / value2, 0f, 1f));
        }

        public static float Factor(float value, float min, float max)
        {
            return Math.Clamp((value - min) / (max - min), 0f, 1f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float RadsToDegrees(float rads)
        {
            return rads * RADS_TO_DEGREES;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DegreesToRads(float degrees)
        {
            return degrees * DEGREES_TO_RADS;
        }

        public static D2DPoint AngleToVectorDegrees(float angle, float length = 1f)
        {
            var rads = DegreesToRads(angle);
            var vec = new D2DPoint(MathF.Cos(rads), MathF.Sin(rads));
            return vec * length;
        }

        /// <summary>
        /// Computes a quadratic bezier point from the specified control points and position <paramref name="t"/>.
        /// </summary>
        /// <param name="p0"></param>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <param name="t"></param>
        /// <returns></returns>
        // See: https://en.wikipedia.org/wiki/B%C3%A9zier_curve#Quadratic_B%C3%A9zier_curves
        public static D2DPoint LerpBezierCurve(D2DPoint p0, D2DPoint p1, D2DPoint p2, float t)
        {
            return ((1f - t) * (1f - t) * p0) + (2f * t * (1f - t) * p1) + (t * t * p2);
        }

        public static float AngleBetween(D2DPoint vector, D2DPoint other, bool clamp = true)
        {
            var angA = vector.Angle(clamp);
            var angB = other.Angle(clamp);

            var angle = AngleDiff(angA, angB);

            return angle;
        }

        public static float AngleDiff(float a, float b)
        {
            return 180f - Math.Abs(Math.Abs(a - b) % (2f * 180f) - 180f);
        }

        public static float ClampAngle(float angle)
        {
            if (angle >= 0f && angle <= 360f)
                return angle;

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

        public static float ReverseAngle(float angle)
        {
            return ClampAngle((angle + 180f) % 360f);
        }

        public static float TangentAngle(float angle)
        {
            return ClampAngle(angle + 90f);
        }

        public static float MoveTowardsAngle(float from, float to, float rate)
        {
            if (to == from)
                return to;

            var diff = ClampAngle180(to - from);
            var sign = Math.Sign(diff);
            var amt = rate * sign;

            if (Math.Abs(amt) > Math.Abs(diff))
                amt = diff;

            return ClampAngle(from + amt);
        }

        public static D2DPoint IntersectionPoint(D2DPoint line1A, D2DPoint line1B, D2DPoint line2A, D2DPoint line2B)
        {
            var A1 = line1B.Y - line1A.Y;
            var B1 = line1A.X - line1B.X;
            var C1 = A1 * line1A.X + B1 * line1A.Y;

            var A2 = line2B.Y - line2A.Y;
            var B2 = line2A.X - line2B.X;
            var C2 = A2 * line2A.X + B2 * line2A.Y;

            var delta = A1 * B2 - A2 + B1;

            if (Math.Abs(delta) <= float.Epsilon)
                return D2DPoint.Zero;

            var x = (B2 * C1 - B1 * C2) / delta;
            var y = (A1 * C2 - A2 * C1) / delta;

            return new D2DPoint(x, y);
        }

        public static D2DPoint GroundIntersectionPoint(GameObject obj, float angle)
        {
            const float GROUND_LINE_LEN = 50000f;
            const float Y_OFFSET = 15f;

            var groundLineA = new D2DPoint(obj.Position.X - GROUND_LINE_LEN, 0f);
            var groundLineB = new D2DPoint(obj.Position.X + GROUND_LINE_LEN, 0f);

            var intersectVector = obj.Position + AngleToVectorDegrees(angle, obj.Altitude + Y_OFFSET);
            var groundPos = IntersectionPoint(obj.Position, intersectVector, groundLineA, groundLineB);

            return groundPos;
        }

        /// <summary>
        /// Checks for ground intersections with the previous and next positions of the specified object.
        /// </summary>
        /// <param name="obj">Object to test.</param>
        /// <param name="altOffset">Ground level Y coordinate offset. (Move the intersection test up or down)</param>
        /// <param name="dt">Current time delta.</param>
        /// <param name="impactPnt">Ground intersection point.</param>
        /// <returns>True if a ground intersection was found.</returns>
        public static bool TryGetGroundCollisionPoint(GameObject obj, float altOffset, float dt, out D2DPoint impactPnt)
        {
            const float GROUND_LINE_LEN = 50000f;

            var groundLineA = new D2DPoint(obj.Position.X - GROUND_LINE_LEN, altOffset);
            var groundLineB = new D2DPoint(obj.Position.X + GROUND_LINE_LEN, altOffset);

            // Intersect current and previous positions.
            var intersectVector = obj.Position + (obj.Velocity * dt);
            var intersectVectorPrev = obj.Position - (obj.Velocity * dt);

            impactPnt = D2DPoint.Zero;

            if (CollisionHelpers.IsIntersecting(obj.Position, intersectVector, groundLineA, groundLineB, out impactPnt))
                return true;

            // Additional check with previous position.
            if (CollisionHelpers.IsIntersecting(obj.Position, intersectVectorPrev, groundLineA, groundLineB, out impactPnt))
                return true;

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float PositionToAltitude(D2DPoint position)
        {
            // Up = negative on the Y axis.
            return -position.Y;
        }

        public static float Cross(D2DPoint vector1, D2DPoint vector2)
        {
            return vector1.X * vector2.Y - vector1.Y * vector2.X;
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

        public static T RandomEnum<T>() where T : struct, IConvertible
        {
            var t = typeof(T);
            var len = Enum.GetNames(t).Length;
            var vals = Enum.GetValues(t) as T[];

            return vals[Rnd.Next(len)];
        }

        public static D2DPoint Translate(this D2DPoint src, float rotation, D2DPoint translation, float scale = 1f)
        {
            var mat = Matrix3x2.CreateScale(scale);
            mat *= Matrix3x2.CreateRotation(rotation * DEGREES_TO_RADS, D2DPoint.Zero);
            mat *= Matrix3x2.CreateTranslation(translation);

            return D2DPoint.Transform(src, mat);
        }

        public static void Translate(this D2DPoint[] src, D2DPoint[] dst, float rotation, D2DPoint translation, float scale = 1f)
        {
            var mat = Matrix3x2.CreateScale(scale);
            mat *= Matrix3x2.CreateRotation(rotation * DEGREES_TO_RADS, D2DPoint.Zero);
            mat *= Matrix3x2.CreateTranslation(translation);

            for (int i = dst.Length - 1; i >= 0; i--)
            {
                dst[i] = D2DPoint.Transform(src[i], mat);
            }
        }

        public static void Translate(this D2DPoint[] src, D2DPoint[] dst, D2DPoint center, float rotation, D2DPoint translation, float scaleX = 1f, float scaleY = 1f)
        {
            var mat = Matrix3x2.CreateScale(scaleX, scaleY, center);
            mat *= Matrix3x2.CreateRotation(rotation * DEGREES_TO_RADS, center);
            mat *= Matrix3x2.CreateTranslation(translation);

            for (int i = dst.Length - 1; i >= 0; i--)
            {
                dst[i] = D2DPoint.Transform(src[i], mat);
            }
        }

        public static D2DColor LerpColor(D2DColor color1, D2DColor color2, float amount)
        {
            color1.r = color1.r + (color2.r - color1.r) * amount;
            color1.g = color1.g + (color2.g - color1.g) * amount;
            color1.b = color1.b + (color2.b - color1.b) * amount;
            color1.a = color1.a + (color2.a - color1.a) * amount;

            return color1;
        }

        public static D2DColor LerpColorWithAlpha(D2DColor color1, D2DColor color2, float amount, float alpha)
        {
            color1.r = color1.r + (color2.r - color1.r) * amount;
            color1.g = color1.g + (color2.g - color1.g) * amount;
            color1.b = color1.b + (color2.b - color1.b) * amount;
            color1.a = alpha;

            return color1;
        }

        public static bool IsPosInFOV(GameObject obj, D2DPoint pos, float fov)
        {
            var dir = pos - obj.Position;
            var angle = dir.Angle();
            var diff = AngleDiff(obj.Rotation, angle);

            return diff <= fov * 0.5f;
        }

        public static D2DPoint RandomPointInCircle(float radius)
        {
            var rndDir = Rnd.NextFloat(0f, 360f);
            var rndLen = Rnd.NextFloat(1f, radius);
            var pnt = AngleToVectorDegrees(rndDir, rndLen);
            return pnt;
        }

        public static float RandomDirection()
        {
            return Rnd.NextFloat(0f, 360f);
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

        public static D2DPoint[] RandomPoly(int nPoints, float radius)
        {
            var poly = new D2DPoint[nPoints];
            var dists = new float[nPoints];

            for (int i = 0; i < nPoints; i++)
            {
                dists[i] = Rnd.NextFloat(radius / 2f, radius);
            }

            var radians = Rnd.NextFloat(0.8f, 1f);
            var angle = 0f;

            for (int i = 0; i < nPoints; i++)
            {
                var pnt = new D2DPoint(MathF.Cos(angle * radians) * dists[i], MathF.Sin(angle * radians) * dists[i]);
                poly[i] = pnt;
                angle += (2f * MathF.PI / nPoints);
            }

            return poly;
        }

        public static bool PointInPoly(D2DPoint pnt, D2DPoint[] poly)
        {
            int i, j = 0;
            bool c = false;
            for (i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if (poly[i].Y > pnt.Y != poly[j].Y > pnt.Y && pnt.X < (poly[j].X - poly[i].X) * (pnt.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X)
                    c = !c;
            }

            return c;
        }

        public static bool PolyInPoly(D2DPoint[] polyA, D2DPoint[] polyB)
        {
            int i, j = 0;
            bool c = false;

            for (int p = 0; p < polyA.Length; p++)
            {
                var pnt = polyA[p];
                c = false;

                for (i = 0, j = polyB.Length - 1; i < polyB.Length; j = i++)
                {
                    if (polyB[i].Y > pnt.Y != polyB[j].Y > pnt.Y && pnt.X < (polyB[j].X - polyB[i].X) * (pnt.Y - polyB[i].Y) / (polyB[j].Y - polyB[i].Y) + polyB[i].X)
                        c = !c;
                }

                if (c)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Wraps the specified index with to the specified length.
        /// 
        /// If the index is less than zero it wraps to the the length.  If the index is greater than length it wraps to zero.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WrapIndex(int index, int length)
        {
            return ((index % length) + length) % length;
        }

        public static float ImpactTime(GameObject objA, GameObject objB)
        {
            var dist = objA.Position.DistanceTo(objB.Position);
            var closingRate = ClosingRate(objA, objB);
            var navTime = dist / closingRate;

            return navTime;
        }

        public static float ClosingRate(GameObject objA, GameObject objB)
        {
            var nextPos1 = objA.Position + objA.Velocity;
            var nextPos2 = objB.Position + objB.Velocity;

            var curDist = objA.Position.DistanceTo(objB.Position);
            var nextDist = nextPos1.DistanceTo(nextPos2);

            return curDist - nextDist;
        }

        public static float GroundImpactTime(GameObject obj)
        {
            var groundPos = new D2DPoint(obj.Position.X, 0f);
            var groundDist = obj.Position.DistanceTo(groundPos);
            var vs = obj.Velocity.Y;
            var impactTime = groundDist / vs;

            return impactTime;
        }

        /// <summary>
        /// Returns the angle required to ascend/descend to and maintain the specified altitude.
        /// </summary>
        /// <param name="obj">Target object.</param>
        /// <param name="targetAltitude">Target altitude.</param>
        /// <returns>Guidance angle.</returns>
        public static float MaintainAltitudeAngle(GameObject obj, float targetAltitude)
        {
            const float MAX_ANGLE = 30f; // Max angle to climb or descend (to meet the target altitude).
            const float EASE_ALT = 1000f; // Determines the difference in altitude at which to begin leveling out.

            var finalAngle = 0f;

            // Get the difference and direction.
            var altDiff = obj.Altitude - targetAltitude;
            var sign = Math.Sign(altDiff);

            // Ease out the angle amount as we approach the target altitude.
            var angleAmount = FactorWithEasing(Math.Abs(altDiff), EASE_ALT, EasingFunctions.Out.EaseSine);

            // Initial angle.
            var angle = MAX_ANGLE * sign * angleAmount;

            // Flip the angle as needed.
            var toRight = IsPointingRight(obj.Rotation);

            if (IsPointingRight(obj.Rotation))
                finalAngle = angle;
            else
                finalAngle = 180f - angle;

            return ClampAngle(finalAngle);
        }

        public static float AngularSize(FighterPlane plane, D2DPoint viewPoint)
        {
            const float MAX_PLANE_WIDTH = 120f;
            const float MIN_PLANE_THICCNESS = 10f;

            var dist = viewPoint.DistanceTo(plane.Position);
            var angleTo = viewPoint.AngleTo(plane.Position);
            var angleToRot = ClampAngle(angleTo - plane.Rotation);

            // Make a line segment to represent the plane's rotation.
            var lineA = D2DPoint.Zero;
            var lineB = new D2DPoint(0f, MAX_PLANE_WIDTH);

            // Rotate the segment.
            lineA = lineA.Translate(angleToRot, D2DPoint.Zero);
            lineB = lineB.Translate(angleToRot, D2DPoint.Zero);

            // Get the abs diff between the X coords of the line to compute linear diameter.
            var linearDiam = Math.Abs(lineB.X - lineA.X);
            linearDiam = Math.Clamp(linearDiam, MIN_PLANE_THICCNESS, float.MaxValue);

            var delta = 2f * MathF.Atan(linearDiam / (2f * dist));

            return RadsToDegrees(delta);
        }

        public static float AngularSize(Decoy decoy, D2DPoint viewPoint)
        {
            var dist = viewPoint.DistanceTo(decoy.Position);
            var linearDiam = decoy.CurrentRadius;
            var delta = 2f * MathF.Atan(linearDiam / (2f * dist));

            return RadsToDegrees(delta);
        }

        /// <summary>
        /// Computes the linear velocity for the specified point in relation to the rotation speed and position of the parent object.
        /// </summary>
        /// <param name="parentObject">Parent object from which position and rotation speed are taken.</param>
        /// <param name="point">A point along the axis of rotation.</param>
        /// <returns>A velocity vector which sums the parent object velocity with the computed linear velocity.</returns>
        /// <remarks>See: http://hyperphysics.phy-astr.gsu.edu/hbase/rotq.html</remarks>
        public static D2DPoint PointVelocity(GameObject parentObject, D2DPoint point)
        {
            var baseVelo = parentObject.Velocity;
            var linearVelo = LinearVelocity(parentObject.Position, point, parentObject.RotationSpeed);

            return baseVelo + linearVelo;
        }

        /// <summary>
        /// Computes the linear velocity for the specified point given the specified center point and rotation speed.
        /// </summary>
        /// <param name="center">Center point representing the axis of rotation.</param>
        /// <param name="point">A point along the axis of rotation.</param>
        /// <param name="rotationSpeedDeg">Rotation speed (angular velocity) in degrees per second.</param>
        /// <returns>The linear velocity of the specified point.</returns>
        /// <remarks>See: http://hyperphysics.phy-astr.gsu.edu/hbase/rotq.html</remarks>
        public static D2DPoint LinearVelocity(D2DPoint center, D2DPoint point, float rotationSpeedDeg)
        {
            // V = WR
            var R = center - point;
            var W = DegreesToRads(rotationSpeedDeg);
            var V = R.Tangent() * W;

            return V;
        }

        public static float GetTorque(D2DPoint centerPosition, D2DPoint forcePosition, D2DPoint force)
        {
            // How is it so simple?
            var r = forcePosition - centerPosition;

            var torque = Utilities.Cross(r, force);
            return torque;
        }

        public static D2DPoint CenterOfLift(params Wing[] wings)
        {
            float totLift = 0f;
            D2DPoint cl = D2DPoint.Zero;

            foreach (var wing in wings)
            {
                totLift += wing.Parameters.MaxLiftForce;
            }

            foreach (var wing in wings)
            {
                cl += wing.Position * (wing.Parameters.MaxLiftForce / totLift);
            }

            return cl;
        }

        public static bool IsPointingRight(float angle)
        {
            var rot180 = ClampAngle180(angle + 90f);

            return rot180 > 0f;
        }

        public static bool IsPointingDown(float angle)
        {
            var rot180 = ClampAngle180(angle + 180f);

            return rot180 < 0f;
        }

        public static string GetLocalIP()
        {
            var addys = Dns.GetHostAddresses(Dns.GetHostName());

            foreach (var ip in addys)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && ip.IsIPv6LinkLocal == false)
                {
                    return ip.ToString();
                }
            }

            return null;
        }

        public static string GetRandomName()
        {
            var rnd = Utilities.Rnd;
            var len = rnd.Next(5, 8);

            string[] consonants = { "b", "c", "d", "f", "g", "h", "j", "k", "l", "m", "l", "n", "p", "q", "r", "s", "t", "v", "w", "x" };
            string[] vowels = { "a", "e", "i", "o", "u", "y" };
            string Name = "";
            Name += consonants[rnd.Next(consonants.Length)].ToUpper();
            Name += vowels[rnd.Next(vowels.Length)];
            int b = 2; //b tells how many times a new letter has been added. It's 2 right now because the first two letters are already in the name.
            while (b < len)
            {
                Name += consonants[rnd.Next(consonants.Length)];
                b++;
                Name += vowels[rnd.Next(vowels.Length)];
                b++;
            }

            return Name;
        }

        public static string GetPersonalityTag(AIPersonality personality)
        {
            string tagText = "";
            var types = Enum.GetValues(typeof(AIPersonality));

            foreach (AIPersonality type in types)
            {
                if ((personality & type) == type)
                {
                    switch (type)
                    {
                        case AIPersonality.Normal:
                            //text += "N";
                            break;

                        case AIPersonality.MissileHappy:
                            tagText += "M";
                            break;

                        case AIPersonality.LongBursts:
                            tagText += "L";
                            break;

                        case AIPersonality.Cowardly:
                            tagText += "C";
                            break;

                        case AIPersonality.Speedy:
                            tagText += "S";
                            break;

                        case AIPersonality.Vengeful:
                            tagText += "V";
                            break;

                        case AIPersonality.TargetTopPlanes:
                            tagText += "T";
                            break;
                    }
                }
            }

            return tagText;
        }

        public static AIPersonality GetRandomPersonalities(int num, bool allowCowardly = true)
        {
            AIPersonality personality = AIPersonality.Normal;

            int nAdded = 0;

            while (nAdded < num)
            {
                var rndPers = RandomEnum<AIPersonality>();

                if (!allowCowardly)
                {
                    while (rndPers == AIPersonality.Cowardly)
                        rndPers = RandomEnum<AIPersonality>();
                }

                if (!personality.HasFlag(rndPers))
                {
                    personality |= rndPers;
                    nAdded++;
                }
            }

            return personality;
        }

        public static D2DPoint ScaleToOrigin(GameObject obj, D2DPoint point)
        {
            var mat = Matrix3x2.CreateScale(1f * (1f / obj.RenderScale), obj.Position);
            mat *= Matrix3x2.CreateRotation(-obj.Rotation * DEGREES_TO_RADS, obj.Position);
            mat *= Matrix3x2.CreateTranslation(new D2DPoint(-obj.Position.X, -obj.Position.Y));
            return D2DPoint.Transform(point, mat);
        }

        public static D2DPoint FindSafeSpawnPoint()
        {
            const float MIN_DIST = 40000f;
            const float MAX_DIST = 100000f;

            const float MIN_ALT = 3000f;
            const float MAX_ALT = 25000f;

            var minDist = MIN_DIST;
            var maxDist = MAX_DIST;

            // Half the spawn range when in guns only mode.
            if (World.GunsOnly)
            {
                minDist = minDist / 2f;
                maxDist = maxDist / 2f;
            }

            var point = new D2DPoint(Rnd.NextFloat(-World.PlaneSpawnRange, World.PlaneSpawnRange), Rnd.NextFloat(-MAX_ALT, -MIN_ALT));
            var objs = World.ObjectManager;

            if (objs.Planes.Count == 0)
                return point;

            var sortedPoints = new List<Tuple<float, D2DPoint>>();

            for (int x = (int)-World.PlaneSpawnRange; x < World.PlaneSpawnRange; x += (int)(minDist / 4f))
            {
                for (int y = (int)MIN_ALT; y < MAX_ALT; y += 1000)
                {
                    var testPoint = new D2DPoint(x, -y);
                    float bestDist = float.MaxValue;
                    D2DPoint bestPoint = testPoint;

                    foreach (var testPlane in objs.Planes.Where(p => p.IsDisabled == false))
                    {
                        var dist = testPoint.DistanceTo(testPlane.Position);

                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            bestPoint = testPoint;
                        }
                    }

                    sortedPoints.Add(new Tuple<float, D2DPoint>(bestDist, bestPoint));
                }
            }

            D2DPoint ret = point;

            if (sortedPoints.Count > 0)
            {
                sortedPoints = sortedPoints.OrderBy(p => p.Item1).ToList();

                if (sortedPoints.Last().Item1 < minDist)
                    ret = sortedPoints.Last().Item2;
                else
                {
                    var validPoints = sortedPoints.Where(p => p.Item1 >= minDist && p.Item1 <= maxDist).ToList();

                    if (validPoints.Count > 0)
                        ret = validPoints[Rnd.Next(0, validPoints.Count)].Item2;
                    else
                        ret = sortedPoints.Last().Item2;
                }
            }

            return ret;
        }
    }
}
