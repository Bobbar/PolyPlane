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
        private const float MAX_ALT = 70000f;
        private const float MAX_SEG_AGE = 60f;
        private const float ALPHA = 0.3f;
        private const float MIN_DIST = 40f;
        private const float MAX_ALPHA_ALT = 3000f;
        private const float TRAIL_WEIGHT = 8f;

        private Dictionary<GameID, PlaneTag> _currentPlanes = new();
        private List<TrailSegment> _segments = new List<TrailSegment>();
        private D2DColor _trailColor = new D2DColor(0.3f, D2DColor.WhiteSmoke);

        public void Update(List<FighterPlane> planes, float dt)
        {
            // Try to compute an offset min distance amount based on time delta.
            // Higher DT == more distance covered between updates,
            // so we need to increase the min distance as DT increases.
            var minDistDT = MIN_DIST + ((MIN_DIST * dt) * 10f);

            foreach (var plane in planes)
            {
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
                                    var seg = new TrailSegment(plane, tag.PrevPos, newPos);
                                    _segments.Add(seg);
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

                    if (seg.Age > MAX_SEG_AGE || seg.Plane.IsExpired)
                        _segments.RemoveAt(i);
                }

                // Prune expired planes.
                foreach (var kvp in _currentPlanes)
                {
                    var tag = kvp.Value;
                    var plane = tag.Plane;

                    if (plane.IsExpired)
                        _currentPlanes.Remove(plane.ID);
                }
            }
        }

        public void Render(RenderContext ctx)
        {
            // Render all segments.
            foreach (var seg in _segments)
            {
                var altFact = GetAltFadeInFactor(seg.PointA);
                var ageFact = (1f - Utilities.Factor(seg.Age, MAX_SEG_AGE));
                var alpha = ALPHA * ageFact * altFact;
                var color = _trailColor.WithAlpha(alpha);

                ctx.DrawLine(seg.PointA, seg.PointB, color, TRAIL_WEIGHT);
            }

            // Draw final connectors between planes and the last segment.
            foreach (var kvp in _currentPlanes)
            {
                var tag = kvp.Value;
                var plane = tag.Plane;

                var altFact = GetAltFadeInFactor(tag.PrevPos);
                var alpha = ALPHA * altFact;
                var color = _trailColor.WithAlpha(alpha);

                if (IsInside(plane) && plane.ThrustAmount > 0f && IsNotInSpace(plane) && !plane.IsDisabled)
                    ctx.DrawLine(tag.PrevPos, plane.ExhaustPosition, color, TRAIL_WEIGHT);
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
            return World.GetDensityAltitude(plane.ExhaustPosition) > 0.01f;
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
            return plane.Altitude >= MIN_ALT && plane.Altitude <= MAX_ALT;
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

            public TrailSegment(FighterPlane plane, D2DPoint pointA, D2DPoint pointB)
            {
                this.Plane = plane;
                this.PointA = pointA;
                this.PointB = pointB;
            }

        }
    }
}
