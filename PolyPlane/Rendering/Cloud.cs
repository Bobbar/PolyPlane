using PolyPlane.Helpers;
using unvell.D2DLib;

namespace PolyPlane.Rendering
{
    public class Cloud
    {
        public CloudGeometry Geometry;
        public D2DPoint Position;
        public int OrderIndex;

        private static D2DPoint[] _shadowRayPoly = new D2DPoint[4];
        private readonly D2DColor _cloudColorLight = D2DColor.WhiteSmoke;
        private readonly D2DColor _cloudColorDark = new D2DColor(1f, 0.6f, 0.6f, 0.6f);
        private const float LIGHT_INTENSITY = 0.7f;
        private const float MAX_SHADOW_ALT = 8000f;

        private static int _orderIndex = 0;

        public Cloud(D2DPoint position, CloudGeometry geo)
        {
            Geometry = geo;
            Position = position;
            _orderIndex++;
            OrderIndex = _orderIndex;
        }

        public void Render(RenderContext ctx, D2DColor shadowColor, D2DColor todColor, float todAngle)
        {
            DrawCloudGroundShadowAndRay(ctx, shadowColor, todAngle);

            if (ctx.Viewport.Contains(this.Position, (this.Geometry.Radius * 2f) * this.Geometry.ScaleX * World.CLOUD_SCALE))
                DrawCloud(ctx, todColor);
        }

        public void Update(float dt)
        {
            var altFact = 30f * Utilities.Factor(Utilities.PositionToAltitude(this.Position), World.CloudRangeY.X * -1f); // Higher clouds move slower?

            var sizeOffset = (this.Geometry.Radius / 2f); // Smaller clouds move slightly faster?
            var rate = Math.Clamp((World.CLOUD_MOVE_RATE - altFact) - sizeOffset, 0.1f, World.CLOUD_MOVE_RATE);

            this.Position.X += rate * dt;
          
            // Wrap clouds.
            if (this.Position.X > World.CLOUD_MAX_X)
                this.Position.X = -World.CLOUD_MAX_X;
        }

        private void DrawCloud(RenderContext ctx, D2DColor todColor)
        {
            const float MAX_ALT = 30000f;
            const float MIN_ALT = 10000f;

            var color1 = _cloudColorDark;
            var color2 = _cloudColorLight;
            var points = this.Geometry.Points;

            // Find min/max height.
            var minY = this.Geometry.TranslatedDims.MinY;
            var maxY = this.Geometry.TranslatedDims.MaxY;

            // Try to make clouds at higher altitude more thin and whispy?
            var scaleFactY = 1f - Utilities.FactorWithEasing(Utilities.PositionToAltitude(this.Position) - MIN_ALT, MAX_ALT, EasingFunctions.Out.EaseQuad);
            scaleFactY = Math.Clamp(scaleFactY, 0.4f, 1f);

            ctx.PushTransform();
            ctx.TranslateTransform(this.Position * ctx.CurrentScale);
            ctx.ScaleTransform(1f, scaleFactY, D2DPoint.Zero);

            for (int i = 0; i < points.Length; i++)
            {
                var point = points[i];
                var dims = this.Geometry.Dims[i];

                // Lerp slightly darker colors to give the cloud some depth.
                // Darker clouds on bottom.
                var amt = Utilities.Factor(point.Y, minY, maxY);
                var color = Utilities.LerpColor(color1, color2, 1f - amt);

                // Add time of day color.
                color = Utilities.LerpColor(color, todColor, 0.5f);

                // Draw cloud part with lighting.
                var sampleLocation = point + this.Position;
                ctx.FillEllipseWithLighting(new D2DEllipse(point, dims), sampleLocation, color, LIGHT_INTENSITY, clipped: false);
            }

            ctx.PopTransform();
        }

        private void DrawCloudGroundShadowAndRay(RenderContext ctx, D2DColor shadowColor, float todAngle)
        {
            const float WIDTH_OFFSET = 50f;

            // Don't even try if the viewport is above the shadows and rays.
            if (ctx.Viewport.bottom * -1f > MAX_SHADOW_ALT)
                return;

            var cloudAlt = Utilities.PositionToAltitude(this.Position);

            if (cloudAlt > MAX_SHADOW_ALT)
                return;

            // Get min/max X positions and compute widths and offsets.
            var minX = this.Position.X + this.Geometry.TranslatedDims.MinX - WIDTH_OFFSET;
            var maxX = this.Position.X + this.Geometry.TranslatedDims.MaxX + WIDTH_OFFSET;
            var width = Math.Abs(maxX - minX);
            var widthHalf = width * 0.5f;
            var widthOffset = new D2DPoint(widthHalf, 0f);

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
                var alpha = 0.05f * Math.Clamp((1f - Utilities.FactorWithEasing(cloudAlt, MAX_SHADOW_ALT, EasingFunctions.EaseLinear)), 0.1f, 1f);

                // Don't draw the ray if alpha is below the point of being visible.
                if (alpha >= 0.005f)
                {
                    var rayColor = shadowColor.WithAlpha(alpha);
                    ctx.FillPolygon(_shadowRayPoly, rayColor);
                }
            }

            // Draw ground shadows.
            if (ctx.Viewport.Contains(cloudShadowPos, this.Geometry.Radius * this.Geometry.ScaleX * World.CLOUD_SCALE * 3f))
            {
                // Make the ground shadow slightly wider.
                var shadowWidth = widthHalf * 1.2f;
                ctx.FillEllipse(new D2DEllipse(cloudShadowPos, new D2DSize(shadowWidth, 35f)), shadowColor.WithAlpha(0.2f));
            }
        }

        private D2DPoint GetCloudShadowPos(D2DPoint pos, float angle)
        {
            const float MIN_CLOUD_ALT = 2000f;

            // Rescale the altitude to a smaller range below the ground level.
            // Higher clouds will produce a lower shadow.
            var alt = Utilities.PositionToAltitude(pos);
            var yPos = Utilities.ScaleToRange(alt, MIN_CLOUD_ALT, MAX_SHADOW_ALT, 120f, 720f);

            var cloudGroundPos = new D2DPoint(pos.X, yPos);
            var cloudShadowPos = cloudGroundPos + Utilities.AngleToVectorDegrees(angle, pos.DistanceTo(cloudGroundPos));
            cloudShadowPos.Y = cloudGroundPos.Y;

            return cloudShadowPos;
        }
    }
}
