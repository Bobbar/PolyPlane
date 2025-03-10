using PolyPlane.GameObjects;
using PolyPlane.Helpers;
using unvell.D2DLib;

namespace PolyPlane.Rendering
{
    public sealed class CloudManager
    {
        private List<CloudGeometry> _cloudGeometries = new();
        private List<Cloud> _clouds = new();

        private const int NUM_GEO = 1000;

        public void Update()
        {
            _cloudGeometries.ForEachParallel(c => c.Update(World.CurrentDT));
            _clouds.ForEachParallel(c => c.Update(World.CurrentDT));
        }

        public void Render(RenderContext ctx)
        {
            var todColor = ctx.GetTimeOfDayColor();
            var todAngle = ctx.GetTimeOfDaySunAngle();
            var shadowColor = Utilities.LerpColorWithAlpha(todColor, D2DColor.Black, 0.7f, 0.05f);

            for (int i = 0; i < _clouds.Count; i++)
            {
                var cloud = _clouds[i];

                cloud.Render(ctx, shadowColor, todColor, todAngle);
            }
        }

        public void GenClouds(Random rnd, int num)
        {
            // Generate a pseudo-random? list of clouds.
            // I tried to do clouds procedurally, but wasn't having much luck.
            // It turns out that we need a surprisingly few number of clouds
            // to cover a very large area, so we will just brute force this for now.

            // Generate some geometry to be shared by multiple clouds.
            GenGeometry(rnd, NUM_GEO);
           
            var cloudDeDup = new HashSet<D2DPoint>();
            const int MIN_PNTS = 12;
            const int MAX_PNTS = 28;
            const int MIN_RADIUS = 5;
            const int MAX_RADIUS = 30;

            var cloudRange = World.CloudRangeY;
            var fieldRange = World.FieldXBounds;

            for (int i = 0; i < num; i++)
            {
                var rndPos = new D2DPoint(rnd.NextFloat(fieldRange.X, fieldRange.Y), rnd.NextFloat(cloudRange.X, cloudRange.Y));

                while (!cloudDeDup.Add(rndPos))
                    rndPos = new D2DPoint(rnd.NextFloat(fieldRange.X, fieldRange.Y), rnd.NextFloat(cloudRange.X, cloudRange.Y));

                var rndGeo = _cloudGeometries[rnd.Next(_cloudGeometries.Count)];
                var cloud = new Cloud(rndPos, rndGeo);

                _clouds.Add(cloud);
            }

            // Add a more dense layer near the ground?
            var cloudLayerRangeY = new D2DPoint(-2500, -2000);
            for (int i = 0; i < num / 2; i++)
            {
                var rndPos = new D2DPoint(rnd.NextFloat(fieldRange.X, fieldRange.Y), rnd.NextFloat(cloudLayerRangeY.X, cloudLayerRangeY.Y));

                while (!cloudDeDup.Add(rndPos))
                    rndPos = new D2DPoint(rnd.NextFloat(fieldRange.X, fieldRange.Y), rnd.NextFloat(cloudLayerRangeY.X, cloudLayerRangeY.Y));

                var rndGeo = _cloudGeometries[rnd.Next(_cloudGeometries.Count)];
                var cloud = new Cloud(rndPos, rndGeo);

                _clouds.Add(cloud);
            }
        }

        private void GenGeometry(Random rnd, int num)
        {
            var cloudDeDup = new HashSet<D2DPoint>();
            const int MIN_PNTS = 12;
            const int MAX_PNTS = 28;
            const int MIN_RADIUS = 5;
            const int MAX_RADIUS = 30;

            var cloudRange = World.CloudRangeY;
            var fieldRange = World.FieldXBounds;

            for (int i = 0; i < num; i++)
            {
                var rndPos = new D2DPoint(0f, rnd.NextFloat(cloudRange.X, cloudRange.Y));

                while (!cloudDeDup.Add(rndPos))
                    rndPos = new D2DPoint(0f, rnd.NextFloat(cloudRange.X, cloudRange.Y));

                var geo = CloudGeometry.RandomGeometry(rnd, rndPos, MIN_PNTS, MAX_PNTS, MIN_RADIUS, MAX_RADIUS);
                _cloudGeometries.Add(geo);
            }
        }
    }

    public class CloudGeometry
    {
        public D2DPoint[] Points = Array.Empty<D2DPoint>();
        public List<D2DSize> Dims = new List<D2DSize>();
        public float Rotation = 0f;
        public float RotationDirection = 1f;
        public float ScaleX = 1f;
        public float ScaleY = 1f;
        public float Radius;
        public MinMax TranslatedDims = new MinMax();

        private readonly D2DPoint[] _pointsOrigin = Array.Empty<D2DPoint>();

        public CloudGeometry(D2DPoint position, List<D2DPoint> points, List<D2DSize> dims, float scaleX, float scaleY, float radius)
        {
            _pointsOrigin = points.ToArray();
            Points = points.ToArray();
            Dims = dims;
            ScaleX = scaleX;
            ScaleY = scaleY;
            Radius = radius;
        }

        public void Update(float dt)
        {
            this.Rotation = Utilities.ClampAngle(this.Rotation + (0.8f * this.RotationDirection) * dt);

            // Apply translations.
            Utilities.ApplyTranslation(this._pointsOrigin, this.Points, this.Rotation, D2DPoint.Zero, World.CLOUD_SCALE);
            Utilities.ApplyTranslation(this.Points, this.Points, D2DPoint.Zero, 0f, D2DPoint.Zero, this.ScaleX, this.ScaleY);

            // Update translated dimensions for rendering purposes.
            TranslatedDims.Reset();
            TranslatedDims.Update(this.Points);
        }

        public static CloudGeometry RandomGeometry(Random rnd, D2DPoint position, int minPoints, int maxPoints, int minRadius, int maxRadius)
        {
            const float MAX_ALT = 30000f;
            const float MIN_DIMS_X = 60f;
            const float MAX_DIMS_X = 100f;
            const float MIN_DIMS_Y = 50f;
            const float MAX_DIMS_Y = 70f;

            const float ALT_FACT_AMT = 30f;
            var nPnts = rnd.Next(minPoints, maxPoints);
            var radius = rnd.Next(minRadius, maxRadius);
            var dims = new List<D2DSize>();

            for (int i = 0; i < nPnts; i++)
            {
                var dimsX = rnd.NextFloat(MIN_DIMS_X, MAX_DIMS_X);
                var dimsY = rnd.NextFloat(MIN_DIMS_Y, MAX_DIMS_Y);

                dims.Add(new D2DSize(dimsX, dimsY));
            }

            var rotation = 0f;
            var poly = GameObjectPoly.RandomPoly(nPnts, radius);
            var pnts = poly.ToList();

            var scaleX = rnd.NextFloat(1.5f, 5f);
            var scaleY = rnd.NextFloat(0.4f, 0.7f);

            var geo = new CloudGeometry(position, pnts, dims, scaleX, scaleY, radius);

            // Fiddle rotation direction.
            if (nPnts % 2 == 0)
                geo.RotationDirection = -1f;

            return geo;
        }
    }
}
