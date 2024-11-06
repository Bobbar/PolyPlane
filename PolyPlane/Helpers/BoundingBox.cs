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
        public D2DRect BoundsRect;

        public BoundingBox(D2DPoint[] polygon, float inflateAmount)
        {
            Compute(polygon, inflateAmount);
        }

        /// <summary>
        /// Returns true if any of the specified points are within the bounding box.
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public bool Contains(params D2DPoint[] points) 
        {
            foreach (var pnt in points)
            {
                if (BoundsRect.Contains(pnt)) 
                    return true;
            }

            return false;
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

            BoundsRect = rect;
        }
    }
}
