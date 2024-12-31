using PolyPlane.GameObjects;
using PolyPlane.Helpers;
using unvell.D2DLib;

namespace PolyPlane.Rendering
{
    public class Cloud
    {
        public D2DPoint Position;
        public readonly D2DPoint[] PointsOrigin = Array.Empty<D2DPoint>();
        public D2DPoint[] Points = Array.Empty<D2DPoint>();
        public List<D2DSize> Dims = new List<D2DSize>();

        public float Rotation = 0f;
        public float Radius = 0f;
        public float ScaleX = 1f;
        public float ScaleY = 1f;
        public float RotationDirection = 1f;

        private MinMax _translatedDims = new MinMax();
        private static D2DPoint[] _shadowRayPoly = new D2DPoint[4];
        private readonly D2DColor _cloudColorLight = D2DColor.WhiteSmoke;
        private readonly D2DColor _cloudColorDark = new D2DColor(1f, 0.6f, 0.6f, 0.6f);
        private const float LIGHT_INTENSITY = 0.7f;

        public Cloud(D2DPoint position, List<D2DPoint> points, List<D2DSize> dims, float rotation)
        {
            Position = position;
            PointsOrigin = points.ToArray();
            Points = points.ToArray();
            Dims = dims;
            Rotation = rotation;
        }

        public void Render(RenderContext ctx, D2DColor shadowColor, D2DColor todColor, float todAngle)
        {
            DrawCloudGroundShadowAndRay(ctx, shadowColor, todAngle);

            if (ctx.Viewport.Contains(this.Position, (this.Radius * 2f) * this.ScaleX * World.CLOUD_SCALE))
                DrawCloud(ctx, todColor);
        }

        public void Update(float dt)
        {
            var altFact = 30f * Utilities.Factor(Utilities.PositionToAltitude(this.Position), World.CloudRangeY.X * -1f); // Higher clouds move slower?
            var sizeOffset = (this.Radius / 2f); // Smaller clouds move slightly faster?
            var rate = Math.Clamp((World.CLOUD_MOVE_RATE - altFact) - sizeOffset, 0.1f, World.CLOUD_MOVE_RATE);

            this.Position.X += rate * dt;
            this.Rotation = Utilities.ClampAngle(this.Rotation + (0.8f * this.RotationDirection) * dt);

            // Wrap clouds.
            if (this.Position.X > World.CLOUD_MAX_X)
                this.Position.X = -World.CLOUD_MAX_X;

            // Apply translations.
            Utilities.ApplyTranslation(this.PointsOrigin, this.Points, this.Rotation, this.Position, World.CLOUD_SCALE);
            Utilities.ApplyTranslation(this.Points, this.Points, this.Position, 0f, D2DPoint.Zero, this.ScaleX, this.ScaleY);

            // Update translated dimensions for rendering purposes.
            _translatedDims.Reset();
            _translatedDims.Update(this.Points);
        }

        private void DrawCloud(RenderContext ctx, D2DColor todColor)
        {
            var color1 = _cloudColorDark;
            var color2 = _cloudColorLight;
            var points = this.Points;

            // Find min/max height.
            var minY = _translatedDims.MinY;
            var maxY = _translatedDims.MaxY;

            for (int i = 0; i < points.Length; i++)
            {
                var point = points[i];
                var dims = this.Dims[i];

                // Lerp slightly darker colors to give the cloud some depth.

                //Darker clouds on bottom.
                var amt = Utilities.Factor(point.Y, minY, maxY);
                var color = Utilities.LerpColor(color1, color2, 1f - amt);

                // Add time of day color.
                color = Utilities.LerpColor(color, todColor, 0.5f);

                // Draw cloud part with lighting.
                ctx.FillEllipseWithLighting(new D2DEllipse(point, dims), color, LIGHT_INTENSITY);
            }
        }

        private void DrawCloudGroundShadowAndRay(RenderContext ctx, D2DColor shadowColor, float todAngle)
        {
            const float MAX_ALT = 8000f;
            const float WIDTH_OFFSET = 50f;
            const float BOT_WIDTH_OFFSET = 40f;

            // Don't even try if the viewport is above the shadows and rays.
            if (ctx.Viewport.bottom * -1f > MAX_ALT)
                return;

            var cloudAlt = Utilities.PositionToAltitude(this.Position);

            if (cloudAlt > MAX_ALT)
                return;

            // Get min/max X positions and compute widths and offsets.
            var minX = _translatedDims.MinX - WIDTH_OFFSET;
            var maxX = _translatedDims.MaxX + WIDTH_OFFSET;
            var width = Math.Abs(maxX - minX);
            var widthHalf = width * 0.5f;
            var widthOffset = new D2DPoint(widthHalf + BOT_WIDTH_OFFSET, 0f);

            // Get the initial ground position.
            var cloudShadowPos = GetCloudShadowPos(this.Position, todAngle);

            // Build a polygon for the ray.
            _shadowRayPoly[0] = new D2DPoint(minX, this.Position.Y);
            _shadowRayPoly[1] = new D2DPoint(maxX, this.Position.Y);
            _shadowRayPoly[2] = cloudShadowPos + widthOffset;
            _shadowRayPoly[3] = cloudShadowPos - widthOffset;

            // Draw ray.
            if (ctx.Viewport.Contains(_shadowRayPoly))
            {
                var alpha = 0.05f * Math.Clamp((1f - Utilities.FactorWithEasing(cloudAlt, MAX_ALT, EasingFunctions.EaseLinear)), 0.1f, 1f);
                var rayColor = shadowColor.WithAlpha(alpha);
                ctx.FillPolygon(_shadowRayPoly, rayColor);
            }

            // Draw ground shadows.
            if (ctx.Viewport.Contains(cloudShadowPos, this.Radius * this.ScaleX * World.CLOUD_SCALE * 3f))
            {
                if (World.UseSimpleCloudGroundShadows)
                {
                    var shadowWidth = width;
                    ctx.FillEllipse(new D2DEllipse(cloudShadowPos, new D2DSize(shadowWidth * 0.9f, 35f)), shadowColor.WithAlpha(0.2f));
                }
                else
                {
                    for (int i = 0; i < this.Points.Length; i++)
                    {
                        var point = this.Points[i];
                        var dims = this.Dims[i];
                        var shadowPos = GetCloudShadowPos(point, todAngle);

                        ctx.FillEllipse(new D2DEllipse(shadowPos, new D2DSize(dims.width * 4f, dims.height * 0.5f)), shadowColor);
                    }
                }
            }
        }

        private D2DPoint GetCloudShadowPos(D2DPoint pos, float angle)
        {
            var cloudGroundPos = new D2DPoint(pos.X, -80f + Math.Abs(((pos.Y * 0.1f))));
            var cloudShadowPos = cloudGroundPos + Utilities.AngleToVectorDegrees(angle, pos.DistanceTo(cloudGroundPos));
            cloudShadowPos.Y = cloudGroundPos.Y;

            return cloudShadowPos;
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
            var dims = new List<D2DSize>();

            // Try to make clouds at higher altitude more thin and whispy?
            var altFact = Utilities.Factor(Math.Abs(position.Y), MAX_ALT);

            for (int i = 0; i < nPnts; i++)
            {
                var dimsX = rnd.NextFloat(MIN_DIMS_X + (altFact * ALT_FACT_AMT), MAX_DIMS_X + (altFact * ALT_FACT_AMT));
                var dimsY = rnd.NextFloat(MIN_DIMS_Y - (altFact * ALT_FACT_AMT), MAX_DIMS_Y - (altFact * ALT_FACT_AMT));
                dims.Add(new D2DSize(dimsX, dimsY));
            }

            var rotation = 0f;
            var poly = GameObjectPoly.RandomPoly(nPnts, radius);
            var pnts = poly.ToList();

            var newCloud = new Cloud(position, pnts, dims, rotation);
            newCloud.Radius = radius;
            newCloud.ScaleX = rnd.NextFloat(1.5f, 5f);
            newCloud.ScaleY = rnd.NextFloat(0.4f, 0.7f);

            // Fiddle rotation direction.
            if (nPnts % 2 == 0)
                newCloud.RotationDirection = -1f;

            return newCloud;
        }
    }
}
