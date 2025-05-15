using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.ES20;
using OpenTK.Platform.Windows;
using PolyPlane.GameObjects;
using PolyPlane.Net;
using SkiaSharp;
using SkiaSharp.Internals;
using SkiaSharp.Views.Desktop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using unvell.D2DLib;
using PolyPlane.Helpers;

namespace PolyPlane.Rendering
{
    public sealed class GLRenderer : IDisposable
    {
        private GLControl _glControl;
        private const SKColorType colorType = SKColorType.Rgba8888;
        private const GRSurfaceOrigin surfaceOrigin = GRSurfaceOrigin.BottomLeft;

        private GRContext grContext;
        private GRGlFramebufferInfo glInfo;
        private GRBackendRenderTarget renderTarget;
        private SKSurface surface;
        private SKCanvas canvas;

        private SKSizeI lastSize;

        private Form? _targetForm;

        private int Width => (int)(_targetForm.Width / (_targetForm.DeviceDpi / DEFAULT_DPI));
        private int Height => (int)(_targetForm.Height / (_targetForm.DeviceDpi / DEFAULT_DPI));


        private const float VIEW_SCALE = 4f;
        private const float DEFAULT_DPI = 96f;
        private const float ZOOM_FACTOR = 0.07f; // Effects zoom in/out speed.


        private readonly SKColor _clearColor = SKColors.Transparent;

        private float _currentDPI = DEFAULT_DPI;

        public GLRenderer(Control renderTarget, NetEventManager netMan)
        {

        }

        public Control InitGLControl(Control targetControl)
        {
            targetControl.Visible = false;

            var form = targetControl.FindForm();
            _targetForm = form; 

            _glControl = new GLControl();
            _glControl.Dock = DockStyle.Fill;
            _glControl.VSync = true;
            _glControl.HandleCreated += HandleCreated;

            form.Controls.Add(_glControl);

            return _glControl;

        }

        private void InitGfx()
        {
            _glControl.MakeCurrent();

            _currentDPI = _targetForm.DeviceDpi;

            // create the contexts if not done already
            if (grContext == null)
            {
                var glInterface = GRGlInterface.Create();
                grContext = GRContext.CreateGl(glInterface);
            }

            // get the new surface size
            var newSize = new SKSizeI(Width, Height);

            // manage the drawing surface
            if (renderTarget == null || lastSize != newSize || !renderTarget.IsValid)
            {
                // create or update the dimensions
                lastSize = newSize;

                GL.GetInteger(GetPName.FramebufferBinding, out var framebuffer);
                GL.GetInteger(GetPName.StencilBits, out var stencil);
                GL.GetInteger(GetPName.Samples, out var samples);
                var maxSamples = grContext.GetMaxSurfaceSampleCount(colorType);
                if (samples > maxSamples)
                    samples = maxSamples;
                glInfo = new GRGlFramebufferInfo((uint)framebuffer, colorType.ToGlSizedFormat());

                // destroy the old surface
                surface?.Dispose();
                surface = null;
                canvas = null;

                // re-create the render target
                renderTarget?.Dispose();
                renderTarget = new GRBackendRenderTarget(newSize.Width, newSize.Height, samples, stencil, glInfo);
            }

            // create the surface
            if (surface == null)
            {
                surface = SKSurface.Create(grContext, renderTarget, surfaceOrigin, colorType);
                canvas = surface.Canvas;
            }

            var scaleSize = GetViewportScaled();
            World.UpdateViewport(scaleSize);

        }

        private Size GetViewportScaled()
        {
            var scaleSize = new Size((int)((float)_targetForm.Size.Width / ((float)_currentDPI / World.DEFAULT_DPI)), (int)((float)_targetForm.Size.Height / ((float)_currentDPI / World.DEFAULT_DPI)));
            return scaleSize;
        }

        private void ResizeViewPort()
        {
            var scaleSize = GetViewportScaled();
            World.UpdateViewport(scaleSize);
        }

        private SKPaint _testPaint = new SKPaint() { Color = SKColors.Blue };

        private void HandleCreated(object? sender, EventArgs e)
        {
            InitGfx();
        }

        public void RenderFrame(GameObject viewObject, float dt)
        {
            if (canvas == null)
                return;

            using (new SKAutoCanvasRestore(canvas, true))
            {
                canvas.Clear(_clearColor);

                var mat = SKMatrix.Identity;
                mat = SKMatrix.Concat(mat, SKMatrix.CreateScale(World.ZoomScale, World.ZoomScale));

                //canvas.Scale(World.ZoomScale);

                if (viewObject is FighterPlane plane)
                {
                    var zAmt = World.ZoomScale;
                    var pos = new D2DPoint(World.ViewPortSize.width * 0.5f, World.ViewPortSize.height * 0.5f);
                    pos *= zAmt;

                    var offset = new D2DPoint(-viewObject.Position.X, -viewObject.Position.Y);
                    offset *= zAmt;

                    //canvas.DrawCircle(new SKPoint(300f, 300f), 100f, _testPaint);

                    mat = SKMatrix.Concat(mat, SKMatrix.CreateScale(VIEW_SCALE, VIEW_SCALE, viewObject.Position.X, viewObject.Position.Y));

                    //var trans = offset + pos;

                    var trans = offset + pos;

                    mat = SKMatrix.Concat(mat, SKMatrix.CreateTranslation(trans.X, trans.Y));


                    //canvas.Translate(offset + pos);

                    //canvas.Scale(VIEW_SCALE, VIEW_SCALE, viewObject.Position.X, viewObject.Position.Y);
                    //canvas.Scale(VIEW_SCALE, VIEW_SCALE);

                    //canvas.Translate(offset);
                    //canvas.Translate(pos);

                    canvas.SetMatrix(mat);

                    using (var path = new SKPath())
                    using (var paint = new SKPaint() { Color = plane.PlaneColor.ToSKColor()})
                    {
                        path.AddPoly(plane.Polygon.Poly.ToSkPoints(), true);

                        canvas.DrawPath(path, paint);
                    }

                    canvas.ResetMatrix();

                }

                // Draw stuff...
            }

            canvas.Flush();
            GL.Finish();
            _glControl.SwapBuffers();
        }

        public void ZoomIn()
        {
            var amt = ZOOM_FACTOR * World.ZoomScale;
            World.ZoomScale += amt;

            ResizeViewPort();
        }

        public void ZoomOut()
        {
            var amt = ZOOM_FACTOR * World.ZoomScale;
            World.ZoomScale -= amt;

            ResizeViewPort();
        }

        public void Dispose()
        {


        }
    }
}
