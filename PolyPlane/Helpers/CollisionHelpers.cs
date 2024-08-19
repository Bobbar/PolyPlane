using PolyPlane.GameObjects;
using System.Numerics;
using unvell.D2DLib;

namespace PolyPlane.Helpers
{
    /// <summary>
    /// Credit: https://www.codeproject.com/Tips/862988/Find-the-Intersection-Point-of-Two-Line-Segments
    /// </summary>
    public static class CollisionHelpers
    {
        public static bool IsIntersecting(D2DPoint p, D2DPoint p2, D2DPoint q, D2DPoint q2, out D2DPoint pos)
        {
            pos = D2DPoint.Zero;

            var r = p2 - p;
            var s = q2 - q;
            var rxs = r.Cross(s);
            var qpxr = (q - p).Cross(r);

            // If r x s = 0 and (q - p) x r = 0, then the two lines are collinear.
            if (rxs.IsZero() && qpxr.IsZero())
            {
                //// 1. If either  0 <= (q - p) * r <= r * r or 0 <= (p - q) * s <= * s
                //// then the two lines are overlapping,
                //if (considerCollinearOverlapAsIntersect)
                //    if ((0 <= (q - p) * r && (q - p) * r <= r * r) || (0 <= (p - q) * s && (p - q) * s <= s * s))
                //        return true;

                // 2. If neither 0 <= (q - p) * r = r * r nor 0 <= (p - q) * s <= s * s
                // then the two lines are collinear but disjoint.
                // No need to implement this expression, as it follows from the expression above.
                return false;
            }

            // 3. If r x s = 0 and (q - p) x r != 0, then the two lines are parallel and non-intersecting.
            if (rxs.IsZero() && qpxr.IsZero())
                return false;

            // t = (q - p) x s / (r x s)
            var t = (q - p).Cross(s) / rxs;

            // u = (q - p) x r / (r x s)
            var u = (q - p).Cross(r) / rxs;

            // 4. If r x s != 0 and 0 <= t <= 1 and 0 <= u <= 1
            // the two line segments meet at the point p + t r = q + u s.
            if (!rxs.IsZero() && (0f <= t && t <= 1f) && (0f <= u && u <= 1f))
            {
                // We can calculate the intersection point using either t or u.
                pos = p + t * r;

                // An intersection was found.
                return true;
            }

            // 5. Otherwise, the two line segments are not parallel but do not intersect.
            return false;
        }

        private const float Epsilon = 1e-10f;
        private static bool IsZero(this float value)
        {
            return Math.Abs(value) < Epsilon;
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

            for (int i = 0; i < poly.Length; i++)
            {
                var idx1 = Utilities.WrapIndex(i, poly.Length);
                var idx2 = Utilities.WrapIndex(i + 1, poly.Length);

                var pnt1 = poly[idx1];
                var pnt2 = poly[idx2];

                if (IsIntersecting(a, b, pnt1, pnt2, out D2DPoint iPos))
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

            const float BB_INFLATE_AMT = 10f;
            var hits = new List<D2DPoint>();
            var velo = impactorObj.Velocity * dt;
            var veloHalf = velo * 0.5f;

            var impactorPoly = impactorObj.Polygon.Poly;
            var impactorBounds = new BoundingBox(impactorPoly, BB_INFLATE_AMT);

            // For new bullets, do a ray cast between the predicted "real" start position and the current position.
            // This is done because we are extrapolating the bullet position when a new packet is received,
            // so we need to handle collisions for the "gap" between the real bullet/plane and the net bullet on the client.
            if (impactorObj is Bullet && impactorObj.AgeMs < (impactorObj.LagAmount * 1f))
            {
                var lagPntStart = impactorObj.Position - (impactorObj.Velocity * (float)((((impactorObj.LagAmount) / 16.6f) * World.DT)));
                var lagPntEnd = impactorObj.Position;

                // Check for intersection on bounding box first.
                if (PolyIntersect(lagPntStart, lagPntEnd, impactorBounds.BoundsPoly, out D2DPoint boundsPnt))
                {
                    if (PolyIntersect(lagPntStart, lagPntEnd, targPoly, out D2DPoint iPosLag))
                    {
                        impactPoint = iPosLag;
                        return true;
                    }
                }
            }

            for (int i = 0; i < impactorPoly.Length; i++)
            {
                var pnt1 = impactorPoly[i] - veloHalf;
                var pnt2 = impactorPoly[i] + veloHalf;

                // Check for intersection on bounding box first.
                if (PolyIntersect(pnt1, pnt2, impactorBounds.BoundsPoly, out D2DPoint boundsPnt))
                {
                    // Check for an intersection and get the exact location of the impact.
                    if (PolyIntersect(pnt1, pnt2, targPoly, out D2DPoint iPosPoly))
                    {
                        hits.Add(iPosPoly);
                    }
                }
            }

            // One last check with the center point.
            var centerPnt1 = impactorObj.Position - veloHalf;
            var centerPnt2 = impactorObj.Position + veloHalf;

            // Check for intersection on bounding box first.
            if (PolyIntersect(centerPnt1, centerPnt2, impactorBounds.BoundsPoly, out D2DPoint boundsPntCenter))
            {
                if (PolyIntersect(centerPnt1, centerPnt2, targPoly, out D2DPoint iPosCenter))
                {
                    hits.Add(iPosCenter);
                }
            }

            if (hits.Count == 1)
            {
                impactPoint = hits.First();
                return true;
            }
            else if (hits.Count > 1)  // If we have multiple hits, find the one closest to the impactor's previous position.
            {
                var closest = hits.OrderBy(p => p.DistanceTo(impactorObj.Position - veloHalf));
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
            var relVeloHalf = relVelo * 0.5f;

            var centerPnt1 = impactorObj.Position - relVeloHalf;
            var centerPnt2 = impactorObj.Position + relVeloHalf;

            if (PolyIntersect(centerPnt1, centerPnt2, targPoly, out D2DPoint iPosCenter))
            {
                hits.Add(iPosCenter);
            }

            if (hits.Count == 1)
            {
                impactPoint = hits.First();
                return true;
            }
            else if (hits.Count > 1)  // If we have multiple hits, find the one closest to the impactor's previous position.
            {
                var closest = hits.OrderBy(p => p.DistanceTo(impactorObj.Position - relVeloHalf));
                impactPoint = closest.First();
                return true;
            }

            impactPoint = D2DPoint.Zero;
            return false;
        }
    }
}
