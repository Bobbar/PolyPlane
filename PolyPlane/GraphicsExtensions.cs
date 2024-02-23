using unvell.D2DLib;

namespace PolyPlane
{
    public static class GraphicsExtensions
    {
        public static int OnScreen = 0;
        public static int OffScreen = 0;


        public static void DrawArrow(this D2DGraphics gfx, D2DPoint start, D2DPoint end, D2DColor color, float weight = 1f)
        {
            const float ARROW_LEN = 10f;
            const float ARROW_ANGLE = 140f;

            var angle = (end - start).Angle(true);
            var arrow1 = Helpers.AngleToVectorDegrees(angle + ARROW_ANGLE);
            var arrow2 = Helpers.AngleToVectorDegrees(angle - ARROW_ANGLE);

            gfx.DrawLine(start, end, color, weight, D2DDashStyle.Solid, D2DCapStyle.Triangle, D2DCapStyle.Triangle);
            gfx.DrawLine(end, end + (arrow1 * ARROW_LEN), color, weight, D2DDashStyle.Solid, D2DCapStyle.Triangle, D2DCapStyle.Triangle);
            gfx.DrawLine(end, end + (arrow2 * ARROW_LEN), color, weight, D2DDashStyle.Solid, D2DCapStyle.Triangle, D2DCapStyle.Triangle);
        }

        public static void DrawArrowClamped(this D2DGraphics gfx, D2DRect viewport, D2DPoint start, D2DPoint end, D2DColor color, float weight = 1f)
        {
            const float ARROW_LEN = 10f;
            const float ARROW_ANGLE = 140f;

            var angle = (end - start).Angle(true);
            var arrow1 = Helpers.AngleToVectorDegrees(angle + ARROW_ANGLE);
            var arrow2 = Helpers.AngleToVectorDegrees(angle - ARROW_ANGLE);

            gfx.DrawLineClamped(viewport, start, end, color, weight, D2DDashStyle.Solid, D2DCapStyle.Triangle, D2DCapStyle.Triangle);
            gfx.DrawLineClamped(viewport, end, end + (arrow1 * ARROW_LEN), color, weight, D2DDashStyle.Solid, D2DCapStyle.Triangle, D2DCapStyle.Triangle);
            gfx.DrawLineClamped(viewport, end, end + (arrow2 * ARROW_LEN), color, weight, D2DDashStyle.Solid, D2DCapStyle.Triangle, D2DCapStyle.Triangle);
        }


        public static void DrawArrowStroked(this D2DGraphics gfx, D2DPoint start, D2DPoint end, D2DColor color, float weight, D2DColor strokeColor, float strokeWeight)
        {
            gfx.DrawArrow(start, end, strokeColor, weight + strokeWeight);
            gfx.DrawArrow(start, end, color, weight);
        }

        public static void DrawArrowStrokedClamped(this D2DGraphics gfx, D2DRect viewport, D2DPoint start, D2DPoint end, D2DColor color, float weight, D2DColor strokeColor, float strokeWeight)
        {
            gfx.DrawArrowClamped(viewport, start, end, strokeColor, weight + strokeWeight);
            gfx.DrawArrowClamped(viewport, start, end, color, weight);


            //if (viewport.Contains(start) || viewport.Contains(end))
            //{
            //    gfx.DrawArrow(start, end, strokeColor, weight + strokeWeight);
            //    gfx.DrawArrow(start, end, color, weight);
            //}
        }


        public static void FillEllipseClamped(this D2DGraphics gfx, D2DRect viewport, D2DEllipse ellipse, D2DColor color)
        {
            if (viewport.Contains(ellipse))
            {
                gfx.FillEllipse(ellipse, color);
                OnScreen++;
            }
            else
                OffScreen++;

        }

        public static void DrawLineClamped(this D2DGraphics gfx, D2DRect viewport, D2DPoint start, D2DPoint end, D2DColor color, float weight = 1f, D2DDashStyle dashStyle = D2DDashStyle.Solid, D2DCapStyle startCap = D2DCapStyle.Flat, D2DCapStyle endCap = D2DCapStyle.Flat)
        {
            if (viewport.Contains(start) || viewport.Contains(end))
            {
                gfx.DrawLine(start, end, color, weight, dashStyle, startCap, endCap);
                OnScreen++;
            }
            else
                OffScreen++;
        }

        public static void DrawPolygonClamped(this D2DGraphics gfx, D2DRect viewport, D2DPoint[] points, D2DColor strokeColor, float strokeWidth, D2DDashStyle dashStyle, D2DColor fillColor)
        {
            if (viewport.Contains(points))
            {
                gfx.DrawPolygon(points, strokeColor, strokeWidth, dashStyle, fillColor);
                OnScreen++;
            }
            else
                OffScreen++;
        }

        public static void FillRectangleClamped(this D2DGraphics gfx, D2DRect viewport, D2DRect rect, D2DColor color)
        {
            if (viewport.Contains(rect))
            {
                gfx.FillRectangle(rect, color);
                OnScreen++;
            }
            else
                OffScreen++;
        }

        public static void DrawTextCenterClamped(this D2DGraphics gfx, D2DRect viewport, string text, D2DColor color, string fontName, float fontSize, D2DRect rect)
        {
            if (viewport.Contains(rect))
            {
                gfx.DrawTextCenter(text, color, fontName, fontSize, rect);
                OnScreen++;
            }
            else
                OffScreen++;
        }

        public static void DrawRectangleClamped(this D2DGraphics gfx, D2DRect viewport, D2DRect rect, D2DColor color, float strokeWidth = 1f, D2DDashStyle dashStyle = D2DDashStyle.Solid)
        {
            if (viewport.Contains(rect))
            {
                gfx.DrawRectangle(rect, color, strokeWidth, dashStyle);
                OnScreen++;
            }
            else
                OffScreen++;
        }

        public static void DrawTextClamped(this D2DGraphics gfx, D2DRect viewport, string text, D2DColor color, string fontName, float fontSize, D2DRect rect, DWriteTextAlignment halign = DWriteTextAlignment.Leading, DWriteParagraphAlignment valign = DWriteParagraphAlignment.Near)
        {
            if (viewport.Contains(rect))
            {
                gfx.DrawText(text, color, fontName, fontSize, rect, halign, valign);
                OnScreen++;
            }
            else
                OffScreen++;
        }

        public static void DrawEllipseClamped(this D2DGraphics gfx, D2DRect viewport, D2DEllipse ellipse, D2DColor color, float weight = 1f, D2DDashStyle dashStyle = D2DDashStyle.Solid)
        {
            if (viewport.Contains(ellipse))
            {
                gfx.DrawEllipse(ellipse, color, weight, dashStyle);
                OnScreen++;
            }
            else
                OffScreen++;
        }

    }
}
