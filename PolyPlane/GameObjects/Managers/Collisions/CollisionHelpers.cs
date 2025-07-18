﻿using PolyPlane.GameObjects.Tools;
using PolyPlane.Helpers;

namespace PolyPlane.GameObjects.Managers
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
            if (!rxs.IsZero() && 0f <= t && t <= 1f && 0f <= u && u <= 1f)
            {
                // We can calculate the intersection point using either t or u.
                pos = p + t * r;

                // An intersection was found.
                return true;
            }

            // 5. Otherwise, the two line segments are not parallel but do not intersect.
            return false;
        }

        private static bool IsZero(this float value)
        {
            return Math.Abs(value) < float.Epsilon;
        }

        public static bool PolyIntersect(D2DPoint a, D2DPoint b, D2DPoint[] poly)
        {
            for (int i = 0; i < poly.Length; i++)
            {
                var idx1 = Utilities.WrapIndex(i, poly.Length);
                var idx2 = Utilities.WrapIndex(i + 1, poly.Length);

                var pnt1 = poly[idx1];
                var pnt2 = poly[idx2];

                if (IsIntersecting(a, b, pnt1, pnt2, out D2DPoint iPos))
                    return true;
            }

            return false;
        }

        public static bool PolyIntersect(D2DPoint a, D2DPoint b, IEnumerable<LineSegment> segs, out D2DPoint pos)
        {
            foreach (var seg in segs)
            {
                if (IsIntersecting(a, b, seg.A, seg.B, out D2DPoint iPos))
                {
                    pos = iPos;
                    return true;
                }
            }

            pos = D2DPoint.Zero;
            return false;
        }


        public static bool PolygonSweepCollision(GameObject impactorObj, RenderPoly impactorPoly, RenderPoly targetPoly, D2DPoint targetVelo, float dt, out D2DPoint impactPoint)
        {
            // Sweep-based Continuous Collision Detection technique.
            // Project lines from each polygon vert of the impactor; one point at the current position, and one point at the next/future position.
            // Then for each of those lines, check for intersections on each line segment of the target object's polygon.

            const float BB_INFLATE_AMT = 10f;
            bool movedBack = false;

            // Get relative velo.
            var relVelo = (impactorObj.Velocity - targetVelo) * dt;

            // Bounding box for initial collision testing.
            var targetBounds = new BoundingBox(targetPoly.Poly, BB_INFLATE_AMT);

            // Special ray-casting and move-back logic for bullets.
            if (impactorObj is Bullet bullet)
            {
                // For new bullets in net games, do a ray cast between the predicted "real" start position and the current position.
                // This is done because we are extrapolating the bullet position when a new packet is received,
                // so we need to handle collisions for the "gap" between the real bullet/plane and the net bullet on the client.
                if (World.IsNetGame)
                {
                    if (bullet.AgeMs(dt) < bullet.LagAmount)
                    {
                        var lagPntStart = bullet.Position - bullet.Velocity * (bullet.LagAmountFrames * dt);
                        var lagPntEnd = bullet.Position;

                        // Check for intersection on bounding box first.
                        if (targetBounds.BoundsRect.Contains(lagPntStart, lagPntEnd) || targetBounds.Contains(lagPntStart, lagPntEnd, bullet.Position))
                        {
                            // Get the sides of the poly which face the impactor.
                            var angleToImpactor = lagPntStart - lagPntEnd;
                            var polyFaces = targetPoly.GetSidesFacingDirection(angleToImpactor);

                            if (PolyIntersect(lagPntStart, lagPntEnd, polyFaces, out D2DPoint iPosLag))
                            {
                                impactPoint = iPosLag;
                                return true;
                            }
                        }
                    }
                }
                else
                {
                    // Ray cast for new bullets during local play.
                    if (bullet.Frame <= 1)
                    {
                        var rayPnt1 = bullet.SpawnPoint;
                        var rayPnt2 = bullet.Position;

                        // Check for intersection on bounding box first.
                        if (targetBounds.BoundsRect.Contains(rayPnt1, rayPnt2) || targetBounds.Contains(rayPnt1, rayPnt2))
                        {
                            // Get the sides of the poly which face the impactor.
                            var angleToImpactor = rayPnt1 - rayPnt2;
                            var polyFaces = targetPoly.GetSidesFacingDirection(angleToImpactor);

                            // Check for an intersection and get the exact location of the impact.
                            if (PolyIntersect(rayPnt1, rayPnt2, polyFaces, out D2DPoint iPosPoly))
                            {
                                impactPoint = iPosPoly;
                                return true;
                            }
                        }
                    }
                }


                // Check if we need to move the impactor backwards because it is already inside the target polygon.
                if (targetBounds.Contains(bullet.Position))
                {
                    while (Utilities.PolyInPoly(impactorPoly.Poly, targetPoly.Poly))
                    {
                        movedBack = true;
                        bullet.Position -= bullet.Velocity * dt;

                        // Stop if it gets close to the ground. Otherwise might butt heads with the ground clamping logic.
                        if (bullet.Position.Y < 5f)
                            break;
                    }

                    // Move it back one last step then update the polygon with the new position.
                    if (movedBack)
                    {
                        bullet.Position -= bullet.Velocity * dt;
                        impactorPoly.Update();
                    }
                }
            }

            // ### Regular swept polygon collisions ###
            // Test the facing points of the impactor with the target poly.
            var angleToTarget = impactorObj.Velocity - targetVelo;
            var impactorPoints = impactorPoly.GetPointsFacingDirection(angleToTarget);

            foreach (var pnt in impactorPoints)
            {
                var pnt1 = pnt;
                var pnt2 = pnt + relVelo;

                // Check for intersection on bounding box first.
                if (targetBounds.BoundsRect.Contains(pnt1, pnt2) || targetBounds.Contains(pnt1, pnt2, impactorObj.Position))
                {
                    // Get the sides of the poly which face the impactor.
                    var angleToImpactor = pnt1 - pnt2;
                    var polyFaces = targetPoly.GetSidesFacingDirection(angleToImpactor);

                    // Check for an intersection and get the exact location of the impact.
                    if (PolyIntersect(pnt1, pnt2, polyFaces, out D2DPoint iPosPoly))
                    {
                        impactPoint = iPosPoly;
                        return true;
                    }
                }
            }

            // One last check with the center point.
            var centerPnt1 = impactorObj.Position;
            var centerPnt2 = impactorObj.Position + relVelo;

            // Check for intersection on bounding box first.
            if (targetBounds.BoundsRect.Contains(centerPnt1, centerPnt2) || targetBounds.Contains(centerPnt1, centerPnt2))
            {
                // Get the sides of the poly which face the impactor.
                var angleToImpactor = centerPnt1 - centerPnt2;
                var polyFaces = targetPoly.GetSidesFacingDirection(angleToImpactor);

                // Check for an intersection and get the exact location of the impact.
                if (PolyIntersect(centerPnt1, centerPnt2, polyFaces, out D2DPoint iPosPoly))
                {
                    impactPoint = iPosPoly;
                    return true;
                }
            }

            impactPoint = D2DPoint.Zero;
            return false;
        }

        public static bool PolygonSweepCollision(GameObject impactorObj, RenderPoly targetPoly, D2DPoint targVelo, float dt, out D2DPoint impactPoint)
        {
            // Sweep-based Continuous Collision Detection technique.
            // Project lines from each polygon vert of the impactor; one point at the current position, and one point at the next/future position.
            // Then for each of those lines, check for intersections on each line segment of the target object's polygon.

            var relVelo = (impactorObj.Velocity - targVelo) * dt; // Get relative velo.

            var centerPnt1 = impactorObj.Position;
            var centerPnt2 = impactorObj.Position + relVelo;

            // Get the sides of the poly which face the impactor.
            var angleToImpactor = centerPnt1 - centerPnt2;
            var polyFaces = targetPoly.GetSidesFacingDirection(angleToImpactor);

            // Check for an intersection and get the exact location of the impact.
            if (PolyIntersect(centerPnt1, centerPnt2, polyFaces, out D2DPoint iPosPoly))
            {
                impactPoint = iPosPoly;
                return true;
            }

            impactPoint = D2DPoint.Zero;
            return false;
        }
    }
}
