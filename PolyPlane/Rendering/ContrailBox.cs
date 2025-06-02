using PolyPlane.GameObjects;
using PolyPlane.Helpers;
using unvell.D2DLib;

namespace PolyPlane.Rendering
{
    /// <summary>
    /// Manages and renders contrails for all planes.
    /// </summary>
    public sealed class ContrailBox
    {
        private const float MIN_ALT = 20000f;
        private const float MAX_SEG_AGE = 120f;
        private const float ALPHA = 0.3f;
        private const float MIN_DIST = 40f;
        private const float MAX_ALPHA_ALT = 6000f;
        private const float TRAIL_WEIGHT = 8f;
        private const float LIGHT_INTENSITY = 0.4f;

        private Dictionary<GameID, PlaneTag> _currentPlanes = new();
        private List<TrailSegment> _segments = new List<TrailSegment>();
        private SpatialGrid<TrailSegment> _segmentGrid = new(s => s.PointA, SegmentIsExpired);
        private GameObjectPool<TrailSegment> _segmentPool = new GameObjectPool<TrailSegment>(() => new TrailSegment());
        private D2DColor _trailColor = new D2DColor(ALPHA, D2DColor.WhiteSmoke);

        public void Update(List<FighterPlane> planes, float dt)
        {
            // Try to compute an offset min distance amount based on time delta.
            // Higher DT == more distance covered between updates,
            // so we need to increase the min distance as DT increases.
            var minDistDT = MIN_DIST + ((MIN_DIST * dt) * 10f);

            for (int i = 0; i < planes.Count; i++)
            {
                var plane = planes[i];

                if (IsInside(plane))
                {
                    // Add tag for new planes.
                    if (IsNew(plane))
                    {
                        _currentPlanes.Add(plane.ID, new PlaneTag(plane));
                    }
                    else
                    {
                        if (_currentPlanes.TryGetValue(plane.ID, out var tag))
                        {
                            // Is the engine running and making thrust?
                            if (plane.ThrustAmount > 0f && IsNotInSpace(plane) && !plane.IsDisabled)
                            {
                                var newPos = plane.ExhaustPosition;
                                var dist = newPos.DistanceTo(tag.PrevPos);

                                // If the length of this segment is too large
                                // just update the previous position and continue.
                                // This can happen when a plane re-spawns at another
                                // point inside the box.
                                if (dist > minDistDT * 3f)
                                {
                                    tag.PrevPos = newPos;
                                    continue;
                                }

                                // Add a new segment and update previous position.
                                if (dist >= minDistDT)
                                {
                                    var seg = _segmentPool.RentObject();
                                    seg.ReInit(plane, tag.PrevPos, newPos);

                                    _segments.Add(seg);
                                    _segmentGrid.Add(seg);

                                    tag.PrevPos = newPos;
                                }
                            }
                        }
                    }
                }
                else
                {
                    // Remove planes that leave the box.
                    if (!IsNew(plane))
                    {
                        _currentPlanes.Remove(plane.ID);
                    }
                }

                // Prune expired planes.
                if (plane.IsExpired)
                {
                    if (_currentPlanes.TryGetValue(plane.ID, out var tag))
                        _currentPlanes.Remove(plane.ID);

                }
            }

            if (!World.IsPaused)
            {
                // Advance and prune segments.
                for (int i = _segments.Count - 1; i >= 0; i--)
                {
                    var seg = _segments[i];
                    seg.Age += dt;

                    if (SegmentIsExpired(seg))
                    {
                        _segments.RemoveAt(i);
                        _segmentPool.ReturnObject(seg);
                    }
                }

                // Prune expired planes.
                foreach (var kvp in _currentPlanes)
                {
                    var tag = kvp.Value;
                    var plane = tag.Plane;

                    if (plane.IsExpired)
                        _currentPlanes.Remove(plane.ID);
                }

                _segmentGrid.Update();
            }
        }

        public void Render(RenderContext ctx)
        {
            // Don't proceed if the viewport is below the minimum altitude.
            if (Math.Abs(ctx.Viewport.top) < MIN_ALT)
                return;

            // Render all visible segments.
            var inViewPort = _segmentGrid.GetInViewport(ctx.Viewport);

            foreach (var seg in inViewPort)
            {
                var altFact = GetAltFadeInFactor(seg.PointA);
                var ageFact = (1f - Utilities.Factor(seg.Age, MAX_SEG_AGE));
                var alpha = ALPHA * ageFact * altFact;
                var color = _trailColor.WithAlpha(alpha);

                ctx.DrawLineWithLighting(seg.PointA, seg.PointB, color, LIGHT_INTENSITY, TRAIL_WEIGHT);
            }

            // Draw final connectors between planes and the last segment.
            foreach (var kvp in _currentPlanes)
            {
                var tag = kvp.Value;
                var plane = tag.Plane;

                var altFact = GetAltFadeInFactor(tag.PrevPos);
                var alpha = ALPHA * altFact;
                var color = _trailColor.WithAlpha(alpha);

                var dist = tag.PrevPos.DistanceTo(plane.ExhaustPosition);
                if (dist > MIN_DIST * 3f)
                    continue;

                if (ctx.Viewport.Contains(plane.Position))
                    if (IsInside(plane) && plane.ThrustAmount > 0f && IsNotInSpace(plane) && !plane.IsDisabled)
                        ctx.DrawLineWithLighting(tag.PrevPos, plane.ExhaustPosition, color, LIGHT_INTENSITY, TRAIL_WEIGHT);
            }
        }

        private float GetAltFadeInFactor(D2DPoint point)
        {
            var altFact = Utilities.Factor(Math.Abs(point.Y) - MIN_ALT, MAX_ALPHA_ALT);
            return altFact;
        }

        /// <summary>
        /// Returns true if the specified plane is within the "atmosphere".
        /// </summary>
        /// <param name="plane"></param>
        /// <returns></returns>
        private bool IsNotInSpace(FighterPlane plane)
        {
            return World.GetAltitudeDensity(plane.ExhaustPosition) > 0.01f;
        }

        /// <summary>
        /// Have we seen this plane already?
        /// </summary>
        /// <param name="plane"></param>
        /// <returns></returns>
        private bool IsNew(FighterPlane plane)
        {
            return !_currentPlanes.ContainsKey(plane.ID);
        }

        /// <summary>
        /// Is the plane inside our area of responsibility.
        /// </summary>
        /// <param name="plane"></param>
        /// <returns></returns>
        private bool IsInside(FighterPlane plane)
        {
            return plane.Altitude >= MIN_ALT;
        }

        /// <summary>
        /// Returns true if the specified segment age has exceeded the max, or if the associated plane is expired.
        /// </summary>
        /// <param name="segment"></param>
        /// <returns></returns>
        private static bool SegmentIsExpired(TrailSegment segment)
        {
            return segment.Age > MAX_SEG_AGE || segment.Plane.IsExpired;
        }

        private class PlaneTag
        {
            public FighterPlane Plane;
            public D2DPoint PrevPos;

            public PlaneTag(FighterPlane plane)
            {
                this.Plane = plane;
                this.PrevPos = plane.Position;
            }
        }

        private class TrailSegment
        {
            public D2DPoint PointA;
            public D2DPoint PointB;
            public FighterPlane Plane;
            public float Age = 0;

            public TrailSegment() { }

            public void ReInit(FighterPlane plane, D2DPoint pointA, D2DPoint pointB)
            {
                this.Age = 0f;
                this.Plane = plane;
                this.PointA = pointA;
                this.PointB = pointB;
            }
        }
    }
}
