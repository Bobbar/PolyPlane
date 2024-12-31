namespace PolyPlane.Helpers
{
    public class MinMax
    {
        public float MinX = float.MaxValue;
        public float MinY = float.MaxValue;
        public float MaxX = float.MinValue;
        public float MaxY = float.MinValue;

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
            MinX = float.MaxValue;
            MinY = float.MaxValue;
            MaxX = float.MinValue;
            MaxY = float.MinValue;
        }

        public MinMax(float minX, float minY, float maxX, float maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        public void Update(D2DPoint[] points)
        {
            for (int i = points.Length - 1; i >= 0; i--)
                Update(points[i]);
        }

        public void Update(D2DPoint point)
        {
            Update(point.X, point.Y);
        }

        public void Update(float x, float y)
        {
            MinX = Math.Min(MinX, x);
            MinY = Math.Min(MinY, y);
            MaxX = Math.Max(MaxX, x);
            MaxY = Math.Max(MaxY, y);
        }

        public void Update(MinMax minMax)
        {
            MinX = Math.Min(MinX, minMax.MinX);
            MinY = Math.Min(MinY, minMax.MinY);
            MaxX = Math.Max(MaxX, minMax.MaxX);
            MaxY = Math.Max(MaxY, minMax.MaxY);
        }

        public void Reset()
        {
            MinX = float.MaxValue;
            MinY = float.MaxValue;
            MaxX = float.MinValue;
            MaxY = float.MinValue;
        }
    }
}
