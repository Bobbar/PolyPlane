using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.ES20;
using OpenTK.Platform.Windows;
using PolyPlane.GameObjects;
using PolyPlane.GameObjects.Managers;
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
using System.Diagnostics;

namespace PolyPlane.Rendering
{
    public sealed class GLRenderer : IDisposable
    {
        private GLRenderContext _ctx = new GLRenderContext();

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

        private float _groundColorTOD = 0f;


        private const float VIEW_SCALE = 4f;
        private const float DEFAULT_DPI = 96f;
        private const float ZOOM_FACTOR = 0.07f; // Effects zoom in/out speed.

        private readonly SKColor _skyColorLight = SKColors.SkyBlue.WithAlpha(127);
        private readonly SKColor _skyColorDark = SKColors.Black.WithAlpha(127);
        private readonly SKColor _clearColor = SKColors.Transparent;

        private float _currentDPI = DEFAULT_DPI;

        private GameObjectManager _objs = World.ObjectManager;
      

        private SKColor _groundColorLight = new SKColor(0, 74, 0, 255);
        private SKColor _groundColorDark = SKColors.DarkGreen;

        private SKPaint _groundBrush;


        private const float GROUND_TOD_INTERVAL = 0.1f; // Update ground brush when elapsed TOD exceeds this amount.

        private const int NUM_CLOUDS = 2000;
        private const int NUM_TREES = 1000;

        //private SmoothDouble _renderTimeSmooth = new SmoothDouble(10);
        //private Stopwatch _timer = new Stopwatch();

        private long _lastRenderTime = 0;
        private SmoothFloat _renderFPSSmooth = new SmoothFloat(20);

        private CloudManager _cloudManager = new();



        public GLRenderer(Control renderTarget, NetEventManager netMan)
        {
            _groundColorTOD = World.TimeOfDay;
           
        }

        public Control InitGLControl(Control targetControl)
        {
            targetControl.Visible = false;
            targetControl.SendToBack();

            var form = targetControl.FindForm();
            _targetForm = form;

            _glControl = new GLControl();
            _glControl.Dock = DockStyle.Fill;
            _glControl.VSync = true;
            _glControl.HandleCreated += HandleCreated;
            _glControl.Resize += Resize;

            form.Controls.Add(_glControl);

            return _glControl;

        }

        private void InitProceduralGenStuff(GLRenderContext ctx)
        {
            var rnd = new Random(1234);

            _cloudManager.GenClouds(rnd, NUM_CLOUDS);
            //_treeManager.GenTrees(rnd, NUM_TREES, ctx);
        }

        private void Resize(object? sender, EventArgs e)
        {
            InitGfx();
            ResizeViewPort();
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

                _ctx.SetCanvas(canvas);
            }

            var scaleSize = GetViewportScaled();
            World.UpdateViewport(scaleSize);

            UpdateGroundColorBrush(_ctx);

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

            InitProceduralGenStuff(_ctx);
        }


        private void UpdateTimersAndAnims(float dt)
        {
            //_hudMessageTimeout.Update(dt);

            //_screenFlash.Update(dt);
            //_screenShakeX.Update(dt);
            //_screenShakeY.Update(dt);
            //_warnLightFlash.Update(World.DEFAULT_DT);

            //_contrailBox.Update(_objs.Planes, dt);

            if (!World.IsPaused)
            {
                _cloudManager.Update();

                // Check if we need to update the ground brush.
                var todDiff = Math.Abs(World.TimeOfDay - _groundColorTOD);
                if (todDiff > GROUND_TOD_INTERVAL)
                {
                    _groundColorTOD = World.TimeOfDay;
                    UpdateGroundColorBrush(_ctx);
                }

                //UpdatePopMessages(dt);
            }

            //UpdateGroundColorBrush(_ctx);

        }


        public void RenderFrame(GameObject viewObject, float dt)
        {
            if (canvas == null)
                return;
            var center = new D2DPoint(World.ViewPortSize.width * 0.5f, World.ViewPortSize.height * 0.5f);


            UpdateTimersAndAnims(dt);

            using (new SKAutoCanvasRestore(canvas, true))
            {
                canvas.Clear(_clearColor);


                if (viewObject != null)
                {
                    var viewPortSize = new D2DSize((World.ViewPortSize.width / VIEW_SCALE), World.ViewPortSize.height / VIEW_SCALE);
                    var viewPortRect = new D2DRect(viewObject.Position, viewPortSize);
                    _ctx.PushViewPort(viewPortRect);



                    DrawSky(_ctx, viewObject);


                    DrawMovingBackground(_ctx, viewObject);

                    _ctx.PushTransform();
                    _ctx.ScaleTransform(World.ZoomScale);
                    //_ctx.ScaleTransform(World.ZoomScale, center);

                    DrawPlayerView(_ctx, viewObject);


                    // Draw stuff...
                    _ctx.PopTransform();


                    _ctx.PopViewPort();

                    //Debug.WriteLine(viewObject.Altitude);

                }
            }

            canvas.Flush();
            GL.Finish();
            _glControl.SwapBuffers();


            var now = DateTime.UtcNow.Ticks;
            var fps = TimeSpan.TicksPerSecond / (float)(now - _lastRenderTime);
            _lastRenderTime = now;
            _renderFPSSmooth.Add(fps);

        }


        private void DrawPlayerView(GLRenderContext ctx, GameObject viewObj)
        {
            FighterPlane? viewPlane = null;

            if (viewObj is FighterPlane plane)
                viewPlane = plane;

            ctx.PushTransform();

            var zAmt = World.ZoomScale;
            var center = new D2DPoint(World.ViewPortSize.width * 0.5f, World.ViewPortSize.height * 0.5f);
            var pos = center;
            var trans = -viewObj.Position + pos;

            ctx.ScaleTransform(VIEW_SCALE, pos);
            ctx.TranslateTransform(trans);

            var viewPortRect = new D2DRect(viewObj.Position, new D2DSize((World.ViewPortSize.width / VIEW_SCALE), World.ViewPortSize.height / VIEW_SCALE));

            const float VIEWPORT_PADDING_AMT = 1.5f;
            var inflateAmt = VIEWPORT_PADDING_AMT * zAmt;
            viewPortRect = viewPortRect.Inflate(viewPortRect.Width * inflateAmt, viewPortRect.Height * inflateAmt, keepAspectRatio: true); // Inflate slightly to prevent "pop-in".

            var objsInViewport = _objs.GetInViewport(ctx.Viewport).Where(o => o is not Explosion);

            // Update the light map.
            if (World.UseLightMap)
            {
                // Clear the map and resize as needed.
                ctx.LightMap.Clear(viewPortRect);

                // Add objects in viewport.
                ctx.LightMap.AddContributions(objsInViewport);

                // Add explosions separately as they have special viewport clipping.
                ctx.LightMap.AddContributions(_objs.Explosions);
            }

            objsInViewport = objsInViewport.OrderBy(o => o.RenderOrder);

            //var shadowColor = ctx.GetShadowColor();
            var todAngle = ctx.GetTimeOfDaySunAngle();

            ctx.PushViewPort(viewPortRect);

            DrawGround(_ctx, viewObj.Position);

            Debug.WriteLine($"{Math.Round(_renderFPSSmooth.Current, 1)}   ({objsInViewport.Count()})");


            foreach (var obj in objsInViewport)
            {
                if (obj is FighterPlane p)
                {
                    p.RenderGL(ctx);
                }
                else
                {
                    obj.RenderGL(ctx);
                }
            }




            DrawClouds(ctx);

            //viewPlane.RenderGL(_ctx);

            ctx.PopViewPort();
            ctx.PopTransform();
        }


        private void DrawClouds(GLRenderContext ctx)
        {
            _cloudManager.RenderGL(ctx);
        }

        private void DrawSky(GLRenderContext ctx, GameObject viewObject)
        {
            const float MAX_ALT_OFFSET = 50000f;

            var plrAlt = viewObject.Altitude;
            if (viewObject.Position.Y >= 0)
                plrAlt = 0f;

            var color1 = _skyColorLight;
            var color2 = _skyColorDark;
            var color = Utilities.LerpColor(color1, color2, (plrAlt / (World.MAX_ALTITUDE - MAX_ALT_OFFSET)));

            // Add time of day color.
            color = Utilities.LerpColor(color, SKColors.Black, Utilities.Factor(World.TimeOfDay, World.MAX_TIMEOFDAY - 5f));
            color = Utilities.LerpColor(color, ctx.AddTimeOfDayColor(color), 0.2f);

            //var rect = new D2DRect(new D2DPoint(this.Width * 0.5f, this.Height * 0.5f), new D2DSize(this.Width, this.Height));
            //var rect =  SKRect.Create(new SKPoint(this.Width * 0.5f, this.Height * 0.5f), new SKSize(this.Width, this.Height));
            var rect = SKRect.Create(new SKPoint(0f, 0f), new SKSize(this.Width, this.Height));

            ctx.FillRectangle(rect, color);
        }

        private void DrawGround(GLRenderContext ctx, D2DPoint position)
        {
            //var groundPos = new D2DPoint(position.X, 0f);
            var groundPos = new D2DPoint(ctx.Viewport.left, 0f);

            //var groundPos = new D2DPoint(0f, 0f);

            if (!ctx.Viewport.Contains(groundPos))
                return;

            const float HEIGHT = 500f;
            //var yPos = HEIGHT / ctx.CurrentScale;
            //var yPos = HEIGHT / World.ZoomScale;
            var yPos = HEIGHT;

            //groundPos += new D2DPoint(0f, yPos * World.ZoomScale);
            //groundPos += new D2DPoint(0f, yPos);

            // Draw the ground.
            //ctx.Gfx.FillRectangle(new D2DRect(groundPos, new D2DSize(this.Width * World.ViewPortScaleMulti, (HEIGHT * 2f) / ctx.CurrentScale)), _groundBrush);
            //var rect = SKRect.Create(groundPos, new SKSize(this.Width * World.ViewPortScaleMulti, (HEIGHT * 2f) / ctx.CurrentScale));

            //var rect = SKRect.Create(groundPos, new SKSize(this.Width * (World.ViewPortScaleMulti * 2f), (HEIGHT * 2f)));
            var rect = SKRect.Create(groundPos, new SKSize(World.ViewPortRect.Width, (HEIGHT * 2f) / ctx.CurrentScale));

            ctx.FillRectangle(rect, _groundBrush);

        }

        private void UpdateGroundColorBrush(GLRenderContext ctx)
        {
            _groundBrush?.Dispose();
            _groundBrush = new SKPaint() { Shader = SKShader.CreateLinearGradient(new SKPoint(0f, 50f), new SKPoint(0f, 4000f), [ctx.AddTimeOfDayColor(_groundColorDark), ctx.AddTimeOfDayColor(_groundColorLight)], SKShaderTileMode.Clamp) };
            //_groundBrush = new SKPaint() { Color = SKColors.LawnGreen };



            //_groundBrush?.Dispose();
            //_groundBrush = _ctx.Device.CreateLinearGradientBrush(new D2DPoint(0f, 50f), new D2DPoint(0f, 4000f), [new D2DGradientStop(0.2f, ctx.AddTimeOfDayColor(_groundColorDark)), new D2DGradientStop(0.1f, ctx.AddTimeOfDayColor(_groundColorLight))]);
        }


        private void DrawMovingBackground(GLRenderContext ctx, GameObject viewObject)
        {
            float spacing = 75f;
            const float size = 4f;
            var d2dSz = new SKSize(size, size);
            //var color = new D2DColor(0.3f, D2DColor.Gray);
            var color = SKColors.Gray.WithAlpha(127);

            var plrPos = viewObject.Position;
            plrPos /= World.ViewPortScaleMulti;

            var roundPos = new D2DPoint((plrPos.X) % spacing, (plrPos.Y) % spacing);
            roundPos *= 3f;

            var rect = new D2DRect(0, 0, this.Width, this.Height);

            for (float x = -spacing * 2f; x < this.Width + roundPos.X; x += spacing)
            {
                for (float y = -spacing * 2f; y < this.Height + roundPos.Y; y += spacing)
                {
                    var pos = new D2DPoint(x, y);
                    pos -= roundPos;

                    if (rect.Contains(pos))
                        ctx.FillRectangle(SKRect.Create(pos, d2dSz), color);
                }
            }
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
