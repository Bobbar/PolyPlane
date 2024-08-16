using PolyPlane.Helpers;

namespace PolyPlane.GameObjects
{
    public class RenderPoly
    {
        public D2DPoint[] Poly;
        public D2DPoint[] SourcePoly;
        public bool IsFlipped = false;

        public RenderPoly()
        {
            Poly = new D2DPoint[0];
            SourcePoly = new D2DPoint[0];
        }

        public RenderPoly(D2DPoint[] polygon)
        {
            Poly = new D2DPoint[polygon.Length];
            SourcePoly = new D2DPoint[polygon.Length];

            Array.Copy(polygon, Poly, polygon.Length);
            Array.Copy(polygon, SourcePoly, polygon.Length);
        }

        public RenderPoly(D2DPoint[] polygon, D2DPoint offset)
        {
            Poly = new D2DPoint[polygon.Length];
            SourcePoly = new D2DPoint[polygon.Length];

            Array.Copy(polygon, Poly, polygon.Length);
            Array.Copy(polygon, SourcePoly, polygon.Length);


            Utilities.ApplyTranslation(Poly, Poly, 0f, offset);
            Utilities.ApplyTranslation(SourcePoly, SourcePoly, 0f, offset);
        }

        public RenderPoly(D2DPoint[] polygon, D2DPoint offset, float scale)
        {
            Poly = new D2DPoint[polygon.Length];
            SourcePoly = new D2DPoint[polygon.Length];

            Array.Copy(polygon, Poly, polygon.Length);
            Array.Copy(polygon, SourcePoly, polygon.Length);

            Utilities.ApplyTranslation(Poly, Poly, 0f, offset, scale);
            Utilities.ApplyTranslation(SourcePoly, SourcePoly, 0f, offset, scale);
        }

        public RenderPoly(D2DPoint[] polygon, float scale)
        {
            Poly = new D2DPoint[polygon.Length];
            SourcePoly = new D2DPoint[polygon.Length];

            Array.Copy(polygon, Poly, polygon.Length);
            Array.Copy(polygon, SourcePoly, polygon.Length);

            Utilities.ApplyTranslation(Poly, Poly, 0f, D2DPoint.Zero, scale);
            Utilities.ApplyTranslation(SourcePoly, SourcePoly, 0f, D2DPoint.Zero, scale);
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
    }
}
