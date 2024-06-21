using PolyPlane.GameObjects;
using System.Numerics;
using unvell.D2DLib;

namespace PolyPlane.Helpers
{
    /// <summary>
    /// Credit: https://www.habrador.com/tutorials/math/5-line-line-intersection/
    /// </summary>
    public static class CollisionHelpers
    {
        //Check if the lines are interesecting in 2d space

        public static bool IsIntersecting(D2DPoint a1, D2DPoint b1, D2DPoint a2, D2DPoint b2, out D2DPoint pos)
        {
            D2DPoint l1_start = new D2DPoint(a1.X, a1.Y);
            D2DPoint l1_end = new D2DPoint(b1.X, b1.Y);

            D2DPoint l2_start = new D2DPoint(a2.X, a2.Y);
            D2DPoint l2_end = new D2DPoint(b2.X, b2.Y);

            //Direction of the lines
            D2DPoint l1_dir = (l1_end - l1_start).Normalized();
            D2DPoint l2_dir = (l2_end - l2_start).Normalized();

            //If we know the direction we can get the normal vector to each line
            D2DPoint l1_normal = new D2DPoint(-l1_dir.Y, l1_dir.X);
            D2DPoint l2_normal = new D2DPoint(-l2_dir.Y, l2_dir.X);


            //Step 1: Rewrite the lines to a general form: Ax + By = k1 and Cx + Dy = k2
            //The normal vector is the A, B
            float A = l1_normal.X;
            float B = l1_normal.Y;

            float C = l2_normal.X;
            float D = l2_normal.Y;

            //To get k we just use one point on the line
            float k1 = A * l1_start.X + B * l1_start.Y;
            float k2 = C * l2_start.X + D * l2_start.Y;

            //Step 2: are the lines parallel? -> no solutions
            if (IsParallel(l1_normal, l2_normal))
            {
                pos = D2DPoint.Zero;
                return false;
            }

            //Step 3: are the lines the same line? -> infinite amount of solutions
            //Pick one point on each line and test if the vector between the points is orthogonal to one of the normals
            if (IsOrthogonal(l1_start - l2_start, l1_normal))
            {
                //Return false anyway
                pos = D2DPoint.Zero;
                return false;
            }

            //Step 4: calculate the intersection point -> one solution
            float x_intersect = (D * k1 - B * k2) / (A * D - B * C);
            float y_intersect = (-C * k1 + A * k2) / (A * D - B * C);

            D2DPoint intersectPoint = new D2DPoint(x_intersect, y_intersect);

            //Step 5: but we have line segments so we have to check if the intersection point is within the segment
            if (IsBetween(l1_start, l1_end, intersectPoint) && IsBetween(l2_start, l2_end, intersectPoint))
            {
                pos = intersectPoint;
                return true;
            }

            pos = D2DPoint.Zero;
            return false;
        }

        //Are 2 vectors parallel?
        public static bool IsParallel(D2DPoint v1, D2DPoint v2)
        {
            //2 vectors are parallel if the angle between the vectors are 0 or 180 degrees
            if (v1.AngleTo(v2) == 0f || v1.AngleTo(v2) == 180f)
            {
                return true;
            }

            return false;
        }

        //Are 2 vectors orthogonal?
        public static bool IsOrthogonal(D2DPoint v1, D2DPoint v2)
        {
            //2 vectors are orthogonal is the dot product is 0
            //We have to check if close to 0 because of floating numbers
            if (Math.Abs(D2DPoint.Dot(v1, v2)) < 0.000001f)
            {
                return true;
            }

            return false;
        }

        //Is a point c between 2 other points a and b?
        public static bool IsBetween(D2DPoint a, D2DPoint b, D2DPoint c)
        {
            bool isBetween = false;

            //Entire line segment
            D2DPoint ab = b - a;
            //The intersection and the first point
            D2DPoint ac = c - a;

            //Need to check 2 things: 
            //1. If the vectors are pointing in the same direction = if the dot product is positive
            //2. If the length of the vector between the intersection and the first point is smaller than the entire line
            if (D2DPoint.Dot(ab, ac) > 0f && ab.LengthSquared() >= ac.LengthSquared())
            {
                isBetween = true;
            }

            return isBetween;
        }

        public static bool EllipseContains(D2DEllipse ellipse, float ellipseRotation, D2DPoint pos)
        {
            var mat = Matrix3x2.CreateRotation(-ellipseRotation * (float)(Math.PI / 180f), ellipse.origin);
            var transPos = D2DPoint.Transform(pos, mat);

            var p = Math.Pow(transPos.X - ellipse.origin.X, 2f) / Math.Pow(ellipse.radiusX, 2f) + Math.Pow(transPos.Y - ellipse.origin.Y, 2f) / Math.Pow(ellipse.radiusY, 2f);

            return p <= 1f;
        }

        public static bool PolyIntersect(D2DPoint a, D2DPoint b, D2DPoint[] poly, out D2DPoint pos)
        {
            var intersections = new List<D2DPoint>();

            // Check the segment against every segment in the polygon.
            for (int i = 0; i < poly.Length - 1; i++)
            {
                var pnt1 = poly[i];
                var pnt2 = poly[i + 1];

                if (CollisionHelpers.IsIntersecting(a, b, pnt1, pnt2, out D2DPoint iPos))
                {
                    intersections.Add(iPos);
                }
            }

            // Return the intersection closest to the first point.
            if (intersections.Count > 0)
            {
                var best = intersections.OrderBy(i => i.DistanceTo(a));
                pos = best.First();
                return true;
            }

            pos = D2DPoint.Zero;
            return false;
        }

        public static bool PolygonSweepCollision(GameObjectPoly impactorObj, D2DPoint[] targPoly, D2DPoint targVelo, float dt, out D2DPoint impactPoint)
        {
            // Sweep-based Continuous Collision Detection technique.
            // Project lines from each polygon vert of the impactor; one point at the current position, and one point at the next/future position.
            // Then for each of those lines, check for intersections on each line segment of the target object's polygon.

            var hits = new List<D2DPoint>();
            var relVelo = (impactorObj.Velocity - targVelo) * dt; // Get relative velo.
            var impactorPoly = impactorObj.Polygon.Poly;


            // For new bullets, do a ray cast between the predicted "real" start position and the current position.
            // This is done because we are extrapolating the bullet position when a new packet is received,
            // so we need to handle collisions for the "gap" between the real bullet/plane and the net bullet on the client.
            if (impactorObj is Bullet && impactorObj.AgeMs < (impactorObj.LagAmount * 1f))
            {
                var lagPntStart = impactorObj.Position - (impactorObj.Velocity * (float)((((impactorObj.LagAmount) / 16.6f) * World.DT)));
                var lagPntEnd = impactorObj.Position;

                if (PolyIntersect(lagPntStart, lagPntEnd, targPoly, out D2DPoint iPosLag))
                {
                    impactPoint = iPosLag;
                    return true;
                }
            }


            for (int i = 0; i < impactorPoly.Length; i++)
            {
                var pnt1 = impactorPoly[i];
                var pnt2 = impactorPoly[i] + relVelo;

                // Check for an intersection and get the exact location of the impact.
                if (PolyIntersect(pnt1, pnt2, targPoly, out D2DPoint iPosPoly))
                {
                    hits.Add(iPosPoly);
                }
            }

            // One last check with the center point.
            var centerPnt1 = impactorObj.Position;
            var centerPnt2 = impactorObj.Position + relVelo;

            if (PolyIntersect(centerPnt1, centerPnt2, targPoly, out D2DPoint iPosCenter))
            {
                hits.Add(iPosCenter);
            }

            // If we have multiple hits, find the one closest to the impactor.
            if (hits.Count > 0)
            {
                var closest = hits.OrderBy(p => p.DistanceTo(impactorObj.Position));
                impactPoint = closest.First();
                return true;
            }

            impactPoint = D2DPoint.Zero;
            return false;
        }

        public static bool PolygonSweepCollision(GameObject impactorObj, D2DPoint[] targPoly, D2DPoint targVelo, float dt, out D2DPoint impactPoint)
        {
            // Sweep-based Continuous Collision Detection technique.
            // Project lines from each polygon vert of the impactor; one point at the current position, and one point at the next/future position.
            // Then for each of those lines, check for intersections on each line segment of the target object's polygon.

            var hits = new List<D2DPoint>();
            var relVelo = (impactorObj.Velocity - targVelo) * dt; // Get relative velo.

            var centerPnt1 = impactorObj.Position;
            var centerPnt2 = impactorObj.Position + relVelo;

            if (PolyIntersect(centerPnt1, centerPnt2, targPoly, out D2DPoint iPosCenter))
            {
                hits.Add(iPosCenter);
            }

            // If we have multiple hits, find the one closest to the impactor.
            if (hits.Count > 0)
            {
                var closest = hits.OrderBy(p => p.DistanceTo(impactorObj.Position));
                impactPoint = closest.First();
                return true;
            }

            impactPoint = D2DPoint.Zero;
            return false;
        }
    }
}
