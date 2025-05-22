using System.Numerics;
using unvell.D2DLib;

namespace PolyPlane.Helpers
{
    public class MinMax
    {
        public float MinX => _minXY.X;
        public float MinY => _minXY.Y;
        public float MaxX => _maxXY.X;
        public float MaxY => _maxXY.Y;

        private Vector2 _minXY = Vector2.Zero;
        private Vector2 _maxXY = Vector2.Zero;

        public float Width
        {
            get
            {
                return Math.Abs(MinX - MaxX);
            }
        }

        public float Height
        {
            get
            {
                return Math.Abs(MinY - MaxY);
            }
        }

        public MinMax()
        {
            Reset();
        }

        /// <summary>
        /// Returns a rectangle representing the current bounds of the min/max coordinates.
        /// </summary>
        public D2DRect GetBounds()
        {
            return new D2DRect(MinX, MinY, MaxX - MinX, MaxY - MinY);
        }

        public void Update(D2DPoint[] points)
        {
            for (int i = points.Length - 1; i >= 0; i--)
            {
                var pnt = points[i];
                _minXY = Vector2.Min(_minXY, pnt);
                _maxXY = Vector2.Max(_maxXY, pnt);
            }
        }

        public void Update(D2DPoint point)
        {
            _minXY = Vector2.Min(_minXY, point);
            _maxXY = Vector2.Max(_maxXY, point);
        }

        public void Reset()
        {
            _minXY = new Vector2(float.MaxValue, float.MaxValue);
            _maxXY = new Vector2(float.MinValue, float.MinValue);
        }
    }
}
