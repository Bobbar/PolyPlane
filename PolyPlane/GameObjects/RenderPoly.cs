using PolyPlane.Helpers;

namespace PolyPlane.GameObjects
{
    public class RenderPoly : IFlippable
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

        public RenderPoly(D2DPoint[] polygon, float scale, float tessalateDist = 0f)
        {
            Poly = new D2DPoint[polygon.Length];
            SourcePoly = new D2DPoint[polygon.Length];

            Array.Copy(polygon, Poly, polygon.Length);
            Array.Copy(polygon, SourcePoly, polygon.Length);

            Utilities.ApplyTranslation(Poly, Poly, 0f, D2DPoint.Zero, scale);
            Utilities.ApplyTranslation(SourcePoly, SourcePoly, 0f, D2DPoint.Zero, scale);

            if (tessalateDist > 0f)
                Tessellate(tessalateDist);
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
        /// Adds points between polygon points where the distance is greater than the specified amount.
        /// 
        /// Increases polygon resolution without changing the original shape.
        /// </summary>
        /// <param name="minDist"></param>
        public void Tessellate(float minDist)
        {
            var srcCopy = new List<D2DPoint>();

            // Iterate poly points and add new points as needed.
            for (int i = 0; i < this.SourcePoly.Length; i++)
            {
                var pnt1 = this.SourcePoly[Utilities.WrapIndex(i, this.SourcePoly.Length)];
                var pnt2 = this.SourcePoly[Utilities.WrapIndex(i + 1, this.SourcePoly.Length)];
                var dist = pnt1.DistanceTo(pnt2);
                var dir = (pnt2 - pnt1).Normalized();

                if (dist >= minDist)
                {
                    var num = (int)(dist / minDist);
                    var amt = dist / (float)num;
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

            this.SourcePoly = srcCopy.ToArray();
            this.Poly = new D2DPoint[this.SourcePoly.Length];
            Array.Copy(this.SourcePoly, this.Poly, this.SourcePoly.Length);
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
