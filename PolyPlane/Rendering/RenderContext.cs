﻿using PolyPlane.GameObjects.Tools;
using unvell.D2DLib;

namespace PolyPlane.Rendering
{
    /// <summary>
    /// Provides overloads of common graphics methods which include automagic viewport clamping for performance.
    /// </summary>
    public class RenderContext
    {
        public D2DGraphics Gfx;
        public D2DDevice Device;
        public D2DRect Viewport;

        public float CurrentScale
        {
            get
            {
                if (Gfx != null)
                    return Gfx.GetTransform().M11;

                return 1f;
            }
        }

        private Stack<D2DRect> _vpStack = new Stack<D2DRect>();

        public RenderContext() { }

        public RenderContext(D2DGraphics gfx) : this(gfx, new D2DRect())
        {
        }

        public RenderContext(D2DGraphics gfx, D2DDevice device)
        {
            Gfx = gfx;
            Device = device;
        }

        public RenderContext(D2DGraphics gfx, D2DRect viewport)
        {
            Gfx = gfx;
            Viewport = viewport;
        }

        public void PushViewPort(D2DRect viewport)
        {
            _vpStack.Push(Viewport);

            Viewport = viewport;
        }

        public void PopViewPort()
        {
            Viewport = _vpStack.Pop();
        }

        public void FillEllipse(D2DEllipse ellipse, D2DColor color)
        {
            Gfx.FillEllipseClamped(Viewport, ellipse, color);
        }

        public void FillEllipse(D2DEllipse ellipse, D2DBrush brush)
        {
            Gfx.FillEllipseClamped(Viewport, ellipse, brush);
        }

        public void FillEllipseSimple(D2DPoint pos, float radius, D2DColor color)
        {
            Gfx.FillEllipseClamped(Viewport, new D2DEllipse(pos, new D2DSize(radius, radius)), color);
        }

        public void FillEllipseSimple(D2DPoint pos, float radius, D2DBrush brush)
        {
            Gfx.FillEllipseClamped(Viewport, new D2DEllipse(pos, new D2DSize(radius, radius)), brush);
        }

        public void DrawEllipse(D2DEllipse ellipse, D2DColor color, float weight = 1f, D2DDashStyle dashStyle = D2DDashStyle.Solid)
        {
            Gfx.DrawEllipseClamped(Viewport, ellipse, color, weight, dashStyle);
        }

        public void DrawLine(D2DPoint start, D2DPoint end, D2DColor color, float weight = 1f, D2DDashStyle dashStyle = D2DDashStyle.Solid, D2DCapStyle startCap = D2DCapStyle.Flat, D2DCapStyle endCap = D2DCapStyle.Flat)
        {
            Gfx.DrawLineClamped(Viewport, start, end, color, weight, dashStyle, startCap, endCap);
        }

        public void DrawPolygon(D2DPoint[] points, D2DColor strokeColor, float strokeWidth, D2DDashStyle dashStyle, D2DColor fillColor)
        {
            Gfx.DrawPolygonClamped(Viewport, points, strokeColor, strokeWidth, dashStyle, fillColor);
        }

        public void DrawPolygon(RenderPoly poly, D2DColor strokeColor, float strokeWidth, D2DDashStyle dashStyle, D2DColor fillColor)
        {
            Gfx.DrawPolygonClamped(Viewport, poly, strokeColor, strokeWidth, dashStyle, fillColor);
        }

        public void DrawPolygon(D2DPoint[] points, D2DColor strokeColor, float strokeWidth, D2DDashStyle dashStyle, D2DBrush fillBrush)
        {
            Gfx.DrawPolygonClamped(Viewport, points, strokeColor, strokeWidth, dashStyle, fillBrush);
        }

        public void FillRectangle(D2DRect rect, D2DColor color)
        {
            Gfx.FillRectangleClamped(Viewport, rect, color);
        }

        public void FillRectangle(float x, float y, float width, float height, D2DColor color)
        {
            Gfx.FillRectangleClamped(Viewport, x, y, width, height, color);
        }

        public void DrawTextCenter(string text, D2DColor color, string fontName, float fontSize, D2DRect rect)
        {
            Gfx.DrawTextCenterClamped(Viewport, text, color, fontName, fontSize, rect);
        }

        public void DrawRectangle(D2DRect rect, D2DColor color, float strokeWidth = 1f, D2DDashStyle dashStyle = D2DDashStyle.Solid)
        {
            Gfx.DrawRectangleClamped(Viewport, rect, color, strokeWidth, dashStyle);
        }

        public void DrawText(string text, D2DColor color, string fontName, float fontSize, D2DRect rect, DWriteTextAlignment halign = DWriteTextAlignment.Leading, DWriteParagraphAlignment valign = DWriteParagraphAlignment.Near)
        {
            Gfx.DrawTextClamped(Viewport, text, color, fontName, fontSize, rect, halign, valign);
        }

        public void DrawText(string text, D2DColor color, string fontName, float fontSize, float x, float y, DWriteTextAlignment halign = DWriteTextAlignment.Leading, DWriteParagraphAlignment valign = DWriteParagraphAlignment.Near)
        {
            Gfx.DrawTextClamped(Viewport, text, color, fontName, fontSize, new D2DRect(x, y, 99999f, 99999f), halign, valign);
        }

        public void DrawText(string text, D2DBrush brush, D2DTextFormat format, D2DRect rect)
        {
            Gfx.DrawTextClamped(Viewport, text, brush, format, rect);
        }

        public void DrawArrow(D2DPoint start, D2DPoint end, D2DColor color, float weight, float arrowLen = 10f)
        {
            Gfx.DrawArrowClamped(Viewport, start, end, color, weight, arrowLen);
        }

        public void DrawArrowStroked(D2DPoint start, D2DPoint end, D2DColor color, float weight, D2DColor strokeColor, float strokeWeight)
        {
            Gfx.DrawArrowStrokedClamped(Viewport, start, end, color, weight, strokeColor, strokeWeight);
        }
    }
}
