using PolyPlane.GameObjects.Tools;
using PolyPlane.Helpers;
using unvell.D2DLib;

namespace PolyPlane.Rendering
{
    public static class GraphicsExtensions
    {
        public static int OnScreen = 0;
        public static int OffScreen = 0;

        private static readonly D2DPoint UnitUp = -D2DPoint.UnitY;
        private static readonly D2DPoint UnitLeft = -D2DPoint.UnitX;
        private static readonly D2DPoint UnitDown = D2DPoint.UnitY;
        private static readonly D2DPoint UnitRight = D2DPoint.UnitX;

        public static readonly D2DPoint[] TrianglePoly = new D2DPoint[]
        {
            new D2DPoint(0,-3),
            new D2DPoint(3,2),
            new D2DPoint(-3,2),
        };

        public static void DrawTriangle(this D2DGraphics gfx, D2DPoint position, D2DColor color, D2DColor fillColor, float scale = 1f)
        {
            var tri = new D2DPoint[TrianglePoly.Length];

            Utilities.ApplyTranslation(TrianglePoly, tri, 0f, position, scale);

            gfx.DrawPolygon(tri, color, 1f, D2DDashStyle.Solid, fillColor);
        }

        public static void DrawArrow(this D2DGraphics gfx, D2DPoint start, D2DPoint end, D2DColor color, float weight = 1f, float arrowLen = 10f)
        {
            const float ARROW_ANGLE = 140f;

            var angle = (end - start).Angle();
            var arrow1 = Utilities.AngleToVectorDegrees(angle + ARROW_ANGLE);
            var arrow2 = Utilities.AngleToVectorDegrees(angle - ARROW_ANGLE);

            gfx.DrawLine(start, end, color, weight, D2DDashStyle.Solid, D2DCapStyle.Triangle, D2DCapStyle.Triangle);
            gfx.DrawLine(end, end + arrow1 * arrowLen, color, weight, D2DDashStyle.Solid, D2DCapStyle.Triangle, D2DCapStyle.Triangle);
            gfx.DrawLine(end, end + arrow2 * arrowLen, color, weight, D2DDashStyle.Solid, D2DCapStyle.Triangle, D2DCapStyle.Triangle);
        }

        public static void DrawArrowClamped(this D2DGraphics gfx, D2DRect viewport, D2DPoint start, D2DPoint end, D2DColor color, float weight = 1f, float arrowLen = 10f)
        {
            const float ARROW_ANGLE = 140f;

            var angle = (end - start).Angle();
            var arrow1 = Utilities.AngleToVectorDegrees(angle + ARROW_ANGLE);
            var arrow2 = Utilities.AngleToVectorDegrees(angle - ARROW_ANGLE);

            gfx.DrawLineClamped(viewport, start, end, color, weight, D2DDashStyle.Solid, D2DCapStyle.Triangle, D2DCapStyle.Triangle);
            gfx.DrawLineClamped(viewport, end, end + arrow1 * arrowLen, color, weight, D2DDashStyle.Solid, D2DCapStyle.Triangle, D2DCapStyle.Triangle);
            gfx.DrawLineClamped(viewport, end, end + arrow2 * arrowLen, color, weight, D2DDashStyle.Solid, D2DCapStyle.Triangle, D2DCapStyle.Triangle);
        }

        public static void DrawCrosshair(this D2DGraphics gfx, D2DPoint pos, float weight, D2DColor color, float innerRadius, float outerRadius)
        {
            gfx.DrawLine(pos + (UnitUp * innerRadius), pos + (UnitUp * outerRadius), color, weight);
            gfx.DrawLine(pos + (UnitLeft * innerRadius), pos + (UnitLeft * outerRadius), color, weight);
            gfx.DrawLine(pos + (UnitDown * innerRadius), pos + (UnitDown * outerRadius), color, weight);
            gfx.DrawLine(pos + (UnitRight * innerRadius), pos + (UnitRight * outerRadius), color, weight);
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


        public static void FillEllipseSimple(this D2DGraphics gfx, D2DPoint pos, float radius, D2DColor color)
        {
            gfx.FillEllipse(new D2DEllipse(pos, new D2DSize(radius, radius)), color);
        }

        public static void FillEllipseSimple(this D2DGraphics gfx, D2DPoint pos, float radius, D2DBrush brush)
        {
            gfx.FillEllipse(new D2DEllipse(pos, new D2DSize(radius, radius)), brush);
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

        public static void FillEllipseClamped(this D2DGraphics gfx, D2DRect viewport, D2DEllipse ellipse, D2DBrush brush)
        {
            if (viewport.Contains(ellipse))
            {
                gfx.FillEllipse(ellipse, brush);
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

        public static void DrawPolygonClamped(this D2DGraphics gfx, D2DRect viewport, RenderPoly poly, D2DColor strokeColor, float strokeWidth, D2DDashStyle dashStyle, D2DColor fillColor)
        {
            if (viewport.Contains(poly.Position))
            {
                gfx.DrawPolygon(poly.Poly, strokeColor, strokeWidth, dashStyle, fillColor);
                OnScreen++;
            }
            else
                OffScreen++;
        }


        public static void DrawPolygonClamped(this D2DGraphics gfx, D2DRect viewport, D2DPoint[] points, D2DColor strokeColor, float strokeWidth, D2DDashStyle dashStyle, D2DBrush fillBrush)
        {
            if (viewport.Contains(points))
            {
                gfx.DrawPolygon(points, strokeColor, strokeWidth, dashStyle, fillBrush);
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

        public static void FillRectangleClamped(this D2DGraphics gfx, D2DRect viewport, D2DRect rect, D2DBrush brush)
        {
            if (viewport.Contains(rect))
            {
                gfx.FillRectangle(rect, brush);

                OnScreen++;
            }
            else
                OffScreen++;
        }

        public static void FillRectangleClamped(this D2DGraphics gfx, D2DRect viewport, float x, float y, float width, float height, D2DColor color)
        {
            var pos = new D2DPoint(x, y);
            if (viewport.Contains(pos))
            {
                gfx.FillRectangle(x, y, width, height, color);

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

        public static void DrawTextClamped(this D2DGraphics gfx, D2DRect viewport, string text, D2DBrush brush, D2DTextFormat format, D2DRect rect)
        {
            if (viewport.Contains(rect))
            {
                gfx.DrawText(text, brush, format, rect);
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
