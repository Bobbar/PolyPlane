using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using unvell.D2DLib;

namespace PolyPlane.Helpers
{
    public struct BoundingBox
    {
        public D2DPoint[] BoundsPoly;

        public BoundingBox(D2DPoint[] polygon, float inflateAmount)
        {
            Compute(polygon, inflateAmount);
        }

        private void Compute(D2DPoint[] polygon, float inflateAmount)
        {
            var minMax = new MinMax();

            foreach (var pnt in polygon)
            {
                minMax.Update(pnt.X, pnt.Y);
            }

            var rect = new D2DRect(minMax.MinX, minMax.MinY, minMax.MaxX - minMax.MinX, minMax.MaxY - minMax.MinY);
            rect = rect.Inflate(inflateAmount, inflateAmount);            
            
            BoundsPoly = new D2DPoint[4];
            BoundsPoly[0] = new D2DPoint(rect.left, rect.top);
            BoundsPoly[1] = new D2DPoint(rect.right, rect.top);
            BoundsPoly[2] = new D2DPoint(rect.right, rect.bottom);
            BoundsPoly[3] = new D2DPoint(rect.left, rect.bottom);
        }
    }
}
