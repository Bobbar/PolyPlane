using PolyPlane.GameObjects.Tools;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using PolyPlane.Helpers;

namespace PolyPlane.Rendering
{
    public static class GLExtensions
    {

        //public static void FillPolygon(this SKCanvas canvas, RenderPoly poly, SKColor color)
        //{
        //    using (var path = new SKPath())
        //    using (var paint = new SKPaint() { Color = color, IsAntialias = true })
        //    {
        //        path.AddPoly(poly.Poly.ToSkPoints(), true);

        //        canvas.DrawPath(path, paint);
        //    }
        //}

        //public static void DrawLine(this SKCanvas canvas, SKPoint p0, SKPoint p1, SKColor color, float weight)
        //{
        //    using (var paint = new SKPaint() { Color = color, IsAntialias = true, StrokeWidth = weight })
        //    {
        //        canvas.DrawLine(p0, p1, paint);
        //    }
        //}
    }
}
