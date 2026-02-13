using PolyPlane.GameObjects.Interfaces;
using PolyPlane.Helpers;

namespace PolyPlane.GameObjects.Tools
{
    public sealed class RenderPoly : IFlippable
    {
        public D2DPoint[] Poly;
        public D2DPoint[] SourcePoly;
        public bool IsFlipped = false;
        public GameObject ParentObject;

        private bool _isDistortable = false;
        private D2DPoint[] _originalPoly;

        public D2DPoint Position => ParentObject.Position;

        public RenderPoly(GameObject parent)
        {
            ParentObject = parent;
            Poly = new D2DPoint[0];
            SourcePoly = new D2DPoint[0];
            _originalPoly = new D2DPoint[0];
        }

        public RenderPoly(RenderPoly copyPoly, D2DPoint pos, float rotation, bool distortable = false) : this(copyPoly.ParentObject)
        {
            InitPolyArrays(copyPoly.SourcePoly, D2DPoint.Zero, 1f, distortable);

            this.Update(pos, rotation, copyPoly.ParentObject.RenderScale);
        }

        public RenderPoly(GameObject parent, D2DPoint[] polygon) : this(parent)
        {
            InitPolyArrays(polygon, D2DPoint.Zero);

            this.Update();
        }

        public RenderPoly(GameObject parent, D2DPoint[] polygon, D2DPoint offset) : this(parent)
        {
            InitPolyArrays(polygon, offset);

            this.Update();
        }

        public RenderPoly(GameObject parent, D2DPoint[] polygon, D2DPoint offset, float scale) : this(parent)
        {
            InitPolyArrays(polygon, offset, scale);

            this.Update();
        }

        public RenderPoly(GameObject parent, D2DPoint[] polygon, float scale, float tessalateDist, bool distortable = false) : this(parent)
        {
            InitPolyArrays(polygon, D2DPoint.Zero, scale, distortable);

            if (tessalateDist > 0f)
                IncreaseResolution(tessalateDist);

            this.Update();
        }

        internal void InitPolyArrays(D2DPoint[] polygon, D2DPoint offset, float scale = 1f, bool distortable = false)
        {
            Poly = new D2DPoint[polygon.Length];
            SourcePoly = new D2DPoint[polygon.Length];

            Array.Copy(polygon, SourcePoly, polygon.Length);
            Array.Copy(polygon, Poly, polygon.Length);

            if (distortable)
            {
                _originalPoly = new D2DPoint[polygon.Length];
                Array.Copy(polygon, _originalPoly, polygon.Length);
                _isDistortable = true;
            }

            Poly.Translate(Poly, 0f, offset, scale);
            SourcePoly.Translate(SourcePoly, 0f, offset, scale);
        }

        /// <summary>
        /// Finds the index of the polygon point which is closest to the specified point.
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public int ClosestIdx(D2DPoint point)
        {
            int idx = 0;
            float minDist = float.MaxValue;

            for (int i = 0; i < SourcePoly.Length; i++)
            {
                var pnt = SourcePoly[i];
                var dist = point.DistanceSquaredTo(pnt);

                if (dist < minDist)
                {
                    minDist = dist;
                    idx = i;
                }
            }

            return idx;
        }

        /// <summary>
        /// Returns a list of line segments representing the faces of the polygon which are facing the specified direction.
        /// </summary>
        /// <param name="direction">Angle in degrees.</param>
        /// <returns></returns>
        public IEnumerable<LineSegment> GetSidesFacingDirection(float direction)
        {
            const float FACING_ANGLE = 90f;

            // Determine if the polygon is clockwise or counter-clockwise.
            bool clockwise = IsClockwise();

            for (int i = 0; i < Poly.Length; i++)
            {
                var idx1 = Utilities.WrapIndex(i, Poly.Length);
                var idx2 = Utilities.WrapIndex(i + 1, Poly.Length);

                var pnt1 = Poly[idx1];
                var pnt2 = Poly[idx2];

                // Compute the normal of the current segment.
                var dirNorm = (pnt1 - pnt2).Normalized();
                var tangent = dirNorm.Tangent(clockwise: clockwise);  // Choose CW/CCW tangent as needed.
                var tangentAngle = tangent.Angle();

                // Compare the angle of the normal with the specified direction.
                // If the difference is less than 90 degrees, we have a valid face.
                var diff = Utilities.AngleDiff(direction, tangentAngle);
                if (diff < FACING_ANGLE)
                {
                    yield return new LineSegment(pnt1, pnt2);
                }
            }
        }

        /// <summary>
        /// Returns a list of line segments representing the faces of the polygon which are facing the specified direction.
        /// </summary>
        /// <param name="direction">Vector representing the direction to test for.</param>
        /// <returns></returns>
        public IEnumerable<LineSegment> GetSidesFacingDirection(D2DPoint direction)
        {
            // Determine if the polygon is clockwise or counter-clockwise.
            bool clockwise = IsClockwise();

            for (int i = 0; i < Poly.Length; i++)
            {
                var idx1 = Utilities.WrapIndex(i, Poly.Length);
                var idx2 = Utilities.WrapIndex(i + 1, Poly.Length);

                var pnt1 = Poly[idx1];
                var pnt2 = Poly[idx2];

                // Compute the direction of the current segment.
                var segDir = (pnt1 - pnt2).Normalized();

                // Flip for CW.
                if (clockwise)
                    segDir *= -1f;

                // Valid face if cross product is greater than zero.
                var diff = (segDir.Cross(direction));
                if (diff > 0f)
                {
                    yield return new LineSegment(pnt1, pnt2);
                }
            }
        }

        /// <summary>
        /// Returns a list of points for the faces of the polygon which are facing the specified direction.
        /// </summary>
        /// <param name="direction">Angle in degrees.</param>
        /// <returns></returns>
        public IEnumerable<D2DPoint> GetPointsFacingDirection(float direction)
        {
            const float FACING_ANGLE = 90f;

            // Determine if the polygon is clockwise or counter-clockwise.
            bool clockwise = IsClockwise();

            for (int i = 0; i < Poly.Length; i++)
            {
                var idx1 = Utilities.WrapIndex(i, Poly.Length);
                var idx2 = Utilities.WrapIndex(i + 1, Poly.Length);

                var pnt1 = Poly[idx1];
                var pnt2 = Poly[idx2];

                // Compute the normal of the current segment.
                var dirNorm = (pnt1 - pnt2).Normalized();
                var tangent = dirNorm.Tangent(clockwise: clockwise);  // Choose CW/CCW tangent as needed.
                var tangentAngle = tangent.Angle();

                // Compare the angle of the normal with the specified direction.
                // If the difference is less than 90 degrees, we have a valid face.
                var diff = Utilities.AngleDiff(direction, tangentAngle);
                if (diff < FACING_ANGLE)
                {
                    yield return pnt1;
                }
            }
        }

        /// <summary>
        /// Returns a list of points for the faces of the polygon which are facing the specified direction.
        /// </summary>
        /// <param name="direction">Vector representing the direction to test for.</param>
        /// <returns></returns>
        public IEnumerable<D2DPoint> GetPointsFacingDirection(D2DPoint direction)
        {
            // Determine if the polygon is clockwise or counter-clockwise.
            bool clockwise = IsClockwise();

            for (int i = 0; i < Poly.Length; i++)
            {
                var idx1 = Utilities.WrapIndex(i, Poly.Length);
                var idx2 = Utilities.WrapIndex(i + 1, Poly.Length);

                var pnt1 = Poly[idx1];
                var pnt2 = Poly[idx2];

                // Compute the direction of the current segment.
                var segDir = (pnt1 - pnt2).Normalized();

                // Flip for CW.
                if (clockwise)
                    segDir *= -1f;

                // Valid point if cross product is greater than zero.
                var diff = (segDir.Cross(direction));
                if (diff > 0f)
                {
                    yield return pnt1;
                }
            }
        }

        /// <summary>
        /// Performs a polygon winding algorithm and returns true if the polygon is wound in the clockwise direction.
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// Credit: https://stackoverflow.com/a/1165943/8581226
        /// https://element84.com/software-engineering/web-development/determining-the-winding-of-a-polygon-given-as-a-set-of-ordered-points/
        /// </remarks>
        public bool IsClockwise()
        {
            float sum = 0f;

            for (int i = 0; i < Poly.Length; i++)
            {
                var idx1 = Utilities.WrapIndex(i, Poly.Length);
                var idx2 = Utilities.WrapIndex(i + 1, Poly.Length);

                var pnt1 = Poly[idx1];
                var pnt2 = Poly[idx2];

                var edge = (pnt2.X - pnt1.X) * (pnt2.Y + pnt1.Y);
                sum += edge;
            }

            return sum > 0f;
        }

        /// <summary>
        /// Adds points between polygon points where the distance is greater than the specified amount.
        /// 
        /// Increases polygon resolution (number of points/verts) without changing the original shape.
        /// </summary>
        /// <param name="minDist"></param>
        public void IncreaseResolution(float minDist)
        {
            var srcCopy = new List<D2DPoint>();

            // Iterate poly points and add new points as needed.
            for (int i = 0; i < SourcePoly.Length; i++)
            {
                var pnt1 = SourcePoly[Utilities.WrapIndex(i, SourcePoly.Length)];
                var pnt2 = SourcePoly[Utilities.WrapIndex(i + 1, SourcePoly.Length)];
                var dist = pnt1.DistanceTo(pnt2);
                var dir = (pnt2 - pnt1).Normalized();

                if (dist >= minDist)
                {
                    var num = (int)(dist / minDist);
                    var amt = dist / num;
                    var pos = pnt1;

                    for (int j = 0; j < num; j++)
                    {
                        srcCopy.Add(pos);
                        pos += dir * amt;
                    }
                }
                else
                {
                    srcCopy.Add(pnt1);
                }
            }

            SourcePoly = srcCopy.ToArray();
            Poly = new D2DPoint[SourcePoly.Length];
            Array.Copy(SourcePoly, Poly, SourcePoly.Length);

            if (_isDistortable)
            {
                _originalPoly = new D2DPoint[SourcePoly.Length];
                Array.Copy(SourcePoly, _originalPoly, SourcePoly.Length);
            }
        }

        /// <summary>
        /// Distorts 3 vertices closest to the specified position in the direction and magnitude of the specified vector.
        /// </summary>
        /// <param name="position">Position within the polygon (at origin) to add the distortion.</param>
        /// <param name="distortVec">Vector containing the magnitude and direction of the distortion.</param>
        public void Distort(D2DPoint position, D2DPoint distortVec)
        {
            if (!_isDistortable)
                return;

            // Find the closest poly point to the impact and distort the polygon.
            var closestIdx = ClosestIdx(position);

            // Distort the closest point and the two surrounding points.
            var prevIdx = Utilities.WrapIndex(closestIdx - 1, SourcePoly.Length);
            var nextIdx = Utilities.WrapIndex(closestIdx + 1, SourcePoly.Length);

            SourcePoly[prevIdx] += distortVec * 0.6f;
            SourcePoly[closestIdx] += distortVec;
            SourcePoly[nextIdx] += distortVec * 0.6f;

            Update();
        }

        /// <summary>
        /// Undo any distortion and restore to the original shape.
        /// </summary>
        public void Restore()
        {
            if (!_isDistortable)
                return;

            for (int i = 0; i < SourcePoly.Length; i++)
                SourcePoly[i] = _originalPoly[i];

            Update();
        }

        /// <summary>
        /// Flips the polygon along the Y axis.
        /// </summary>
        public void FlipY()
        {
            IsFlipped = !IsFlipped;

            for (int i = 0; i < Poly.Length; i++)
            {
                SourcePoly[i].Y = -SourcePoly[i].Y;

                if (_isDistortable)
                    _originalPoly[i].Y = -_originalPoly[i].Y;
            }

            this.Update();
        }

        public void Update(D2DPoint pos, float rotation, float scale)
        {
            SourcePoly.Translate(Poly, rotation, pos, scale);
        }

        public void Update()
        {
            this.Update(this.ParentObject.Position, this.ParentObject.Rotation, this.ParentObject.RenderScale);
        }

    }
}
