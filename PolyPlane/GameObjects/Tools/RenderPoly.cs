using PolyPlane.GameObjects.Interfaces;
using PolyPlane.Helpers;

namespace PolyPlane.GameObjects.Tools
{
    public class RenderPoly : IFlippable
    {
        public D2DPoint[] Poly;
        public D2DPoint[] SourcePoly;
        public bool IsFlipped = false;
        public GameObject ParentObject;

        public D2DPoint Position => ParentObject.Position;

        public RenderPoly(GameObject parent)
        {
            ParentObject = parent;
            Poly = new D2DPoint[0];
            SourcePoly = new D2DPoint[0];
        }

        public RenderPoly(RenderPoly copyPoly, D2DPoint pos, float rotation) : this(copyPoly.ParentObject)
        {
            Poly = new D2DPoint[copyPoly.SourcePoly.Length];
            SourcePoly = new D2DPoint[copyPoly.SourcePoly.Length];

            Array.Copy(copyPoly.SourcePoly, Poly, copyPoly.SourcePoly.Length);
            Array.Copy(copyPoly.SourcePoly, SourcePoly, copyPoly.SourcePoly.Length);

            this.Update(pos, rotation, copyPoly.ParentObject.RenderScale);
        }

        public RenderPoly(GameObject parent, D2DPoint[] polygon) : this(parent)
        {
            Poly = new D2DPoint[polygon.Length];
            SourcePoly = new D2DPoint[polygon.Length];

            Array.Copy(polygon, Poly, polygon.Length);
            Array.Copy(polygon, SourcePoly, polygon.Length);

            this.Update();
        }

        public RenderPoly(GameObject parent, D2DPoint[] polygon, D2DPoint offset) : this(parent)
        {
            Poly = new D2DPoint[polygon.Length];
            SourcePoly = new D2DPoint[polygon.Length];

            Array.Copy(polygon, Poly, polygon.Length);
            Array.Copy(polygon, SourcePoly, polygon.Length);


            Utilities.ApplyTranslation(Poly, Poly, 0f, offset);
            Utilities.ApplyTranslation(SourcePoly, SourcePoly, 0f, offset);

            this.Update();
        }

        public RenderPoly(GameObject parent, D2DPoint[] polygon, D2DPoint offset, float scale) : this(parent)
        {
            Poly = new D2DPoint[polygon.Length];
            SourcePoly = new D2DPoint[polygon.Length];

            Array.Copy(polygon, Poly, polygon.Length);
            Array.Copy(polygon, SourcePoly, polygon.Length);

            Utilities.ApplyTranslation(Poly, Poly, 0f, offset, scale);
            Utilities.ApplyTranslation(SourcePoly, SourcePoly, 0f, offset, scale);

            this.Update();
        }

        public RenderPoly(GameObject parent, D2DPoint[] polygon, float scale, float tessalateDist = 0f) : this(parent)
        {
            Poly = new D2DPoint[polygon.Length];
            SourcePoly = new D2DPoint[polygon.Length];

            Array.Copy(polygon, Poly, polygon.Length);
            Array.Copy(polygon, SourcePoly, polygon.Length);

            Utilities.ApplyTranslation(Poly, Poly, 0f, D2DPoint.Zero, scale);
            Utilities.ApplyTranslation(SourcePoly, SourcePoly, 0f, D2DPoint.Zero, scale);

            if (tessalateDist > 0f)
                Tessellate(tessalateDist);

            this.Update();
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
                var dist = point.DistanceTo(pnt);

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
        /// <param name="reverseTangent">True to invert normals. Depends on polygon direction. (CCW vs CW)</param>
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
                if (diff <= FACING_ANGLE)
                {
                    yield return new LineSegment(pnt1, pnt2);
                }
            }
        }


        /// <summary>
        /// Returns a list of points for the faces of the polygon which are facing the specified direction.
        /// </summary>
        /// <param name="direction">Angle in degrees.</param>
        /// <param name="reverseTangent">True to invert normals. Depends on polygon direction. (CCW vs CW)</param>
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
                if (diff <= FACING_ANGLE)
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
        private bool IsClockwise()
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
        /// Increases polygon resolution without changing the original shape.
        /// </summary>
        /// <param name="minDist"></param>
        public void Tessellate(float minDist)
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
            }
        }

        public void Update(D2DPoint pos, float rotation, float scale)
        {
            Utilities.ApplyTranslation(SourcePoly, Poly, rotation, pos, scale);
        }

        public void Update()
        {
            this.Update(this.ParentObject.Position, this.ParentObject.Rotation, this.ParentObject.RenderScale);
        }

    }
}
