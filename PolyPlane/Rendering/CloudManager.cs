using PolyPlane.Helpers;
using unvell.D2DLib;

namespace PolyPlane.Rendering
{
    public sealed class CloudManager
    {
        private List<CloudGeometry> _cloudGeometries = new();
        private List<Cloud> _clouds = new();
        private SpatialGrid<Cloud> _cloudGrid = new SpatialGrid<Cloud>(c => c.Position, c => false, 13);

        private const int NUM_GEO = 500;

        public void Update(float dt)
        {
            _cloudGeometries.ForEachParallel(c => c.Update(dt));
            _clouds.ForEachParallel(c => c.Update(dt));
            _cloudGrid.Update();
        }

        public void Render(RenderContext ctx)
        {
            // Increase the width inflate factor with zoom level.
            float InflateFactorWidth = Math.Clamp(9f * (1f - Utilities.FactorWithEasing(World.ViewPortScaleMulti, 95f, EasingFunctions.Out.EaseCircle)), 1f, 11f);

            var todColor = ctx.GetTimeOfDayColor();
            var todAngle = ctx.GetTimeOfDaySunAngle();
            var shadowColor = Utilities.LerpColorWithAlpha(todColor, D2DColor.Black, 0.7f, 0.05f);

            // Inflate the viewport to ensure off-screen clouds with shadow rays are included.
            var viewPortInflated = ctx.Viewport.Inflate(ctx.Viewport.Width * InflateFactorWidth, ctx.Viewport.Height);

            // Clamp the top of the viewport so that it is never lower than the lowest shawdow clouds.
            if (viewPortInflated.top > -Cloud.MAX_SHADOW_ALT)
                viewPortInflated.top = -Cloud.MAX_SHADOW_ALT;

            var inViewPort = _cloudGrid.GetInViewport(viewPortInflated).OrderBy(c => c.OrderIndex);
            foreach (var cloud in inViewPort)
            {
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
            var cloudRange = World.CloudRangeY;
            var fieldRange = World.FieldXBounds;

            for (int i = 0; i < num; i++)
            {
                var rndPos = new D2DPoint(rnd.NextFloat(-fieldRange, fieldRange), rnd.NextFloat(cloudRange.X, cloudRange.Y));

                while (!cloudDeDup.Add(rndPos))
                    rndPos = new D2DPoint(rnd.NextFloat(-fieldRange, fieldRange), rnd.NextFloat(cloudRange.X, cloudRange.Y));

                var rndGeo = _cloudGeometries[rnd.Next(_cloudGeometries.Count)];

                rndGeo = DeDupGeo(rnd, rndGeo, rndPos);

                var cloud = new Cloud(rndPos, rndGeo);

                _clouds.Add(cloud);
                _cloudGrid.Add(cloud);
            }

            // Add a more dense layer near the ground?
            var cloudLayerRangeY = new D2DPoint(-2500, -2000);
            for (int i = 0; i < num / 2; i++)
            {
                var rndPos = new D2DPoint(rnd.NextFloat(-fieldRange, fieldRange), rnd.NextFloat(cloudLayerRangeY.X, cloudLayerRangeY.Y));

                while (!cloudDeDup.Add(rndPos))
                    rndPos = new D2DPoint(rnd.NextFloat(-fieldRange, fieldRange), rnd.NextFloat(cloudLayerRangeY.X, cloudLayerRangeY.Y));

                var rndGeo = _cloudGeometries[rnd.Next(_cloudGeometries.Count)];

                rndGeo = DeDupGeo(rnd, rndGeo, rndPos);

                var cloud = new Cloud(rndPos, rndGeo);

                _clouds.Add(cloud);
                _cloudGrid.Add(cloud);
            }
        }

        /// <summary>
        /// Replaces the specified geometry with a new randomly picked geometry if the specified position is too close to another cloud with the same geometry.
        /// </summary>
        private CloudGeometry DeDupGeo(Random rnd, CloudGeometry geo, D2DPoint pos)
        {
            const float MIN_DIST = 10000f;

            var sameGeo = _clouds.Where(c => c.Geometry.ID == geo.ID);

            if (sameGeo.Any())
            {
                var nearest = sameGeo.Min(c => c.Position.DistanceTo(pos));

                while (nearest <= MIN_DIST)
                {
                    geo = _cloudGeometries[rnd.Next(_cloudGeometries.Count)];
                    sameGeo = _clouds.Where(c => c.Geometry.ID == geo.ID);

                    if (!sameGeo.Any())
                        break;

                    nearest = sameGeo.Min(c => c.Position.DistanceTo(pos));
                }
            }

            return geo;
        }

        private void GenGeometry(Random rnd, int num)
        {
            const int MIN_PNTS = 12;
            const int MAX_PNTS = 28;
            const int MIN_RADIUS = 10;
            const int MAX_RADIUS = 30;

            for (int i = 0; i < num; i++)
            {
                var geo = CloudGeometry.RandomGeometry(rnd, MIN_PNTS, MAX_PNTS, MIN_RADIUS, MAX_RADIUS);
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
        public int ID = 0;

        private readonly D2DPoint[] _pointsOrigin = Array.Empty<D2DPoint>();
        private static int _geoID = 0;

        public CloudGeometry(List<D2DPoint> points, List<D2DSize> dims, float scaleX, float scaleY, float radius)
        {
            _pointsOrigin = points.ToArray();
            Points = points.ToArray();
            Dims = dims;
            ScaleX = scaleX;
            ScaleY = scaleY;
            Radius = radius;

            _geoID++;
            ID = _geoID;
        }

        public void Update(float dt)
        {
            this.Rotation = Utilities.ClampAngle(this.Rotation + (0.8f * this.RotationDirection) * dt);

            // Apply translations.
            this._pointsOrigin.Translate(this.Points, this.Rotation, D2DPoint.Zero, World.CLOUD_SCALE);
            this.Points.Translate(this.Points, D2DPoint.Zero, 0f, D2DPoint.Zero, this.ScaleX, this.ScaleY);

            // Update translated dimensions for rendering purposes.
            TranslatedDims.Reset();
            TranslatedDims.Update(this.Points);
        }

        public static CloudGeometry RandomGeometry(Random rnd, int minPoints, int maxPoints, int minRadius, int maxRadius)
        {
            const float MAX_ALT = 30000f;
            const float MIN_DIMS_X = 60f;
            const float MAX_DIMS_X = 100f;
            const float MIN_DIMS_Y = 50f;
            const float MAX_DIMS_Y = 70f;

            const float ALT_FACT_AMT = 30f;
            var radius = rnd.Next(minRadius, maxRadius);
            var nPnts = (int)radius;
            var dims = new List<D2DSize>();

            for (int i = 0; i < nPnts; i++)
            {
                var dimsX = rnd.NextFloat(MIN_DIMS_X, MAX_DIMS_X);
                var dimsY = rnd.NextFloat(MIN_DIMS_Y, MAX_DIMS_Y);

                dims.Add(new D2DSize(dimsX, dimsY));
            }

            var poly = Utilities.RandomPoly(nPnts, radius);
            var pnts = poly.ToList();

            var scaleX = rnd.NextFloat(2f, 5f);
            var scaleY = rnd.NextFloat(0.4f, 0.7f);

            var geo = new CloudGeometry(pnts, dims, scaleX, scaleY, radius);

            // Fiddle rotation direction.
            if (nPnts % 2 == 0)
                geo.RotationDirection = -1f;

            return geo;
        }
    }
}
