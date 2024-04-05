using PolyPlane.GameObjects;
using unvell.D2DLib;

namespace PolyPlane.Rendering
{
    public class Cloud
    {
        public D2DPoint Position;
        public List<D2DPoint> Points = new List<D2DPoint>();
        public List<D2DPoint> Dims = new List<D2DPoint>();
        public float Rotation = 0f;
        public D2DColor Color;
        public float Radius = 0f;

        public Cloud(D2DPoint position, List<D2DPoint> points, List<D2DPoint> dims, float rotation, D2DColor color)
        {
            Position = position;
            Points = points;
            Dims = dims;
            Rotation = rotation;
            Color = color;
        }

        public static Cloud RandomCloud(Random rnd, D2DPoint position, int minPoints, int maxPoints, int minRadius, int maxRadius)
        {
            const float MAX_ALT = 30000f;
            const float MIN_DIMS = 30f;
            const float MAX_DIMS = 50f;
            const float ALT_FACT_AMT = 30f;
            var nPnts = rnd.Next(minPoints, maxPoints);
            var radius = rnd.Next(minRadius, maxRadius);
            var dims = new List<D2DPoint>();


            // Try to make clouds at higher altitude more thin and whispy?
            var altFact = Helpers.Factor(Math.Abs(position.Y), MAX_ALT);

            for (int i = 0; i < nPnts; i++)
                dims.Add(new D2DPoint(rnd.NextFloat(MIN_DIMS + (altFact * ALT_FACT_AMT), MAX_DIMS + (altFact * ALT_FACT_AMT)), rnd.NextFloat(MIN_DIMS - (altFact * ALT_FACT_AMT), MAX_DIMS - (altFact * ALT_FACT_AMT))));

            var rotation = rnd.NextFloat(0, 360);
            var pnts = GameObjectPoly.RandomPoly(nPnts, radius).ToList();
            var color1 = new D2DColor(0.2f, D2DColor.White);
            var color2 = new D2DColor(0.2f, D2DColor.Gray);
            var color = Helpers.LerpColor(color1, color2, rnd.NextFloat(0f, 1f));

            var newCloud = new Cloud(position, pnts, dims, rotation, color);
            newCloud.Radius = radius;

            return newCloud;
        }
    }
}
