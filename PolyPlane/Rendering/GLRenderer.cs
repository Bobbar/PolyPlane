//using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.ES20;
using OpenTK.Platform.Windows;
using PolyPlane.GameObjects;
using PolyPlane.GameObjects.Animations;
using PolyPlane.GameObjects.Managers;
using PolyPlane.Helpers;
using PolyPlane.Net;
using SkiaSharp;
using SkiaSharp.Internals;
using SkiaSharp.Views.Desktop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using unvell.D2DLib;

namespace PolyPlane.Rendering
{
    public sealed class GLRenderer : IDisposable
    {
        private GLRenderContext _ctx = new GLRenderContext();
        private Action? _renderDelegate = null;

        private OpenTK.GLControl _glControl;
        private const SKColorType colorType = SKColorType.Rgba8888;
        private const GRSurfaceOrigin surfaceOrigin = GRSurfaceOrigin.BottomLeft;

        private GRContext grContext;
        private GRGlFramebufferInfo glInfo;
        private GRBackendRenderTarget renderTarget;
        private SKSurface surface;
        private SKCanvas canvas;

        private SKSizeI lastSize;

        private Form? _targetForm;

        private int Width => (int)(_targetForm.ClientSize.Width / (_targetForm.DeviceDpi / DEFAULT_DPI));
        private int Height => (int)(_targetForm.ClientSize.Height / (_targetForm.DeviceDpi / DEFAULT_DPI));

        private float _groundColorTOD = 0f;


        private const float VIEW_SCALE = 4f;
        private const float DEFAULT_DPI = 96f;
        private const float ZOOM_FACTOR = 0.07f; // Effects zoom in/out speed.

        private readonly SKSize _healthBarSize = new SKSize(80, 20);


        private readonly SKColor _skyColorLight = SKColors.SkyBlue.WithAlpha(127);
        private readonly SKColor _skyColorDark = SKColors.Black.WithAlpha(127);
        private readonly SKColor _clearColor = SKColors.Transparent;
        //private readonly D2DColor _groundImpactOuterColor = new D2DColor(1f, 0.56f, 0.32f, 0.18f);
        //private readonly D2DColor _groundImpactInnerColor = new D2DColor(1f, 0.35f, 0.2f, 0.1f);

        private readonly SKColor _groundImpactOuterColor = Utilities.SKColorFromFloats(0.56f, 0.32f, 0.18f, 1f);
        private readonly SKColor _groundImpactInnerColor = Utilities.SKColorFromFloats(0.35f, 0.2f, 0.1f, 1f);
        private readonly SKColor _groundLightColor = Utilities.SKColorFromFloats(0.85f, 0.76f, 0.14f, 1f);


        private float _currentDPI = DEFAULT_DPI;

        private GameObjectManager _objs = World.ObjectManager;
        private NetEventManager _netMan;


        private SKColor _groundColorLight = new SKColor(0, 74, 0, 255);
        private SKColor _groundColorDark = SKColors.DarkGreen;
        private SKColor _screenFlashColor = SKColors.Red;

        private SKPaint _groundBrush;
        //private SKPaint _muzzleFlashBrush = new SKPaint() { IsAntialias = true, Shader = SKShader.CreateRadialGradient(SKPoint.Empty, MUZZ_FLASH_RADIUS, [SKColors.Transparent, SKColors.Orange.WithAlpha(0.2f)], SKShaderTileMode.Clamp) };
        private SKPaint? _muzzleFlashBrush = null;


        private SKPaint _hudDashedLine = new SKPaint() { IsAntialias = true, StrokeWidth = 1f, Color = World.HudColor.ToSKColor(), PathEffect = SKPathEffect.CreateDash([2f, 2f], 4f) };
        private SKPaint _hudColorBrush = new SKPaint() { IsAntialias = true, Color = World.HudColor.ToSKColor() };
        private SKPaint _greenYellowColorBrush = new SKPaint() { IsAntialias = true, Color = SKColors.GreenYellow };
        private SKPaint? _bulletLightingBrush = null;
        private SKPaint? _missileLightingBrush = null;
        private SKPaint? _decoyLightBrush = null;

        private SKFont _textConsolas12 = new SKFont(SKTypeface.FromFamilyName("Consolas"), 12f);
        private SKFont _textConsolas15 = new SKFont(SKTypeface.FromFamilyName("Consolas"), 15f);
        private SKFont _textConsolas30 = new SKFont(SKTypeface.FromFamilyName("Consolas"), 30f);


        private const float GROUND_TOD_INTERVAL = 0.1f; // Update ground brush when elapsed TOD exceeds this amount.

        private const int NUM_CLOUDS = 2000;
        private const int NUM_TREES = 1000;
        private const float MUZZ_FLASH_RADIUS = 60f;


        private int _scoreScrollPos = 0;
        private float _hudScale = 1f;
        private double _renderFPS = 0f;
        private float _screenFlashOpacity = 0f;
        private float _warnLightFlashAmount = 1f;
        private uint _numObjectsOnScreen = 0;
        private bool _showHUD = true;
        private bool _showHelp = false;
        private bool _showInfo = false;
        private bool _showScore = false;


        //private SmoothDouble _renderTimeSmooth = new SmoothDouble(10);
        //private Stopwatch _timer = new Stopwatch();

        private double _lastRenderTime = 0;
        private SmoothDouble _renderFPSSmooth = new SmoothDouble(60);
        private SmoothDouble _renderTimeSmooth = new SmoothDouble(60);
        private SmoothDouble _updateTimeSmooth = new SmoothDouble(60);
        private SmoothDouble _collisionTimeSmooth = new SmoothDouble(60);

        private CloudManager _cloudManager = new();
        private TreeManager _treeManager = new();
        private ContrailBox _contrailBox = new ContrailBox();

        private StringBuilder _stringBuilder = new StringBuilder();

        private Vector2 _screenShakeTrans = Vector2.Zero;


        private FloatAnimation _screenShakeX;
        private FloatAnimation _screenShakeY;
        private FloatAnimation _screenFlash;
        private FloatAnimation _warnLightFlash;

        private GameObject? _viewObject = null;

        public GLRenderer(Control renderTarget, NetEventManager netMan)
        {
            _groundColorTOD = World.TimeOfDay;
            _netMan = netMan;

        }

        public Control InitGLControl(Control targetControl)
        {
            targetControl.Visible = false;
            targetControl.SendToBack();

            var form = targetControl.FindForm();
            _targetForm = form;

            _glControl = new OpenTK.GLControl();
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
            _treeManager.GenTreesGL(rnd, NUM_TREES, ctx);

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

        private void InitOtherStuff()
        {
            _screenFlash = new FloatAnimation(0.4f, 0f, 4f, EasingFunctions.Out.EaseCircle, v => _screenFlashOpacity = v);
            _screenShakeX = new FloatAnimation(5f, 0f, 2f, EasingFunctions.Out.EaseCircle, v => _screenShakeTrans.X = v);
            _screenShakeY = new FloatAnimation(5f, 0f, 2f, EasingFunctions.Out.EaseCircle, v => _screenShakeTrans.Y = v);

            _warnLightFlash = new FloatAnimation(0f, 1f, 0.3f, EasingFunctions.EaseLinear, v => _warnLightFlashAmount = v);
            _warnLightFlash.Loop = true;
            _warnLightFlash.ReverseOnLoop = true;
            _warnLightFlash.Start();


            InitProceduralGenStuff(_ctx);
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

        private void HandleCreated(object? sender, EventArgs e)
        {
            InitGfx();

            InitOtherStuff();
        }


        private void UpdateTimersAndAnims(float dt)
        {
            //_hudMessageTimeout.Update(dt);

            _screenFlash.Update(dt);
            _screenShakeX.Update(dt);
            _screenShakeY.Update(dt);
            _warnLightFlash.Update(World.DEFAULT_DT);

            _contrailBox.Update(_objs.Planes, dt);

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
            try
            {
                if (_renderDelegate == null)
                    _renderDelegate = new Action(Render);

                _viewObject = viewObject;

                // Invoke the render call on the UI thread.
                _targetForm?.Invoke(_renderDelegate);
            }
            catch (ObjectDisposedException)
            {
                // Catch disposed exceptions.
            }
        }

        private void Render()
        {
            var viewObject = _viewObject;
            var dt = World.CurrentDT;

            if (canvas == null)
                return;

            Profiler.Start(ProfilerStat.Update);
            UpdateTimersAndAnims(dt);
            Profiler.StopAndAppend(ProfilerStat.Update);


            Profiler.Start(ProfilerStat.Render);

            using (new SKAutoCanvasRestore(canvas, true))
            {
                _ctx.BeginRender(_clearColor);

                if (viewObject != null)
                {
                    // Do G-Force screen shake effect for planes.
                    if (viewObject is FighterPlane plane)
                    {
                        if (plane.GForce > World.SCREEN_SHAKE_G)
                            DoScreenShake(plane.GForce / 4f);
                    }


                    var viewPortSize = new D2DSize((World.ViewPortSize.width / VIEW_SCALE), World.ViewPortSize.height / VIEW_SCALE);
                    var viewPortRect = new D2DRect(viewObject.Position, viewPortSize);
                    _ctx.PushViewPort(viewPortRect);

                    DrawSky(_ctx, viewObject);

                    // Push screen shake transform.
                    _ctx.PushTransform();
                    _ctx.TranslateTransform(_screenShakeTrans);

                    DrawMovingBackground(_ctx, viewObject);

                    _ctx.PushTransform();
                    _ctx.ScaleTransform(World.ZoomScale);

                    DrawPlayerView(_ctx, viewObject);

                    // Pop scale transform.
                    _ctx.PopTransform();

                    // Draw HUD.
                    DrawHud(_ctx, viewObject, dt);

                    // Pop screen shake transform.
                    _ctx.PopTransform();

                    // Chat and event box.
                    //DrawChatAndEventBox(_ctx);

                    // Draw overlay text. (FPS, Help and Info)
                    DrawOverlays(_ctx, viewObject);

                    // And finally screen flash.
                    //DrawScreenFlash(_gfx);

                    _ctx.PopViewPort();
                }
            }


            canvas.Flush();

            _renderTimeSmooth.Add(Profiler.Stop(ProfilerStat.Render).GetElapsedMilliseconds());


            //GL.Finish();
            _glControl.SwapBuffers();


            var now = World.CurrentTimeMs();
            var elap = now - _lastRenderTime;
            _lastRenderTime = now;
            var fps = 1000d / elap;
            _renderFPS = fps;
        }


        private void DrawPlayerView(GLRenderContext ctx, GameObject viewObj)
        {
            uint numOnScreen = 0;

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

            //var objsInViewport = _objs.GetInViewport(ctx.Viewport).Where(o => o is not Explosion);
            var objsInViewport = _objs.GetInViewport(ctx.Viewport);

            // Update the light map.
            if (World.UseLightMap)
            {
                // Clear the map and resize as needed.
                ctx.LightMap.Clear(viewPortRect);

                // Add objects in viewport.
                ctx.LightMap.AddContributions(objsInViewport);

                //// Add explosions separately as they have special viewport clipping.
                //ctx.LightMap.AddContributions(_objs.Explosions);
            }

            //objsInViewport = objsInViewport.OrderBy(o => o.RenderOrder);
            var objsInViewportSorted = objsInViewport.OrderBy(o => o.RenderOrder);

            var shadowColor = ctx.GetShadowColor();
            var todAngle = ctx.GetTimeOfDaySunAngle();

            ctx.PushViewPort(viewPortRect);

            DrawGround(ctx, viewObj.Position);
            DrawGroundImpacts(ctx);
            DrawTrees(ctx);
            DrawPlaneGroundShadows(ctx, shadowColor, todAngle);

            for (int i = 0; i < _objs.MissileTrails.Count; i++)
                _objs.MissileTrails[i].RenderGL(ctx);

            _contrailBox.RenderGL(ctx);

            foreach (var obj in objsInViewportSorted)
            {
                numOnScreen++;

                if (obj is FighterPlane p)
                {
                    p.RenderGL(ctx);

                    // TODO: Kinda broken.
                    DrawMuzzleFlash(ctx, p);

                    if (viewPlane != null)
                    {
                        // Draw health bars for other planes.
                        if (!p.Equals(viewPlane))
                            DrawPlaneHealthBar(ctx, p, new D2DPoint(p.Position.X, p.Position.Y - 110f));

                        // Draw circle around locked on plane.
                        if (viewPlane.Radar.LockedObj != null && viewPlane.Radar.LockedObj.Equals(p))
                            ctx.DrawCircle(p.Position, 80f, World.HudColor.ToSKColor(), 4f);
                    }
                }
                else if (obj is GuidedMissile missile)
                {
                    missile.RenderGL(ctx);

                    // Circle enemy missiles.
                    if (!World.FreeCameraMode && !viewObj.Equals(missile) && !missile.Owner.Equals(viewPlane))
                        ctx.DrawCircle(missile.Position, 50f, SKColors.Red.WithAlpha(0.4f), 8f);

                    DrawLightFlareEffects(ctx, missile);
                }
                else if (obj is Explosion)
                {
                    // Skip explosions. Render later.
                    continue;
                }
                else
                {
                    obj.RenderGL(ctx);

                    DrawLightFlareEffects(ctx, obj);
                }
            }


            // Render explosions separate so that they can clip to the viewport correctly.
            for (int i = 0; i < _objs.Explosions.Count; i++)
                _objs.Explosions[i].RenderGL(ctx);

            DrawClouds(ctx);
            DrawPlaneCloudShadows(ctx, shadowColor, objsInViewport);
            //DrawLightFlareEffects(ctx, objsInViewport);


            ctx.PopViewPort();
            ctx.PopTransform();

            _numObjectsOnScreen = numOnScreen;
        }

        private void DrawLightFlareEffects(GLRenderContext ctx, GameObject obj)
        {
            const float BULLET_LIGHT_RADIUS = 60f;
            if (_bulletLightingBrush == null)
                _bulletLightingBrush = new SKPaint() { BlendMode = SKBlendMode.Multiply, IsAntialias = true, Shader = SKShader.CreateRadialGradient(SKPoint.Empty, BULLET_LIGHT_RADIUS, [SKColors.Yellow.WithAlpha(0.2f), SKColors.Transparent], [0.2f, 1.4f], SKShaderTileMode.Clamp) };

            const float MISSILE_LIGHT_RADIUS = 70f;
            if (_missileLightingBrush == null)
                _missileLightingBrush = new SKPaint() { BlendMode = SKBlendMode.Multiply, IsAntialias = true, Shader = SKShader.CreateRadialGradient(SKPoint.Empty, MISSILE_LIGHT_RADIUS, [SKColors.Yellow.WithAlpha(0.2f), SKColors.Transparent], [0f, 1.4f], SKShaderTileMode.Clamp) };

            const float DECOY_LIGHT_RADIUS = 90f;
            if (_decoyLightBrush == null)
                _decoyLightBrush = new SKPaint() { BlendMode = SKBlendMode.Multiply, IsAntialias = true, Shader = SKShader.CreateRadialGradient(SKPoint.Empty, DECOY_LIGHT_RADIUS, [SKColors.LightYellow.WithAlpha(0.2f), SKColors.Transparent], [0f, 1.4f], SKShaderTileMode.Clamp) };


            if (obj is Bullet bullet)
            {
                ctx.PushTransform();
                ctx.TranslateTransform(bullet.Position);
                ctx.FillCircle(Vector2.Zero, BULLET_LIGHT_RADIUS, _bulletLightingBrush);
                ctx.PopTransform();

                DrawObjectGroundLight(ctx, bullet);
            }
            else if (obj is GuidedMissile missile)
            {
                if (missile.FlameOn && missile.CurrentFuel > 0f)
                {
                    ctx.PushTransform();
                    ctx.TranslateTransform(missile.CenterOfThrust);

                    // Add a little flicker effect to missile lights.
                    var flickerScale = Utilities.Rnd.NextFloat(0.7f, 1f);
                    ctx.ScaleTransform(flickerScale);

                    ctx.FillCircle(Vector2.Zero, MISSILE_LIGHT_RADIUS, _missileLightingBrush);

                    ctx.PopTransform();

                    DrawObjectGroundLight(ctx, missile);
                }
            }
            else if (obj is Decoy decoy)
            {
                if ((decoy.IsFlashing()))
                {
                    ctx.PushTransform();
                    ctx.TranslateTransform(decoy.Position);

                    ctx.FillCircle(Vector2.Zero, DECOY_LIGHT_RADIUS, _decoyLightBrush);

                    ctx.PopTransform();

                    DrawObjectGroundLight(ctx, decoy);
                }
            }
        }

        //private void DrawLightFlareEffects(GLRenderContext ctx, IEnumerable<GameObject> objs)
        //{
        //    const float BULLET_LIGHT_RADIUS = 60f;
        //    if (_bulletLightingBrush == null)
        //        _bulletLightingBrush = new SKPaint() { BlendMode = SKBlendMode.Multiply, IsAntialias = true, Shader = SKShader.CreateRadialGradient(SKPoint.Empty, BULLET_LIGHT_RADIUS, [SKColors.Yellow.WithAlpha(0.2f), SKColors.Transparent], [0.2f, 1.4f], SKShaderTileMode.Clamp) };

        //    const float MISSILE_LIGHT_RADIUS = 70f;
        //    if (_missileLightingBrush == null)
        //        _missileLightingBrush = new SKPaint() { BlendMode = SKBlendMode.Multiply, IsAntialias = true, Shader = SKShader.CreateRadialGradient(SKPoint.Empty, MISSILE_LIGHT_RADIUS, [SKColors.Yellow.WithAlpha(0.2f), SKColors.Transparent], [0f, 1.4f], SKShaderTileMode.Clamp) };

        //    const float DECOY_LIGHT_RADIUS = 90f;
        //    if (_decoyLightBrush == null)
        //        _decoyLightBrush = new SKPaint() { BlendMode = SKBlendMode.Multiply, IsAntialias = true, Shader = SKShader.CreateRadialGradient(SKPoint.Empty, DECOY_LIGHT_RADIUS, [SKColors.LightYellow.WithAlpha(0.2f), SKColors.Transparent], [0f, 1.4f], SKShaderTileMode.Clamp) };


        //    foreach (var obj in objs)
        //    {
        //        if (obj is Bullet bullet)
        //        {
        //            ctx.PushTransform();
        //            ctx.TranslateTransform(bullet.Position);
        //            ctx.FillCircle(Vector2.Zero, BULLET_LIGHT_RADIUS, _bulletLightingBrush);
        //            ctx.PopTransform();

        //            DrawObjectGroundLight(ctx, bullet);
        //        }
        //        else if (obj is GuidedMissile missile)
        //        {
        //            if (missile.FlameOn && missile.CurrentFuel > 0f)
        //            {
        //                ctx.PushTransform();
        //                ctx.TranslateTransform(missile.CenterOfThrust);

        //                // Add a little flicker effect to missile lights.
        //                var flickerScale = Utilities.Rnd.NextFloat(0.7f, 1f);
        //                ctx.ScaleTransform(flickerScale);

        //                ctx.FillCircle(Vector2.Zero, MISSILE_LIGHT_RADIUS, _missileLightingBrush);

        //                ctx.PopTransform();

        //                DrawObjectGroundLight(ctx, missile);
        //            }
        //        }
        //        else if (obj is Decoy decoy)
        //        {
        //            if ((decoy.IsFlashing()))
        //            {
        //                ctx.PushTransform();
        //                ctx.TranslateTransform(decoy.Position);

        //                ctx.FillCircle(Vector2.Zero, DECOY_LIGHT_RADIUS, _decoyLightBrush);

        //                ctx.PopTransform();

        //                DrawObjectGroundLight(ctx, decoy);
        //            }
        //        }
        //    }
        //}

        private void DrawObjectGroundLight(GLRenderContext ctx, GameObject obj)
        {
            const float MAX_SIZE_ALT = 300f;
            const float MAX_SHOW_ALT = 600f;
            const float Y_POS = 25f;
            const float MAX_WIDTH = 220f;
            const float HEIGHT = 15f;

            if (obj.Altitude > MAX_SHOW_ALT)
                return;

            var lightWidth = Utilities.Lerp(1f, MAX_WIDTH, Utilities.FactorWithEasing(MAX_SIZE_ALT, obj.Altitude, EasingFunctions.EaseLinear));
            var lightAlpha = 0.2f * (1f - Utilities.FactorWithEasing(obj.Altitude, MAX_SHOW_ALT, EasingFunctions.In.EaseSine));
            var lightPos = Utilities.GroundIntersectionPoint(obj, 90f);
            lightPos += new D2DPoint(0f, Y_POS);

            if (obj.Altitude <= 0f)
                lightWidth = MAX_WIDTH;

            //ctx.FillEllipse(new D2DEllipse(lightPos, new D2DSize(lightWidth, HEIGHT)), _groundLightColor.WithAlpha(lightAlpha));
            ctx.FillEllipse(lightPos, new SKSize(lightWidth, HEIGHT), _groundLightColor.WithAlpha(lightAlpha));
        }


        private void DrawPlaneHealthBar(GLRenderContext ctx, FighterPlane plane, D2DPoint position)
        {
            if (!ctx.Viewport.Contains(position))
                return;

            var size = _healthBarSize;
            var healthPct = plane.Health / FighterPlane.MAX_HEALTH;

            if (healthPct > 0f && healthPct < 0.05f)
                healthPct = 0.05f;

            var color = World.HudColor.ToSKColor();
            ctx.DrawProgressBar(position, size, color, color, healthPct);

            // Draw player name.
            if (string.IsNullOrEmpty(plane.PlayerName))
                return;

            var rect = new D2DRect(position + new D2DPoint(0, -40), new D2DSize(300, 100));
            ctx.DrawText(plane.PlayerName, position + new D2DPoint(0, -40), SKTextAlign.Center, _textConsolas30, _hudColorBrush);
        }



        private void DrawHud(GLRenderContext ctx, GameObject viewObject, float dt)
        {
            var viewportsize = World.ViewPortRectUnscaled.Size;

            ctx.PushViewPort(World.ViewPortRectUnscaled);
            ctx.PushTransform();
            ctx.ScaleTransform(_hudScale, new D2DPoint(viewportsize.width * 0.5f, viewportsize.height * 0.5f));

            if (_showHUD)
            {
                var speedoPos = new D2DPoint(viewportsize.width * 0.15f, viewportsize.height * 0.33f);
                var altimeterPos = new D2DPoint(viewportsize.width * 0.85f, viewportsize.height * 0.33f);

                // Draw altimeter and speedo.
                DrawTapeIndicator(ctx, viewportsize.ToSKSize(), altimeterPos, viewObject.Altitude, 3000f, 175f);
                DrawTapeIndicator(ctx, viewportsize.ToSKSize(), speedoPos, viewObject.AirSpeedIndicated, 250f, 50f);

                //if (viewObject is FighterPlane plane)
                //{
                //    // Draw g-force.
                //    var gforceRect = new D2DRect(new D2DPoint(speedoPos.X, speedoPos.Y - 195f), new D2DSize(60f, 20f));
                //    ctx.DrawText($"G {Math.Round(plane.GForce, 1)}", _hudColorBrush, _textConsolas15, gforceRect);

                //    if (!plane.IsDisabled)
                //    {
                //        if (plane.IsAI == false)
                //        {
                //            DrawGuideIcon(ctx, viewportsize, plane);
                //            DrawGroundWarning(ctx, viewportsize, plane);
                //        }

                //        DrawPlanePointers(ctx, viewportsize, plane, dt);
                //        DrawMissilePointersAndWarnings(ctx, viewportsize, plane);
                //    }

                //    DrawHudMessage(ctx.Gfx, viewportsize);
                //    DrawRadar(ctx, viewportsize, plane);

                //    var healthBarSize = new D2DSize(300, 30);
                //    var pos = new D2DPoint(viewportsize.width * 0.5f, viewportsize.height - (viewportsize.height * 0.85f));
                //    DrawHealthBarAndAmmo(ctx, plane, pos, healthBarSize);

                //    DrawPopMessages(ctx, viewportsize, plane);
                //}
            }

            //if (_showScore)
            //    DrawScoreCard(ctx, viewportsize);

            ctx.PopTransform();
            ctx.PopViewPort();

            //if (World.FreeCameraMode)
            //    DrawFreeCamPrompt(ctx);
        }


        /// <summary>
        /// Draws a tape style indicator for the specified value.
        /// </summary>
        /// <param name="ctx">Render context.</param>
        /// <param name="viewportsize">Viewport size.</param>
        /// <param name="pos">Position within the viewport.</param>
        /// <param name="value">Current value.</param>
        /// <param name="minValue">Minimum value. The background changes to red below this value.</param>
        /// <param name="markerRange">Step size for markers.</param>
        private void DrawTapeIndicator(GLRenderContext ctx, SKSize viewportsize, SKPoint pos, float value, float minValue, float markerRange)
        {
            const float W = 80f;
            const float H = 350f;
            const float HalfW = W * 0.5f;
            const float HalfH = H * 0.5f;


            var rect = SKRect.Create(new SKPoint(pos.X - HalfW, pos.Y - HalfH), new SKSize(W, H));

            var startValue = (value) - (value % (markerRange)) + markerRange;
            var valueWarningColor = SKColors.Red.WithAlpha(0.2f);


            var highestVal = startValue + markerRange;
            var lowestVal = (startValue - HalfH) - markerRange;

            ctx.DrawRectangle(rect, World.HudColor.ToSKColor());
            ctx.DrawLine(new D2DPoint(pos.X - HalfW, pos.Y), new D2DPoint(pos.X + HalfW, pos.Y), World.HudColor.WithAlpha(1f).ToSKColor(), 1f);

            if (highestVal <= minValue || lowestVal <= minValue)
            {
                var s = new D2DPoint(pos.X - HalfW, (pos.Y + (value - minValue)));

                if (s.Y < pos.Y - HalfH)
                    s.Y = pos.Y - HalfH;

                //var sRect = new D2DRect(s.X, s.Y, W, (pos.Y + (H * 0.5f)) - s.Y);
                var sRect = SKRect.Create(s.X, s.Y, W, (pos.Y + (H * 0.5f)) - s.Y);

                if (sRect.Height > 0f)
                    ctx.FillRectangle(sRect, valueWarningColor);
            }


            for (float y = -markerRange; y < H; y += markerRange)
            {
                var posY = (pos.Y - y + HalfH - markerRange) + (value % markerRange);

                if (posY <= (pos.Y - HalfH) - markerRange)
                    continue;

                if (posY >= (pos.Y + HalfH))
                    continue;

                var start = new D2DPoint(pos.X - HalfW, posY);
                var end = new D2DPoint(pos.X + HalfW, posY);

                var div = y / markerRange;
                var markerValue = Math.Round(startValue + (-HalfH + (div * markerRange)), 0);

                if (rect.Contains(start))
                    ctx.DrawLine(start, end, _hudDashedLine);

                if (markerValue >= 0f)
                {
                    // Fade in marker text as they move towards the center.
                    var alpha = Math.Clamp(1f - Utilities.FactorWithEasing(Math.Abs(pos.Y - posY), HalfH, EasingFunctions.Out.EaseSine), 0.02f, 0.4f);
                    var textRect = new D2DRect(start - new D2DPoint(25f, 0f), new D2DSize(60f, 30f));
                    ctx.DrawText(markerValue.ToString(), start - new D2DPoint(25f, 0f), SKTextAlign.Center, _textConsolas15, World.HudColor.WithAlpha(alpha).ToSKColor());
                }
            }

            var curValueRect = new D2DRect(new D2DPoint(pos.X, pos.Y + HalfH + 20f), new D2DSize(60f, 20f));
            ctx.DrawText(Math.Round(value, 0).ToString(), new D2DPoint(pos.X, pos.Y + HalfH + 20f), SKTextAlign.Center, _textConsolas15, _hudColorBrush);


        }


        private void DrawMuzzleFlash(GLRenderContext ctx, FighterPlane plane)
        {
            if (!ctx.Viewport.Contains(plane.Gun.Position))
                return;

            if (_muzzleFlashBrush == null)
            {
                _muzzleFlashBrush = new SKPaint()
                {
                    BlendMode = SKBlendMode.Multiply,
                    IsAntialias = true,
                    Shader = SKShader.CreateRadialGradient(
                SKPoint.Empty,
                50f,
                [
                    SKColors.Orange.WithAlpha(0.2f),
                    SKColors.Transparent,
                ],
                [
                    0f,
                    1f
                ],
                SKShaderTileMode.Clamp)
                };
            }

            if (plane.Gun.MuzzleFlashOn)
            {
                ctx.PushTransform();
                ctx.TranslateTransform(plane.GunPosition);
                ctx.FillCircle(SKPoint.Empty, MUZZ_FLASH_RADIUS, _muzzleFlashBrush);
                ctx.PopTransform();
            }

        }


        private void DrawClouds(GLRenderContext ctx)
        {
            _cloudManager.RenderGL(ctx);
        }

        private void DrawPlaneCloudShadows(GLRenderContext ctx, SKColor shadowColor, IEnumerable<GameObject> objs)
        {
            // Don't bother if we are currently zoomed way out.
            if (World.ZoomScale < 0.03f)
                return;

            var color = shadowColor.WithAlpha(0.07f);

            foreach (var obj in objs)
            {
                if (obj is FighterPlane plane)
                    ctx.FillPolygon(plane.Polygon.Poly, color);
            }
        }

        private void DrawTrees(GLRenderContext ctx)
        {
            _treeManager.RenderGL(ctx);
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

            //using (var testBrush = new SKPaint() { Color = SKColors.WhiteSmoke.WithAlpha(0.4f), Shader = SKShader.CreatePerlinNoiseFractalNoise(0.5f, 0.5f, 4, Utilities.Rnd.NextFloat(0f, 20f)) })
            //{
            //    ctx.FillRectangle(rect, color);
            //    ctx.FillRectangle(rect, testBrush);

            //}


            ctx.FillRectangle(rect, color);
        }

        private void DrawGround(GLRenderContext ctx, D2DPoint position)
        {
            var groundPos = new D2DPoint(ctx.Viewport.left, 0f);

            if (!ctx.Viewport.Contains(groundPos))
                return;

            const float HEIGHT = 500f;
            var rect = SKRect.Create(groundPos, new SKSize(World.ViewPortRect.Width, (HEIGHT * 2f) / ctx.CurrentScale));

            ctx.DrawRectangle(rect, _groundBrush);
        }


        private void DrawGroundImpacts(GLRenderContext ctx)
        {
            const float LIGHT_INTENSITY = 0.4f;

            var groundPos = new D2DPoint(ctx.Viewport.left, 0f);
            var rect = SKRect.Create(groundPos, new SKSize(World.ViewPortRect.Width, 4000f));

            ctx.Gfx.Save();
            ctx.Gfx.ClipRect(rect, SKClipOperation.Intersect, antialias: true);

            for (int i = 0; i < _objs.GroundImpacts.Count; i++)
            {
                var impact = _objs.GroundImpacts[i];

                if (ctx.Viewport.Contains(impact.Position))
                {
                    ctx.PushTransform();

                    ctx.RotateTransform(impact.Angle, impact.Position);

                    float ageAlpha = 1f;

                    if (impact.Age >= GroundImpact.START_FADE_AGE)
                        ageAlpha = 1f - Utilities.FactorWithEasing(impact.Age - GroundImpact.START_FADE_AGE, GroundImpact.MAX_AGE - GroundImpact.START_FADE_AGE, EasingFunctions.In.EaseExpo);

                    ctx.FillEllipseWithLighting(impact.Position, new SKSize(impact.Size.width + 4f, impact.Size.height + 4f), _groundImpactOuterColor.WithAlpha(ageAlpha), LIGHT_INTENSITY);
                    ctx.FillEllipseWithLighting(impact.Position, new SKSize(impact.Size.width, impact.Size.height), _groundImpactInnerColor.WithAlpha(ageAlpha), LIGHT_INTENSITY);

                    ctx.PopTransform();
                }
            }

            ctx.Gfx.Restore();
        }

        private void DrawPlaneGroundShadows(GLRenderContext ctx, SKColor shadowColor, float todAngle)
        {
            const float WIDTH_PADDING = 20f;
            const float MAX_WIDTH = 120f;
            const float HEIGHT = 10f;
            const float MAX_SIZE_ALT = 500f;
            const float MAX_SHOW_ALT = 2000f;
            const float Y_POS = 15f;

            for (int i = 0; i < _objs.Planes.Count; i++)
            {
                var plane = _objs.Planes[i];

                if (plane.Altitude > MAX_SHOW_ALT)
                    continue;

                // Find the ground intersection point for the shadow position.
                var shadowPos = Utilities.GroundIntersectionPoint(plane, todAngle);

                // Move the shadow position down to the desired Y position.
                shadowPos += new D2DPoint(0f, Y_POS);

                // Make sure it is inside the viewport.
                if (!ctx.Viewport.Contains(shadowPos))
                    continue;

                // Offset ToD angle by plane rotation.
                var todRotationOffset = Utilities.ClampAngle(todAngle - plane.Rotation);

                // Make a line segment to represent the plane's rotation in relation to the angle of the sun.
                var lineA = new D2DPoint(0f, 0f);
                var lineB = new D2DPoint(0f, MAX_WIDTH);

                // Rotate the segment.
                lineA = lineA.Translate(todRotationOffset, D2DPoint.Zero);
                lineB = lineB.Translate(todRotationOffset, D2DPoint.Zero);

                // Get the abs diff between the X coords of the line to compute the initial shadow width.
                var width = Math.Abs(lineB.X - lineA.X);
                var initialWidth = ((width) * 0.5f) + WIDTH_PADDING;

                // Compute the shadow width and alpha per altitude and draw it.
                var shadowWidth = Utilities.Lerp(1f, initialWidth, Utilities.Factor(MAX_SIZE_ALT, plane.Altitude));
                var shadowAlpha = shadowColor.Alpha.ToColorFloat() * (1f - Utilities.FactorWithEasing(plane.Altitude, MAX_SHOW_ALT, EasingFunctions.In.EaseSine));

                if (plane.Altitude <= 0f)
                    shadowWidth = initialWidth;

                if (shadowWidth <= 0f)
                    return;

                ctx.FillEllipse(shadowPos, new SKSize(shadowWidth, HEIGHT), shadowColor.WithAlpha(shadowAlpha));

            }
        }

        private void UpdateGroundColorBrush(GLRenderContext ctx)
        {
            _groundBrush?.Dispose();
            _groundBrush = new SKPaint() { Shader = SKShader.CreateLinearGradient(new SKPoint(0f, 50f), new SKPoint(0f, 4000f), [ctx.AddTimeOfDayColor(_groundColorDark), ctx.AddTimeOfDayColor(_groundColorLight)], SKShaderTileMode.Clamp) };
        }


        private void DrawMovingBackground(GLRenderContext ctx, GameObject viewObject)
        {
            float spacing = 75f;
            const float size = 4f;
            var d2dSz = new SKSize(size, size);

            // Fade out with zoom level.
            var alphaFact = 1f - Utilities.Factor(World.ViewPortScaleMulti, 35f);

            if (alphaFact < 0.05f)
                return;

            var color = SKColors.Gray.WithAlpha(0.3f * alphaFact);

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

        private void DrawOverlays(GLRenderContext ctx, GameObject viewObject)
        {
            DrawInfo(ctx, viewObject);
            //DrawDeathScreenOverlay(ctx, viewObject);
        }

        public void DrawInfo(GLRenderContext ctx, GameObject viewObject)
        {
            var infoText = GetInfo(viewObject);

            if (_showHelp)
            {
                _stringBuilder.AppendLine("H: Hide help\n");

                if (!World.IsNetGame)
                {
                    _stringBuilder.AppendLine($"Alt + Enter: Toggle Fullscreen");
                    _stringBuilder.AppendLine($"P: Pause");
                    _stringBuilder.AppendLine($"U: Spawn AI Plane");
                    _stringBuilder.AppendLine($"C: Remove AI Planes");
                }

                _stringBuilder.AppendLine($"Y: Start Chat Message");
                _stringBuilder.AppendLine($"Tab: Show Scores");
                _stringBuilder.AppendLine($"(+/-): Zoom");
                _stringBuilder.AppendLine($"Shift + (+/-): HUD Scale");
                _stringBuilder.AppendLine($"Left-Click: Fire Bullets");
                _stringBuilder.AppendLine($"Right-Click: Drop Decoys");
                _stringBuilder.AppendLine($"Middle-Click/Space Bar: Fire Missile");
                _stringBuilder.AppendLine($"L: Toggle Lead Indicators");
                _stringBuilder.AppendLine($"M: Toggle Missiles On Radar");
                _stringBuilder.AppendLine($"K: Toggle Missiles Regen");
                _stringBuilder.AppendLine($"F2: Toggle HUD");

                _stringBuilder.AppendLine($"\nSpectate (While crashed)");
                _stringBuilder.AppendLine($"([/]): Prev/Next Spectate Plane");
                _stringBuilder.AppendLine($"Backspace: Reset Spectate");
                _stringBuilder.AppendLine($"F: Toggle Free Camera Mode (Hold Right-Mouse to move)");
            }
            else
            {
                _stringBuilder.AppendLine("H: Show help");
            }

            ctx.DrawTextMultiLine(_stringBuilder.ToString(), new Vector2(20f, 20f), SKTextAlign.Left, _textConsolas12, _greenYellowColorBrush);
        }

        private string GetInfo(GameObject viewObject)
        {
            _stringBuilder.Clear();

            var numObj = _objs.TotalObjects;

            _stringBuilder.AppendLine($"FPS: {Math.Round(_renderFPSSmooth.Add(_renderFPS), 1)}");

            if (_showInfo)
            {
                _updateTimeSmooth.Add(Profiler.GetElapsedMilliseconds(ProfilerStat.Update));
                _collisionTimeSmooth.Add(Profiler.GetElapsedMilliseconds(ProfilerStat.Collisions));

                if (World.IsNetGame && _netMan != null)
                {
                    _stringBuilder.AppendLine($"Latency: {Math.Round(_netMan.Host.GetPlayerRTT(0), 2)}");
                    _stringBuilder.AppendLine($"Packet Delay: {Math.Round(_netMan.PacketDelay, 2)}");
                    _stringBuilder.AppendLine($"Packet Loss: {_netMan.Host.PacketLoss()}");
                    _stringBuilder.AppendLine($"Packets Deferred: {_netMan.NumDeferredPackets}");
                    _stringBuilder.AppendLine($"Packets Handled: {_netMan.NumHandledPackets}");
                    _stringBuilder.AppendLine($"Packets Expired: {_netMan.NumExpiredPackets}\n");
                }

                _stringBuilder.AppendLine($"Num Objects: {numObj}");
                _stringBuilder.AppendLine($"On Screen: {_numObjectsOnScreen}");
                _stringBuilder.AppendLine($"Off Screen: {GraphicsExtensions.OffScreen}");
                _stringBuilder.AppendLine($"Planes: {_objs.Planes.Count}");
                _stringBuilder.AppendLine($"Update ms: {Math.Round(_updateTimeSmooth.Current, 2)}");
                _stringBuilder.AppendLine($"Render ms: {Math.Round(_renderTimeSmooth.Current, 2)}");
                _stringBuilder.AppendLine($"Collision ms: {Math.Round(_collisionTimeSmooth.Current, 2)}");
                _stringBuilder.AppendLine($"Total ms: {Math.Round(_updateTimeSmooth.Current + _collisionTimeSmooth.Current + _renderTimeSmooth.Current, 2)}");

                _stringBuilder.AppendLine($"Zoom: {Math.Round(World.ZoomScale, 2)}");
                _stringBuilder.AppendLine($"DT: {Math.Round(World.TargetDT, 4)}  ({Math.Round(World.CurrentDT, 4)}) ");
                _stringBuilder.AppendLine($"Position: {viewObject?.Position}");

                if (viewObject is FighterPlane plane)
                {
                    _stringBuilder.AppendLine($"Kills: {plane.Kills}");
                    _stringBuilder.AppendLine($"Headshots: {plane.Headshots}");
                    _stringBuilder.AppendLine($"IsDisabled: {plane.IsDisabled}");
                    _stringBuilder.AppendLine($"HasCrashed: {plane.HasCrashed}");
                    _stringBuilder.AppendLine($"ThrustOn: {plane.ThrustOn}");
                }

                _stringBuilder.AppendLine($"GunsOnly: {World.GunsOnly.ToString()}");
                _stringBuilder.AppendLine($"MissilesOnRadar: {World.ShowMissilesOnRadar.ToString()}");
                _stringBuilder.AppendLine($"Missile Regen: {World.MissileRegen.ToString()}");
                _stringBuilder.AppendLine($"TimeOfDay: {Math.Round(World.TimeOfDay, 1)}");
                _stringBuilder.AppendLine($"TimeOffset: {Math.Round(TimeSpan.FromTicks((long)World.ServerTimeOffset).TotalMilliseconds, 2)}");
            }

            return _stringBuilder.ToString();
        }


        public void DoScreenShake()
        {
            float amt = 10f;
            _screenShakeX.StartValue = Utilities.Rnd.NextFloat(-amt, amt);
            _screenShakeY.StartValue = Utilities.Rnd.NextFloat(-amt, amt);

            _screenShakeX.Restart();
            _screenShakeY.Restart();
        }

        public void DoScreenShake(float amt)
        {
            if (_screenShakeX == null || _screenShakeY == null)
                return;

            _screenShakeX.StartValue = Utilities.Rnd.NextFloat(-amt, amt);
            _screenShakeY.StartValue = Utilities.Rnd.NextFloat(-amt, amt);

            _screenShakeX.Restart();
            _screenShakeY.Restart();
        }

        public void DoScreenFlash(D2DColor color)
        {
            _screenFlashColor = color.ToSKColor();
            _screenFlash?.Restart();
        }


        public void ToggleInfo()
        {
            _showInfo = !_showInfo;
        }

        public void ToggleHelp()
        {
            _showHelp = !_showHelp;
        }

        public void ToggleScore()
        {
            _showScore = !_showScore;
            _scoreScrollPos = 0;
        }

        public void ToggleHUD()
        {
            _showHUD = !_showHUD;
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
