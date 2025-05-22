using unvell.D2DLib;

namespace PolyPlane.Helpers
{
    /// <summary>
    /// Efficient line clipping algorithms.
    /// 
    /// https://en.wikipedia.org/wiki/Cohen%E2%80%93Sutherland_algorithm
    /// </summary>
    public static class LineClipping
    {
        const int INSIDE = 0b0000;
        const int LEFT = 0b0001;
        const int RIGHT = 0b0010;
        const int BOTTOM = 0b0100;
        const int TOP = 0b1000;

                /// <summary>
        /// Returns true if the specified line points intersect any portion of the specified rectangle.
        /// </summary>
        public static bool CohenSutherlandLineClip(D2DPoint a, D2DPoint b, D2DRect rect)
        {
            return CohenSutherlandLineClip(a.X, a.Y, b.X, b.Y, rect);
        }

        /// <summary>
        /// Returns true if the specified line points intersect any portion of the specified rectangle.
        /// </summary>
        public static bool CohenSutherlandLineClip(float ax, float ay, float bx, float by, D2DRect rect)
        {
            var xmin = rect.left;
            var xmax = rect.right;
            var ymin = rect.top;
            var ymax = rect.bottom;

            // compute outcodes for P0, P1, and whatever point lies outside the clip rectangle
            int outcode0 = ComputeOutCode(ax, ay, rect);
            int outcode1 = ComputeOutCode(bx, by, rect);
            bool accept = false;

            while (true)
            {
                if ((outcode0 | outcode1) == 0)
                {
                    // bitwise OR is 0: both points inside window; trivially accept and exit loop
                    accept = true;
                    break;
                }
                else if ((outcode0 & outcode1) != 0)
                {
                    // bitwise AND is not 0: both points share an outside zone (LEFT, RIGHT, TOP,
                    // or BOTTOM), so both must be outside window; exit loop (accept is false)
                    break;
                }
                else
                {
                    // failed both tests, so calculate the line segment to clip
                    // from an outside point to an intersection with clip edge
                    float x = 0f, y = 0f;

                    // At least one endpoint is outside the clip rectangle; pick it.
                    int outcodeOut = outcode1 > outcode0 ? outcode1 : outcode0;

                    // Now find the intersection point;
                    // use formulas:
                    //   slope = (y1 - y0) / (x1 - x0)
                    //   x = x0 + (1 / slope) * (ym - y0), where ym is ymin or ymax
                    //   y = y0 + slope * (xm - x0), where xm is xmin or xmax
                    // No need to worry about divide-by-zero because, in each case, the
                    // outcode bit being tested guarantees the denominator is non-zero
                    if ((outcodeOut & TOP) != 0)
                    {           // point is above the clip window
                        x = ax + (bx - ax) * (ymax - ay) / (by - ay);
                        y = ymax;
                    }
                    else if ((outcodeOut & BOTTOM) != 0)
                    { // point is below the clip window
                        x = ax + (bx - ax) * (ymin - ay) / (by - ay);
                        y = ymin;
                    }
                    else if ((outcodeOut & RIGHT) != 0)
                    {  // point is to the right of clip window
                        y = ay + (by - ay) * (xmax - ax) / (bx - ax);
                        x = xmax;
                    }
                    else if ((outcodeOut & LEFT) != 0)
                    {   // point is to the left of clip window
                        y = ay + (by - ay) * (xmin - ax) / (bx - ax);
                        x = xmin;
                    }

                    // Now we move outside point to intersection point to clip
                    // and get ready for next pass.
                    if (outcodeOut == outcode0)
                    {
                        ax = x;
                        ay = y;
                        outcode0 = ComputeOutCode(ax, ay, rect);
                    }
                    else
                    {
                        bx = x;
                        by = y;
                        outcode1 = ComputeOutCode(bx, by, rect);
                    }
                }
            }

            return accept;
        }

        private static int ComputeOutCode(float x, float y, D2DRect rect)
        {
            int code = INSIDE;  // initialised as being inside of clip window

            var xmin = rect.left;
            var xmax = rect.right;
            var ymin = rect.top;
            var ymax = rect.bottom;

            if (x < xmin)           // to the left of clip window
                code |= LEFT;
            else if (x > xmax)      // to the right of clip window
                code |= RIGHT;
            if (y < ymin)           // below the clip window
                code |= BOTTOM;
            else if (y > ymax)      // above the clip window
                code |= TOP;

            return code;
        }
    }
}
