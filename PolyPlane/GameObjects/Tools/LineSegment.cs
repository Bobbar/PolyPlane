using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PolyPlane.GameObjects.Tools
{
    public class LineSegment
    {
        public D2DPoint A;
        public D2DPoint B;

        public LineSegment(D2DPoint a, D2DPoint b)
        {
            A = a;
            B = b;
        }
    }
}
