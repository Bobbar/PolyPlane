using PolyPlane.GameObjects;
using PolyPlane.Helpers;

namespace PolyPlane.Rendering
{
    public class Cloud
    {
        public D2DPoint Position;
        public readonly D2DPoint[] PointsOrigin = Array.Empty<D2DPoint>();
        public D2DPoint[] Points = Array.Empty<D2DPoint>();
        public List<D2DPoint> Dims = new List<D2DPoint>();
        public float Rotation = 0f;
        public float Radius = 0f;
        public float ScaleX = 1f;
        public float ScaleY = 1f;

        public Cloud(D2DPoint position, List<D2DPoint> points, List<D2DPoint> dims, float rotation)
        {
            Position = position;
            PointsOrigin = points.ToArray();
            Points = points.ToArray();
            Dims = dims;
            Rotation = rotation;
        }

        public static Cloud RandomCloud(Random rnd, D2DPoint position, int minPoints, int maxPoints, int minRadius, int maxRadius)
        {
            const float MAX_ALT = 30000f;
            const float MIN_DIMS_X = 60f;
            const float MAX_DIMS_X = 100f;
            const float MIN_DIMS_Y = 50f;
            const float MAX_DIMS_Y = 70f;

            const float ALT_FACT_AMT = 30f;
            var nPnts = rnd.Next(minPoints, maxPoints);
            var radius = rnd.Next(minRadius, maxRadius);
            var dims = new List<D2DPoint>();

            // Try to make clouds at higher altitude more thin and whispy?
            var altFact = Utilities.Factor(Math.Abs(position.Y), MAX_ALT);

            for (int i = 0; i < nPnts; i++)
            {
                var dimsX = rnd.NextFloat(MIN_DIMS_X + (altFact * ALT_FACT_AMT), MAX_DIMS_X + (altFact * ALT_FACT_AMT));
                var dimsY = rnd.NextFloat(MIN_DIMS_Y - (altFact * ALT_FACT_AMT), MAX_DIMS_Y - (altFact * ALT_FACT_AMT));
                dims.Add(new D2DPoint(dimsX, dimsY));
            }

            var rotation = 0f;
            var poly = GameObjectPoly.RandomPoly(nPnts, radius);
            var pnts = poly.ToList();

            var newCloud = new Cloud(position, pnts, dims, rotation);
            newCloud.Radius = radius;
            newCloud.ScaleX = rnd.NextFloat(1.5f, 5f);
            newCloud.ScaleY = rnd.NextFloat(0.4f, 0.7f);

            return newCloud;
        }
    }
}
