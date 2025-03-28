﻿using PolyPlane.GameObjects;
using PolyPlane.GameObjects.Animations;
using PolyPlane.GameObjects.Tools;
using PolyPlane.Helpers;
using PolyPlane.Net;
using System.Diagnostics;
using System.Text;
using unvell.D2DLib;

namespace PolyPlane.Rendering
{
    public class Renderer : IDisposable
    {
        public TimeSpan CollisionTime = TimeSpan.Zero;
        public TimeSpan UpdateTime = TimeSpan.Zero;

        public float HudScale
        {
            get { return _hudScale; }

            set
            {
                if (value > 0f && value < 10f)
                    _hudScale = value;
            }
        }

        public NetEventManager NetManager
        {
            get { return _netMan; }
            set { _netMan = value; }
        }

        private StringBuilder _stringBuilder = new StringBuilder();

        private D2DDevice _device;
        private D2DGraphics _gfx = null;
        private RenderContext _ctx;
        private D2DLayer _groundClipLayer = null;

        private D2DRadialGradientBrush _bulletLightingBrush = null;
        private D2DRadialGradientBrush _missileLightingBrush = null;
        private D2DRadialGradientBrush _muzzleFlashBrush = null;
        private D2DRadialGradientBrush _decoyLightBrush = null;
        private D2DLinearGradientBrush _groundBrush = null;

        private D2DTextFormat _textConsolas12;
        private D2DTextFormat _textConsolas15Centered;
        private D2DTextFormat _textConsolas15;
        private D2DTextFormat _textConsolas25Centered;
        private D2DTextFormat _textConsolas30Centered;
        private D2DTextFormat _textConsolas30;
        private D2DTextFormat _messageBoxFont;

        private D2DSolidColorBrush _hudColorBrush;
        private D2DSolidColorBrush _hudColorBrushLight;
        private D2DSolidColorBrush _redColorBrush;
        private D2DSolidColorBrush _whiteColorBrush;
        private D2DSolidColorBrush _greenYellowColorBrush;
        private D2DBitmap? _clearBitmap = null;

        private bool _showInfo = false;
        private bool _showHelp = false;
        private bool _showScore = false;
        private bool _showHUD = true;
        private int _scoreScrollPos = 0;
        private int _currentDPI = 96;
        private double _lastRenderTime = 0;
        private float _hudScale = 1f;
        private double _renderFPS = 0;
        private float _screenFlashOpacity = 0f;
        private float _warnLightFlashAmount = 1f;
        private float _groundColorTOD = 0f;
        private string _hudMessage = string.Empty;

        private SmoothDouble _renderTimeSmooth = new SmoothDouble(60);
        private SmoothDouble _updateTimeSmooth = new SmoothDouble(60);
        private SmoothDouble _fpsSmooth = new SmoothDouble(20);

        private Stopwatch _timer = new Stopwatch();
        private GameTimer _hudMessageTimeout = new GameTimer(10f);
        private List<EventMessage> _messageEvents = new List<EventMessage>();
        private List<PopMessage> _popMessages = new List<PopMessage>();

        private Control _renderTarget;
        private GameObjectManager _objs = World.ObjectManager;
        private NetEventManager _netMan;
        private ContrailBox _contrailBox = new ContrailBox();
        private FPSLimiter _fpsLimiter = new FPSLimiter();

        private D2DPoint _screenShakeTrans = D2DPoint.Zero;
        private D2DColor _hudMessageColor = D2DColor.Red;
        private D2DColor _screenFlashColor = D2DColor.Red;

        private readonly D2DColor _clearColor = D2DColor.Transparent;
        private readonly D2DColor _groundImpactOuterColor = new D2DColor(1f, 0.56f, 0.32f, 0.18f);
        private readonly D2DColor _groundImpactInnerColor = new D2DColor(1f, 0.35f, 0.2f, 0.1f);
        private readonly D2DColor _skyColorLight = new D2DColor(0.5f, D2DColor.SkyBlue);
        private readonly D2DColor _skyColorDark = new D2DColor(0.5f, D2DColor.Black);
        private readonly D2DColor _groundColorLight = new D2DColor(1f, 0f, 0.29f, 0);
        private readonly D2DColor _groundColorDark = D2DColor.DarkGreen;
        private readonly D2DColor _groundLightColor = new D2DColor(0.85f, 0.76f, 0.14f);
        private readonly D2DColor _deathScreenColor = new D2DColor(0.2f, D2DColor.Red);
        private readonly D2DColor _hudColorLight = Utilities.LerpColor(World.HudColor, D2DColor.WhiteSmoke, 0.3f);

        private readonly D2DPoint _infoPosition = new D2DPoint(20, 20);
        private readonly D2DSize _healthBarSize = new D2DSize(80, 20);

        private FloatAnimation _screenShakeX;
        private FloatAnimation _screenShakeY;
        private FloatAnimation _screenFlash;
        private FloatAnimation _warnLightFlash;

        private int Width => (int)(_renderTarget.Width / (_renderTarget.DeviceDpi / DEFAULT_DPI));
        private int Height => (int)(_renderTarget.Height / (_renderTarget.DeviceDpi / DEFAULT_DPI));

        private const float VIEW_SCALE = 4f;
        private const float DEFAULT_DPI = 96f;

        private const int NUM_CLOUDS = 2000;
        private const int NUM_TREES = 1000;

        private const float GROUND_TOD_INTERVAL = 0.1f; // Update ground brush when elapsed TOD exceeds this amount.
        private const float ZOOM_FACTOR = 0.07f; // Effects zoom in/out speed.
        private const float MESSAGEBOX_FONT_SIZE = 10f;
        private const string DEFAULT_FONT_NAME = "Consolas";

        private List<Tree> _trees = new List<Tree>();
        private CloudManager _cloudManager = new();

        public Renderer(Control renderTarget, NetEventManager netMan)
        {
            _renderTarget = renderTarget;
            _netMan = netMan;

            if (_netMan != null)
                _netMan.NewChatMessage += NetMan_NewChatMessage;

            if (!World.IsNetGame)
            {
                _objs.PlayerKilledEvent += PlayerKilledEvent;
                _objs.NewPlayerEvent += NewPlayerEvent;
                _objs.PlayerScoredEvent += PlayerScoredEvent;
            }
            else
            {
                netMan.PlayerScoredEvent += PlayerScoredEvent;
            }


            InitProceduralGenStuff();
            InitRenderTarget();

            _groundColorTOD = World.TimeOfDay;
        }

        public void Dispose()
        {
            _hudMessageTimeout.Stop();

            _groundClipLayer?.Dispose();
            _bulletLightingBrush?.Dispose();
            _missileLightingBrush?.Dispose();
            _muzzleFlashBrush?.Dispose();
            _decoyLightBrush?.Dispose();
            _groundBrush?.Dispose();

            _textConsolas12?.Dispose();
            _textConsolas15Centered?.Dispose();
            _textConsolas15?.Dispose();
            _textConsolas25Centered?.Dispose();
            _textConsolas30Centered?.Dispose();
            _textConsolas30?.Dispose();
            _messageBoxFont?.Dispose();

            _hudColorBrush?.Dispose();
            _hudColorBrushLight?.Dispose();
            _redColorBrush?.Dispose();
            _whiteColorBrush?.Dispose();
            _greenYellowColorBrush?.Dispose();

            _clearBitmap?.Dispose();
            _trees.ForEach(t => t.Dispose());

            _device?.Dispose();
            _fpsLimiter?.Dispose();
        }


        private void InitRenderTarget()
        {
            _device?.Dispose();
            _device = D2DDevice.FromHwnd(_renderTarget.Handle);
            _device.Resize();
        }

        public void InitGfx()
        {
            if (_gfx != null)
                return;

            _gfx = new D2DGraphics(_device);
            _gfx.Antialias = true;
            _device.Resize();
            _ctx = new RenderContext(_gfx, _device);

            _currentDPI = _renderTarget.DeviceDpi;

            var scaleSize = GetViewportScaled();
            World.UpdateViewport(scaleSize);

            _screenFlash = new FloatAnimation(0.4f, 0f, 4f, EasingFunctions.Out.EaseCircle, v => _screenFlashOpacity = v);
            _screenShakeX = new FloatAnimation(5f, 0f, 2f, EasingFunctions.Out.EaseCircle, v => _screenShakeTrans.X = v);
            _screenShakeY = new FloatAnimation(5f, 0f, 2f, EasingFunctions.Out.EaseCircle, v => _screenShakeTrans.Y = v);

            _warnLightFlash = new FloatAnimation(0f, 1f, 0.3f, EasingFunctions.EaseLinear, v => _warnLightFlashAmount = v);
            _warnLightFlash.Loop = true;
            _warnLightFlash.ReverseOnLoop = true;
            _warnLightFlash.Start();

            _textConsolas12 = _ctx.Device.CreateTextFormat(DEFAULT_FONT_NAME, 12f);
            _textConsolas15Centered = _ctx.Device.CreateTextFormat(DEFAULT_FONT_NAME, 15f, D2DFontWeight.Normal, D2DFontStyle.Normal, D2DFontStretch.Normal, DWriteTextAlignment.Center, DWriteParagraphAlignment.Center);
            _textConsolas15 = _ctx.Device.CreateTextFormat(DEFAULT_FONT_NAME, 15f);
            _textConsolas25Centered = _ctx.Device.CreateTextFormat(DEFAULT_FONT_NAME, 25f, D2DFontWeight.Normal, D2DFontStyle.Normal, D2DFontStretch.Normal, DWriteTextAlignment.Center, DWriteParagraphAlignment.Center);
            _textConsolas30Centered = _ctx.Device.CreateTextFormat(DEFAULT_FONT_NAME, 30f, D2DFontWeight.Normal, D2DFontStyle.Normal, D2DFontStretch.Normal, DWriteTextAlignment.Center, DWriteParagraphAlignment.Center);
            _textConsolas30 = _ctx.Device.CreateTextFormat(DEFAULT_FONT_NAME, 30f);
            _messageBoxFont = _ctx.Device.CreateTextFormat(DEFAULT_FONT_NAME, MESSAGEBOX_FONT_SIZE);

            _hudColorBrush = _ctx.Device.CreateSolidColorBrush(World.HudColor);
            _hudColorBrushLight = _ctx.Device.CreateSolidColorBrush(_hudColorLight);
            _redColorBrush = _ctx.Device.CreateSolidColorBrush(D2DColor.Red);
            _whiteColorBrush = _ctx.Device.CreateSolidColorBrush(D2DColor.White);
            _greenYellowColorBrush = _ctx.Device.CreateSolidColorBrush(D2DColor.GreenYellow);

            UpdateGroundColorBrush(_ctx);
            InitClearGradientBitmap();
        }

        private void InitClearGradientBitmap()
        {
            var startColor = D2DColor.Black.WithAlpha(1f);
            var endColor = D2DColor.Black.WithAlpha(0.5f);

            using (var gradGfx = _device.CreateBitmapGraphics(this.Width, this.Height))
            using (var gradBrush = _device.CreateLinearGradientBrush(D2DPoint.Zero, new D2DPoint(0f, this.Height), [new D2DGradientStop(0f, startColor), new D2DGradientStop(1f, endColor)]))
            {
                gradGfx.BeginRender();

                gradGfx.FillRectangle(new D2DRect(0f, 0f, this.Width, this.Height), gradBrush);

                gradGfx.EndRender();

                _clearBitmap?.Dispose();
                _clearBitmap = gradGfx.GetBitmap();
            }
        }

        private void InitProceduralGenStuff()
        {
            var rnd = new Random(1234);

            _cloudManager.GenClouds(rnd, NUM_CLOUDS);
            GenTrees(rnd);
        }

        private void GenTrees(Random rnd)
        {
            // Gen trees.
            var treeDeDup = new HashSet<D2DPoint>();

            var trunkColorNormal = D2DColor.Chocolate;
            var trunkColorNormalDark = new D2DColor(1f, 0.29f, 0.18f, 0.105f);
            var leafColorNormal = D2DColor.ForestGreen;
            var trunkColorPine = D2DColor.BurlyWood;
            var leafColorPine = D2DColor.Green;
            var minDist = rnd.NextFloat(20f, 200f);
            var fieldRange = World.FieldXBounds;

            for (int i = 0; i < NUM_TREES; i++)
            {
                var rndPos = new D2DPoint(rnd.NextFloat(fieldRange.X, fieldRange.Y), 0f);

                while (!treeDeDup.Add(rndPos) || (_trees.Count > 0 && _trees.Min(t => t.Position.DistanceTo(rndPos)) < minDist))
                    rndPos = new D2DPoint(rnd.NextFloat(fieldRange.X, fieldRange.Y), 0f);

                var type = rnd.Next(10);
                var height = 10f + (rnd.NextFloat(1f, 3f) * 20f);

                Tree newTree;

                if (type <= 8)
                {
                    var radius = rnd.NextFloat(40f, 80f);

                    var leafColor = leafColorNormal;
                    leafColor.g -= rnd.NextFloat(0.0f, 0.2f);

                    var trunkColor = Utilities.LerpColor(trunkColorNormal, trunkColorNormalDark, rnd.NextFloat(0f, 1f));
                    var trunkWidth = rnd.NextFloat(3f, 7f);

                    newTree = new NormalTree(rndPos, height, radius, trunkWidth, trunkColor, leafColor);
                }
                else
                {
                    var width = rnd.NextFloat(20f, 30f);
                    newTree = new PineTree(rndPos, height, width, trunkColorPine, leafColorPine);
                }

                _trees.Add(newTree);

                if (i % 50 == 0)
                    minDist = rnd.NextFloat(20f, 200f);
            }
        }

        private void ResizeGfx(bool force = false)
        {
            if (!force)
                if (World.ViewPortBaseSize.width == this.Width && World.ViewPortBaseSize.height == this.Height)
                    return;

            _device?.Resize();

            ResizeViewPort();

            InitClearGradientBitmap();

            // Resizing graphics causes spikes in FPS. Try to limit them here.
            _fpsLimiter.Wait(World.TARGET_FPS);
        }

        private void ResizeViewPort()
        {
            var scaleSize = GetViewportScaled();
            World.UpdateViewport(scaleSize);
        }

        private Size GetViewportScaled()
        {
            var scaleSize = new Size((int)((float)_renderTarget.Size.Width / ((float)_currentDPI / World.DEFAULT_DPI)), (int)((float)_renderTarget.Size.Height / ((float)_currentDPI / World.DEFAULT_DPI)));
            return scaleSize;
        }

        private void UpdateTimersAndAnims(float dt)
        {
            _hudMessageTimeout.Update(dt);

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

                UpdatePopMessages(dt);
            }
        }

        public void RenderFrame(GameObject viewObject, float dt)
        {
            if (_gfx == null)
                throw new InvalidOperationException($"Renderer not initialized. Please call {nameof(InitGfx)} first.");

            ResizeGfx();

            _timer.Restart();

            UpdateTimersAndAnims(dt);

            _timer.Stop();
            UpdateTime += _timer.Elapsed;
            _timer.Restart();

            if (World.UseSkyGradient)
                _ctx.BeginRender(_clearBitmap);
            else
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

                // Draw sky background color.
                DrawSky(_ctx, viewObject);

                // Push screen shake transform.
                _ctx.PushTransform();
                _ctx.TranslateTransform(_screenShakeTrans);

                // Draw parallax grid. 
                DrawMovingBackground(_ctx, viewObject);

                // Push zoom scale transform.
                _ctx.PushTransform();
                _ctx.ScaleTransform(World.ZoomScale);

                // Draw the main player view.  (Draws all game objects, clouds, ground, lighting effects, etc)
                DrawPlayerView(_ctx, viewObject);

                // Pop scale transform.
                _ctx.PopTransform();

                // Draw HUD.
                DrawHud(_ctx, viewObject, dt);

                // Pop screen shake transform.
                _ctx.PopTransform();

                // Chat and event box.
                DrawChatAndEventBox(_ctx);

                // Draw overlay text. (FPS, Help and Info)
                DrawOverlays(_ctx, viewObject);

                // And finally screen flash.
                DrawScreenFlash(_gfx);

                _ctx.PopViewPort();
            }

            _timer.Stop();
            _renderTimeSmooth.Add(_timer.Elapsed.TotalMilliseconds);

            _ctx.EndRender();

            var now = World.CurrentTimeMs();
            var elap = now - _lastRenderTime;
            _lastRenderTime = now;
            var fps = 1000d / elap;
            _renderFPS = fps;
        }

        private void DrawSky(RenderContext ctx, GameObject viewObject)
        {
            const float MAX_ALT_OFFSET = 50000f;

            var plrAlt = viewObject.Altitude;
            if (viewObject.Position.Y >= 0)
                plrAlt = 0f;

            var color1 = _skyColorLight;
            var color2 = _skyColorDark;
            var color = Utilities.LerpColor(color1, color2, (plrAlt / (World.MAX_ALTITUDE - MAX_ALT_OFFSET)));

            // Add time of day color.
            color = Utilities.LerpColor(color, D2DColor.Black, Utilities.Factor(World.TimeOfDay, World.MAX_TIMEOFDAY - 5f));
            color = Utilities.LerpColor(color, ctx.AddTimeOfDayColor(color), 0.2f);

            ctx.Gfx.FillRectangle(World.ViewPortRectUnscaled, color);
        }

        private void DrawMovingBackground(RenderContext ctx, GameObject viewObject)
        {
            float spacing = 75f;
            const float size = 4f;
            var d2dSz = new D2DSize(size, size);

            // Fade out with zoom level.
            var alphaFact = 1f - Utilities.Factor(World.ViewPortScaleMulti, 35f);
            var color = new D2DColor(0.3f * alphaFact, D2DColor.Gray);

            if (alphaFact < 0.05f)
                return;

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
                        ctx.Gfx.FillRectangle(new D2DRect(pos, d2dSz), color);
                }
            }
        }

        private void DrawPlayerView(RenderContext ctx, GameObject viewObj)
        {
            FighterPlane? viewPlane = null;

            if (viewObj is FighterPlane plane)
                viewPlane = plane;

            ctx.PushTransform();

            var zAmt = World.ZoomScale;
            var center = new D2DPoint(World.ViewPortBaseSize.width * 0.5f, World.ViewPortBaseSize.height * 0.5f);
            var viewObjOffset = new D2DPoint(-viewObj.Position.X, -viewObj.Position.Y) * zAmt;

            ctx.ScaleTransform(VIEW_SCALE, viewObj.Position);
            ctx.TranslateTransform(viewObjOffset);
            ctx.TranslateTransform(center);

            var viewPortRect = new D2DRect(viewObj.Position, new D2DSize((World.ViewPortSize.width / VIEW_SCALE), World.ViewPortSize.height / VIEW_SCALE));

            const float VIEWPORT_PADDING_AMT = 1.5f;
            var inflateAmt = VIEWPORT_PADDING_AMT * zAmt;
            viewPortRect = viewPortRect.Inflate(viewPortRect.Width * inflateAmt, viewPortRect.Height * inflateAmt, keepAspectRatio: true); // Inflate slightly to prevent "pop-in".

            // Query the spatial grid for objects within the current viewport.
            var objsInViewport = _objs.GetInViewport(viewPortRect).Where(o => o is not Explosion);

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

            // Order the enumerator by render order after populating the light map.
            // This is done to avoid the internal sorting/mapping while populating the light map.
            objsInViewport = objsInViewport.OrderBy(o => o.RenderOrder);

            var shadowColor = ctx.GetShadowColor();
            var todAngle = ctx.GetTimeOfDaySunAngle();

            ctx.PushViewPort(viewPortRect);

            DrawGround(ctx, viewObj.Position);
            DrawGroundImpacts(ctx);
            DrawTrees(ctx);
            DrawPlaneGroundShadows(ctx, shadowColor, todAngle);

            for (int i = 0; i < _objs.MissileTrails.Count; i++)
                _objs.MissileTrails[i].Render(ctx);

            _contrailBox.Render(ctx);

            foreach (var obj in objsInViewport)
            {
                if (obj is FighterPlane p)
                {
                    p.Render(ctx);
                    DrawMuzzleFlash(ctx, p);

                    if (viewPlane != null)
                    {
                        // Draw health bars for other planes.
                        if (!p.Equals(viewPlane))
                            DrawPlaneHealthBar(ctx, p, new D2DPoint(p.Position.X, p.Position.Y - 110f));

                        // Draw circle around locked on plane.
                        if (viewPlane.Radar.LockedObj != null && viewPlane.Radar.LockedObj.Equals(p))
                            ctx.DrawEllipse(new D2DEllipse(p.Position, new D2DSize(80f, 80f)), World.HudColor, 4f);
                    }
                }
                else if (obj is GuidedMissile missile)
                {
                    missile.Render(ctx);

                    // Circle enemy missiles.
                    if (!World.FreeCameraMode && !viewObj.Equals(missile) && !missile.Owner.Equals(viewPlane))
                        ctx.DrawEllipse(new D2DEllipse(missile.Position, new D2DSize(50f, 50f)), new D2DColor(0.4f, D2DColor.Red), 8f);
                }
                else
                {
                    obj.Render(ctx);
                }
            }

            // Render explosions separate so that they can clip to the viewport correctly.
            for (int i = 0; i < _objs.Explosions.Count; i++)
                _objs.Explosions[i].Render(ctx);

            DrawClouds(ctx);
            DrawPlaneCloudShadows(ctx, shadowColor, objsInViewport);
            DrawLightFlareEffects(ctx, objsInViewport);

            if (World.DrawNoiseMap)
                DrawNoise(ctx);

            if (World.DrawLightMap)
                DrawLightMap(ctx);

            ctx.PopViewPort();
            ctx.PopTransform();
        }

        private void DrawGround(RenderContext ctx, D2DPoint position)
        {
            var groundPos = new D2DPoint(position.X, 0f);

            if (!ctx.Viewport.Contains(groundPos))
                return;

            const float HEIGHT = 500f;
            var yPos = HEIGHT / ctx.CurrentScale;
            groundPos += new D2DPoint(0f, yPos);

            // Draw the ground.
            var rect = new D2DRect(groundPos, new D2DSize(World.ViewPortSize.width, (HEIGHT * 2f) / ctx.CurrentScale));
            ctx.Gfx.FillRectangle(rect, _groundBrush);
        }

        private void DrawTrees(RenderContext ctx)
        {
            var todColor = ctx.GetTimeOfDayColor();
            var shadowColor = ctx.GetShadowColor();
            var shadowAngle = Tree.GetTreeShadowAngle();

            for (int i = 0; i < _trees.Count; i++)
            {
                var tree = _trees[i];

                if (ctx.Viewport.Contains(tree.Position, tree.TotalHeight * Tree.TREE_SCALE))
                {
                    tree.Render(ctx, todColor, shadowColor, shadowAngle);
                }
            } 
        }

        private void DrawGroundImpacts(RenderContext ctx)
        {
            if (_groundClipLayer == null)
                _groundClipLayer = ctx.Device.CreateLayer();

            const float LIGHT_INTENSITY = 0.4f;

            var rect = new D2DRect(ctx.Viewport.Location.X, 0f, ctx.Viewport.Width, 4000f);

            using (var clipGeo = ctx.Device.CreateRectangleGeometry(rect))
            {
                ctx.Gfx.PushLayer(_groundClipLayer, ctx.Viewport, clipGeo);

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

                        ctx.FillEllipseWithLighting(new D2DEllipse(impact.Position, new D2DSize(impact.Size.width + 4f, impact.Size.height + 4f)), _groundImpactOuterColor.WithAlpha(ageAlpha), LIGHT_INTENSITY);
                        ctx.FillEllipseWithLighting(new D2DEllipse(impact.Position, new D2DSize(impact.Size.width, impact.Size.height)), _groundImpactInnerColor.WithAlpha(ageAlpha), LIGHT_INTENSITY);

                        ctx.PopTransform();
                    }
                }

                ctx.Gfx.PopLayer();
            }
        }

        private void DrawMuzzleFlash(RenderContext ctx, FighterPlane plane)
        {
            if (!ctx.Viewport.Contains(plane.Gun.Position))
                return;

            const float MUZZ_FLASH_RADIUS = 60f;
            if (_muzzleFlashBrush == null)
                _muzzleFlashBrush = ctx.Device.CreateRadialGradientBrush(D2DPoint.Zero, D2DPoint.Zero, MUZZ_FLASH_RADIUS, MUZZ_FLASH_RADIUS, new D2DGradientStop[] { new D2DGradientStop(1f, D2DColor.Transparent), new D2DGradientStop(0f, new D2DColor(0.2f, D2DColor.Orange)) });

            if (plane.Gun.MuzzleFlashOn)
            {
                ctx.PushTransform();
                ctx.TranslateTransform(plane.GunPosition * ctx.CurrentScale);
                ctx.Gfx.FillEllipseSimple(D2DPoint.Zero, MUZZ_FLASH_RADIUS, _muzzleFlashBrush);
                ctx.PopTransform();
            }
        }

        private void DrawPlaneHealthBar(RenderContext ctx, FighterPlane plane, D2DPoint position)
        {
            if (!ctx.Viewport.Contains(position))
                return;

            var size = _healthBarSize;
            var healthPct = plane.Health / FighterPlane.MAX_HEALTH;

            if (healthPct > 0f && healthPct < 0.05f)
                healthPct = 0.05f;

            ctx.DrawProgressBar(position, size, World.HudColor, World.HudColor, healthPct);

            // Draw player name.
            if (string.IsNullOrEmpty(plane.PlayerName))
                return;

            var rect = new D2DRect(position + new D2DPoint(0, -40), new D2DSize(300, 100));
            ctx.DrawText(plane.PlayerName, _hudColorBrush, _textConsolas30Centered, rect);
        }

        private void DrawClouds(RenderContext ctx)
        {
            _cloudManager.Render(ctx);
        }

        private void DrawPlaneCloudShadows(RenderContext ctx, D2DColor shadowColor, IEnumerable<GameObject> objs)
        {
            // Don't bother if we are currently zoomed way out.
            if (World.ZoomScale < 0.03f)
                return;

            var color = shadowColor.WithAlpha(0.07f);

            foreach (var obj in objs)
            {
                if (obj is FighterPlane plane)
                    ctx.FillPolygon(plane.Polygon, color);
            }
        }

        private void DrawPlaneGroundShadows(RenderContext ctx, D2DColor shadowColor, float todAngle)
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
                var shadowAlpha = shadowColor.a * (1f - Utilities.FactorWithEasing(plane.Altitude, MAX_SHOW_ALT, EasingFunctions.In.EaseSine));

                if (plane.Altitude <= 0f)
                    shadowWidth = initialWidth;

                if (shadowWidth <= 0f)
                    return;

                ctx.FillEllipse(new D2DEllipse(shadowPos, new D2DSize(shadowWidth, HEIGHT)), shadowColor.WithAlpha(shadowAlpha));
            }
        }

        private void DrawLightFlareEffects(RenderContext ctx, IEnumerable<GameObject> objs)
        {
            const float BULLET_LIGHT_RADIUS = 60f;
            if (_bulletLightingBrush == null)
                _bulletLightingBrush = ctx.Device.CreateRadialGradientBrush(D2DPoint.Zero, D2DPoint.Zero, BULLET_LIGHT_RADIUS, BULLET_LIGHT_RADIUS, new D2DGradientStop[] { new D2DGradientStop(1.4f, D2DColor.Transparent), new D2DGradientStop(0f, new D2DColor(0.2f, D2DColor.Yellow)) });

            const float MISSILE_LIGHT_RADIUS = 70f;
            if (_missileLightingBrush == null)
                _missileLightingBrush = ctx.Device.CreateRadialGradientBrush(D2DPoint.Zero, D2DPoint.Zero, MISSILE_LIGHT_RADIUS, MISSILE_LIGHT_RADIUS, new D2DGradientStop[] { new D2DGradientStop(1.4f, D2DColor.Transparent), new D2DGradientStop(0f, new D2DColor(0.2f, D2DColor.Yellow)) });

            const float DECOY_LIGHT_RADIUS = 90f;
            if (_decoyLightBrush == null)
                _decoyLightBrush = ctx.Device.CreateRadialGradientBrush(D2DPoint.Zero, D2DPoint.Zero, DECOY_LIGHT_RADIUS, DECOY_LIGHT_RADIUS, new D2DGradientStop[] { new D2DGradientStop(1.4f, D2DColor.Transparent), new D2DGradientStop(0f, new D2DColor(0.3f, D2DColor.LightYellow)) });

            var scale = ctx.CurrentScale;

            foreach (var obj in objs)
            {
                if (obj is Bullet bullet)
                {
                    ctx.PushTransform();
                    ctx.TranslateTransform(bullet.Position * scale);
                    ctx.Gfx.FillEllipseSimple(D2DPoint.Zero, BULLET_LIGHT_RADIUS, _bulletLightingBrush);
                    ctx.PopTransform();

                    DrawObjectGroundLight(ctx, bullet);
                }
                else if (obj is GuidedMissile missile)
                {
                    if (missile.FlameOn && missile.CurrentFuel > 0f)
                    {
                        ctx.PushTransform();
                        ctx.TranslateTransform(missile.CenterOfThrust * scale);

                        // Add a little flicker effect to missile lights.
                        var flickerScale = Utilities.Rnd.NextFloat(0.7f, 1f);
                        ctx.ScaleTransform(flickerScale);

                        ctx.Gfx.FillEllipseSimple(D2DPoint.Zero, MISSILE_LIGHT_RADIUS, _missileLightingBrush);
                        ctx.PopTransform();

                        DrawObjectGroundLight(ctx, missile);
                    }
                }
                else if (obj is Decoy decoy)
                {
                    if ((decoy.IsFlashing()))
                    {
                        ctx.PushTransform();
                        ctx.TranslateTransform(decoy.Position * scale);
                        ctx.Gfx.FillEllipseSimple(D2DPoint.Zero, DECOY_LIGHT_RADIUS, _decoyLightBrush);
                        ctx.PopTransform();

                        DrawObjectGroundLight(ctx, decoy);
                    }
                }
            }
        }

        private void DrawObjectGroundLight(RenderContext ctx, GameObject obj)
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

            ctx.FillEllipse(new D2DEllipse(lightPos, new D2DSize(lightWidth, HEIGHT)), _groundLightColor.WithAlpha(lightAlpha));
        }


        private void DrawNoise(RenderContext ctx)
        {
            const float step = 25f;
            const float size = 10f;

            for (float x = ctx.Viewport.left; x <= ctx.Viewport.right; x += step)
            {
                for (float y = ctx.Viewport.top; y <= ctx.Viewport.bottom; y += step)
                {
                    var nPos = new D2DPoint(x, y);
                    var noise = World.SampleNoise(nPos);

                    ctx.FillRectangle(new D2DRect(nPos, new D2DSize(size, size)), D2DColor.Black.WithAlpha(noise));
                }
            }
        }

        private void DrawLightMap(RenderContext ctx)
        {
            float step = ctx.LightMap.SIDE_LEN;

            for (float x = ctx.Viewport.left; x <= ctx.Viewport.right; x += step)
            {
                for (float y = ctx.Viewport.top; y <= ctx.Viewport.bottom; y += step)
                {
                    var nPos = new D2DPoint(x, y);
                    var color = ctx.LightMap.SampleMap(nPos);
                    ctx.FillRectangle(new D2DRect(nPos, new D2DSize(step, step)), color.ToD2DColor());
                }
            }
        }

        private void DrawHud(RenderContext ctx, GameObject viewObject, float dt)
        {
            var viewportsize = World.ViewPortRectUnscaled.Size;

            ctx.PushTransform();
            ctx.ScaleTransform(_hudScale, new D2DPoint(viewportsize.width * 0.5f, viewportsize.height * 0.5f));

            if (_showHUD)
            {
                var speedoPos = new D2DPoint(viewportsize.width * 0.15f, viewportsize.height * 0.33f);
                var altimeterPos = new D2DPoint(viewportsize.width * 0.85f, viewportsize.height * 0.33f);

                // Draw altimeter and speedo.
                DrawTapeIndicator(ctx, viewportsize, altimeterPos, viewObject.Altitude, 3000f, 175f);
                DrawTapeIndicator(ctx, viewportsize, speedoPos, viewObject.AirSpeedIndicated, 250f, 50f);

                if (viewObject is FighterPlane plane)
                {
                    // Draw g-force.
                    var gforceRect = new D2DRect(new D2DPoint(speedoPos.X, speedoPos.Y - 195f), new D2DSize(60f, 20f));
                    ctx.Gfx.DrawText($"G {Math.Round(plane.GForce, 1)}", _hudColorBrush, _textConsolas15, gforceRect);

                    if (!plane.IsDisabled)
                    {
                        if (plane.IsAI == false)
                        {
                            DrawGuideIcon(ctx.Gfx, viewportsize, plane);
                            DrawGroundWarning(ctx, viewportsize, plane);
                        }

                        DrawPlanePointers(ctx, viewportsize, plane, dt);
                        DrawMissilePointersAndWarnings(ctx, viewportsize, plane);
                    }

                    DrawHudMessage(ctx.Gfx, viewportsize);
                    DrawRadar(ctx, viewportsize, plane);

                    var healthBarSize = new D2DSize(300, 30);
                    var pos = new D2DPoint(viewportsize.width * 0.5f, viewportsize.height - (viewportsize.height * 0.85f));
                    DrawHealthBarAndAmmo(ctx, plane, pos, healthBarSize);

                    DrawPopMessages(ctx, viewportsize, plane);
                }
            }

            if (_showScore)
                DrawScoreCard(ctx, viewportsize);

            ctx.PopTransform();

            if (World.FreeCameraMode)
                DrawFreeCamPrompt(ctx);
        }

        /// <summary>
        /// Draws a tape style indicator for the specified value.
        /// </summary>
        /// <param name="gfx">Graphics context.</param>
        /// <param name="viewportsize">Viewport size.</param>
        /// <param name="pos">Position within the viewport.</param>
        /// <param name="value">Current value.</param>
        /// <param name="minValue">Minimum value. The background changes to red below this value.</param>
        /// <param name="markerRange">Step size for markers.</param>
        private void DrawTapeIndicator(RenderContext ctx, D2DSize viewportsize, D2DPoint pos, float value, float minValue, float markerRange)
        {
            const float W = 80f;
            const float H = 350f;
            const float HalfW = W * 0.5f;
            const float HalfH = H * 0.5f;

            var rect = new D2DRect(pos, new D2DSize(W, H));
            var startValue = (value) - (value % (markerRange)) + markerRange;
            var valueWarningColor = new D2DColor(0.2f, D2DColor.Red);

            var highestVal = startValue + markerRange;
            var lowestVal = (startValue - HalfH) - markerRange;

            ctx.Gfx.DrawRectangle(rect, World.HudColor);
            ctx.Gfx.DrawLine(new D2DPoint(pos.X - HalfW, pos.Y), new D2DPoint(pos.X + HalfW, pos.Y), World.HudColor.WithAlpha(1f), 1f, D2DDashStyle.Solid);

            if (highestVal <= minValue || lowestVal <= minValue)
            {
                var s = new D2DPoint(pos.X - HalfW, (pos.Y + (value - minValue)));

                if (s.Y < pos.Y - HalfH)
                    s.Y = pos.Y - HalfH;

                var sRect = new D2DRect(s.X, s.Y, W, (pos.Y + (H * 0.5f)) - s.Y);

                if (sRect.Height > 0f)
                    ctx.Gfx.FillRectangle(sRect, valueWarningColor);
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
                    ctx.Gfx.DrawLine(start, end, World.HudColor, 1f, D2DDashStyle.Dash);

                if (markerValue >= 0f)
                {
                    // Fade in marker text as they move towards the center.
                    var alpha = Math.Clamp(1f - Utilities.FactorWithEasing(Math.Abs(pos.Y - posY), HalfH, EasingFunctions.Out.EaseSine), 0.02f, 0.4f);
                    var textRect = new D2DRect(start - new D2DPoint(25f, 0f), new D2DSize(60f, 30f));
                    ctx.DrawText(markerValue.ToString(), World.HudColor.WithAlpha(alpha), _textConsolas15Centered, textRect);
                }
            }

            var curValueRect = new D2DRect(new D2DPoint(pos.X, pos.Y + HalfH + 20f), new D2DSize(60f, 20f));
            ctx.DrawText(Math.Round(value, 0).ToString(), _hudColorBrush, _textConsolas15Centered, curValueRect);
        }

        private void DrawGuideIcon(D2DGraphics gfx, D2DSize viewportsize, FighterPlane viewPlane)
        {
            const float DIST = 300f;
            var pos = new D2DPoint(viewportsize.width * 0.5f, viewportsize.height * 0.5f);

            var mouseAngle = viewPlane.PlayerGuideAngle;
            var mouseVec = Utilities.AngleToVectorDegrees(mouseAngle, DIST);
            gfx.DrawEllipse(new D2DEllipse(pos + mouseVec, new D2DSize(5f, 5f)), World.HudColor, 2f);

            var planeAngle = viewPlane.Rotation;
            var planeVec = Utilities.AngleToVectorDegrees(planeAngle, DIST);
            gfx.DrawCrosshair(pos + planeVec, 2f, World.HudColor, 5f, 20f);

            //gfx.DrawLine(pos, pos + planeVec, World.HudColor, 1f, D2DDashStyle.Dot);
        }

        private void DrawGroundWarning(RenderContext ctx, D2DSize viewportsize, FighterPlane viewPlane)
        {
            const float WARN_TIME = 5f;
            var pos = new D2DPoint(viewportsize.width / 2f, viewportsize.height / 2f - 100f);
            var rect = new D2DRect(pos, new D2DSize(150, 100));
            var impactTime = Utilities.GroundImpactTime(viewPlane);

            if (impactTime > 0f && impactTime < WARN_TIME)
                ctx.Gfx.DrawText("PULL UP!", _redColorBrush, _textConsolas30Centered, rect);
        }

        private void DrawPlanePointers(RenderContext ctx, D2DSize viewportsize, FighterPlane plane, float dt)
        {
            const float MIN_DIST = 600f;
            const float MAX_DIST = 10000f;
            const float POINTER_DIST = 300f;

            var pos = new D2DPoint(viewportsize.width * 0.5f, viewportsize.height * 0.5f);
            var color = Utilities.LerpColor(World.HudColor, D2DColor.WhiteSmoke, 0.3f);

            for (int i = 0; i < _objs.Planes.Count; i++)
            {
                var target = _objs.Planes[i];

                if (target == null)
                    continue;

                if (target.IsDisabled)
                    continue;

                var dist = D2DPoint.Distance(plane.Position, target.Position);

                if (dist < MIN_DIST || dist > MAX_DIST)
                    continue;

                var dir = target.Position - plane.Position;
                var angle = dir.Angle();
                var vec = Utilities.AngleToVectorDegrees(angle);

                ctx.Gfx.DrawArrow(pos + (vec * (POINTER_DIST - 20f)), pos + (vec * POINTER_DIST), color, 2f);

                // Draw lead indicator.
                if (World.ShowLeadIndicators)
                {
                    var leadAmt = GetTargetLeadAmount(target, plane, dt);
                    var angleDiff = Utilities.AngleDiff(angle, leadAmt);

                    if (Math.Abs(angleDiff) < 70f && plane.IsObjInFOV(target, 70f))
                    {
                        var leadVec = Utilities.AngleToVectorDegrees(leadAmt);

                        ctx.Gfx.DrawLine(pos + (vec * POINTER_DIST), pos + (leadVec * POINTER_DIST), color, 1f, D2DDashStyle.Dash);
                        ctx.Gfx.FillEllipseSimple(pos + (leadVec * POINTER_DIST), 3f, color);
                    }
                }
            }

            if (plane.Radar.HasLock)
            {
                var lockPos = pos + new D2DPoint(0f, -200f);
                var lRect = new D2DRect(lockPos, new D2DSize(120, 30));
                ctx.DrawText("LOCKED", _hudColorBrushLight, _textConsolas25Centered, lRect);
            }
        }

        private float GetTargetLeadAmount(GameObject target, FighterPlane plane, float dt)
        {
            const float pValue = 10f;

            var los = target.Position - plane.Position;
            var navigationTime = los.Length() / ((plane.AirSpeedTrue + Bullet.SPEED) * dt);
            var targRelInterceptPos = los + ((target.Velocity * dt) * navigationTime);

            targRelInterceptPos *= pValue;

            var leadRotation = ((target.Position + targRelInterceptPos) - plane.Position).Angle();
            var targetRot = leadRotation;

            return targetRot;
        }

        private void DrawMissilePointersAndWarnings(RenderContext ctx, D2DSize viewportsize, FighterPlane plane)
        {
            const float MAX_WARN_DIST = 60000f;
            const float MIN_IMPACT_TIME = 20f;

            bool warningMessage = false;
            var pos = new D2DPoint(viewportsize.width * 0.5f, viewportsize.height * 0.5f);

            for (int i = 0; i < _objs.Missiles.Count; i++)
            {
                var missile = _objs.Missiles[i] as GuidedMissile;

                if (missile == null)
                    continue;

                if (missile.Owner.Equals(plane))
                    continue;

                if (missile.Target != null && !missile.Target.Equals(plane))
                    continue;

                var dist = D2DPoint.Distance(plane.Position, missile.Position);

                if (dist > MAX_WARN_DIST)
                    continue;

                // If this missile is targeting and tracking us.
                if (missile.Target.Equals(plane) && !missile.MissedTarget)
                    warningMessage = true;

                var impactTime = Utilities.ImpactTime(missile, plane);

                // Draw pointer when the missile is close.
                if (warningMessage && impactTime > 0f)
                {
                    const float MAX_SIZE = 30f;

                    var dir = missile.Position - plane.Position;
                    var vec = dir.Normalized();
                    var pos1 = pos + (vec * 200f);
                    var pos2 = pos1 + (vec * 20f);
                    var impactFact = 1f - Utilities.FactorWithEasing(impactTime, MIN_IMPACT_TIME, EasingFunctions.Out.EaseQuad);

                    if (impactFact > 0f)
                        ctx.Gfx.DrawArrow(pos1, pos2, D2DColor.Red, (impactFact * MAX_SIZE) + 1f);
                }
            }

            var flashScale = Utilities.ScaleToRange(_warnLightFlashAmount, 0f, 1f, 0.98f, 1.1f);
            var flashAlpha = Utilities.ScaleToRange(_warnLightFlashAmount, 0f, 1f, 0.5f, 1f);

            // Lock light.
            if (plane.HasRadarLock)
            {
                var lockRect = new D2DRect(pos - new D2DPoint(0, -160), new D2DSize(120, 30));
                var lockColor = D2DColor.Red.WithAlpha(0.7f);

                ctx.Gfx.DrawRectangle(lockRect, lockColor);
                ctx.DrawText("LOCK", lockColor, _textConsolas30Centered, lockRect);
            }

            // Missile warning light.
            if (warningMessage)
            {
                var missileWarnPos = pos - new D2DPoint(0, -200);

                ctx.PushTransform();
                ctx.ScaleTransform(flashScale, flashScale, missileWarnPos);

                var missileWarnRect = new D2DRect(missileWarnPos, new D2DSize(120, 30));
                var warnColor = D2DColor.Red.WithAlpha(flashAlpha);

                ctx.Gfx.DrawRectangle(missileWarnRect, warnColor);
                ctx.DrawText("MISSILE", warnColor, _textConsolas30Centered, missileWarnRect);

                ctx.PopTransform();
            }

            // Engine out light.
            if (plane.EngineDamaged)
            {
                var engineOutPos = pos - new D2DPoint(0, -240);

                ctx.PushTransform();
                ctx.ScaleTransform(flashScale, flashScale, engineOutPos);

                var engineOutRect = new D2DRect(engineOutPos, new D2DSize(180, 30));
                var engineOutColor = D2DColor.Orange.WithAlpha(flashAlpha);

                ctx.Gfx.DrawRectangle(engineOutRect, engineOutColor);
                ctx.DrawText("ENGINE OUT", engineOutColor, _textConsolas30Centered, engineOutRect);

                ctx.PopTransform();
            }
        }

        private void DrawHudMessage(D2DGraphics gfx, D2DSize viewportsize)
        {
            const float FONT_SIZE = 30f;
            if (_hudMessageTimeout.IsRunning && !string.IsNullOrEmpty(_hudMessage))
            {
                var pos = new D2DPoint(viewportsize.width * 0.5f, 300f);
                var initSize = new D2DSize(600, 100);
                var size = gfx.MeasureText(_hudMessage, DEFAULT_FONT_NAME, FONT_SIZE, initSize);
                var rect = new D2DRect(pos, size);

                gfx.DrawTextCenter(_hudMessage, _hudMessageColor, DEFAULT_FONT_NAME, FONT_SIZE, rect);
            }

            if (!_hudMessageTimeout.IsRunning)
                _hudMessage = string.Empty;
        }

        private void DrawRadar(RenderContext ctx, D2DSize viewportsize, FighterPlane plane)
        {
            const float SCALE = 0.9f;
            var pos = new D2DPoint(viewportsize.width * 0.82f * HudScale * SCALE, viewportsize.height * 0.76f * HudScale * SCALE);

            ctx.PushTransform();
            ctx.ScaleTransform(SCALE, pos);
            ctx.TranslateTransform(pos);

            plane.Radar.Render(ctx);

            ctx.PopTransform();
        }

        private void DrawHealthBarAndAmmo(RenderContext ctx, FighterPlane plane, D2DPoint position, D2DSize size)
        {
            var healthPct = plane.Health / FighterPlane.MAX_HEALTH;

            if (healthPct > 0f && healthPct < 0.05f)
                healthPct = 0.05f;

            ctx.Gfx.DrawProgressBar(position, size, World.HudColor, World.HudColor, healthPct);

            // Draw loadout stats labels.
            var labelSize = new D2DSize(120f, 30f);
            var missilePos = position + new D2DPoint(-110f, 30f);
            DrawLabeledValue(ctx, missilePos, labelSize, D2DColor.Red, 1, plane.NumMissiles, "MSL");

            var decoyPos = position + new D2DPoint(0, 30f);
            DrawLabeledValue(ctx, decoyPos, labelSize, D2DColor.Red, 5, plane.NumDecoys, "DECOY");

            var ammoPos = position + new D2DPoint(110f, 30f);
            DrawLabeledValue(ctx, ammoPos, labelSize, D2DColor.Red, 10, plane.NumBullets, "AMMO");

            // Draw player name.
            if (string.IsNullOrEmpty(plane.PlayerName))
                return;

            var rect = new D2DRect(position + new D2DPoint(0, -40), new D2DSize(300, 100));
            ctx.DrawText(plane.PlayerName, _hudColorBrush, _textConsolas30Centered, rect);
        }

        private void DrawLabeledValue(RenderContext ctx, D2DPoint position, D2DSize size, D2DColor warnColor, float warnValue, float value, string label)
        {
            var flashScale = Utilities.ScaleToRange(_warnLightFlashAmount, 0f, 1f, 0.9f, 1.10f);

            var labelPos = position;
            var labelRect = new D2DRect(labelPos, size);
            var labelText = $"{label}: {value}";

            if (value <= warnValue)
            {
                ctx.PushTransform();
                ctx.ScaleTransform(flashScale, flashScale, labelPos);

                ctx.DrawText(labelText, warnColor, _textConsolas15Centered, labelRect);

                ctx.PopTransform();
            }
            else
            {
                ctx.DrawText(labelText, _hudColorBrush, _textConsolas15Centered, labelRect);
            }
        }

        private void DrawChatAndEventBox(RenderContext ctx)
        {
            const float SCALE = 1f;
            const float ACTIVE_SCALE = 1.8f;
            const float LEFT_PAD = 10f;
            const int LINES_ACTIVE = 20;
            const int LINES_INACTIVE = 8;
            const float LINE_HEIGHT = 10f;
            const float WIDTH = 400f;

            var viewportsize = World.ViewPortRectUnscaled.Size;
            
            // Fudge/compute the position and scaling of the chat box.
            // Apply user scaling and re-position while active for net chat. 
            var chatActive = _netMan != null && _netMan.ChatInterface.ChatIsActive;
            var scale = SCALE;
            var numLines = LINES_INACTIVE;
            var height = numLines * LINE_HEIGHT;
            var boxPos = new D2DPoint(330f * HudScale * scale, viewportsize.height - (180f * HudScale * scale));

            if (chatActive)
            {
                scale = ACTIVE_SCALE;
                numLines = LINES_ACTIVE;
                height = numLines * LINE_HEIGHT;
                boxPos = new D2DPoint((viewportsize.width * 0.5f), (viewportsize.height * 0.5f) - (height * 0.5f));
            }

            var linePos = new D2DPoint(boxPos.X + LEFT_PAD, boxPos.Y);
            var lineSize = new D2DSize(WIDTH, height / numLines);

            // TODO: These transforms are screwy..
            ctx.PushTransform();

            ctx.ScaleTransform(_hudScale, new D2DPoint(viewportsize.width * 0.5f, viewportsize.height * 0.5f));
            ctx.ScaleTransform(scale, boxPos);

            ctx.Gfx.FillRectangle(boxPos.X - (WIDTH / 2f), boxPos.Y - lineSize.height, WIDTH, height + lineSize.height, new D2DColor(0.05f, World.HudColor));

            var start = 0;

            if (_messageEvents.Count >= numLines)
                start = _messageEvents.Count - numLines;

            for (int i = start; i < _messageEvents.Count; i++)
            {
                var msg = _messageEvents[i];
                var rect = new D2DRect(linePos, lineSize);
                var brush = _hudColorBrushLight;

                switch (msg.Type)
                {
                    case EventType.Chat:
                        brush = _whiteColorBrush;
                        break;
                }

                ctx.Gfx.DrawText(msg.Message, brush, _messageBoxFont, rect);
                linePos += new D2DPoint(0, lineSize.height);
            }

            ctx.Gfx.DrawRectangle(boxPos.X - (WIDTH / 2f), boxPos.Y - lineSize.height, WIDTH, height + lineSize.height, World.HudColor);

            // Draw current chat message.
            if (chatActive)
            {
                var rect = new D2DRect(new D2DPoint(boxPos.X + LEFT_PAD, boxPos.Y + height + 6f), lineSize);
                var curText = _netMan.ChatInterface.CurrentText;

                if (string.IsNullOrEmpty(curText))
                    ctx.Gfx.DrawText("Type chat message...", _hudColorBrush, _messageBoxFont, rect);
                else
                    ctx.Gfx.DrawText(_netMan.ChatInterface.CurrentText, _whiteColorBrush, _messageBoxFont, rect);

                ctx.Gfx.DrawRectangle(boxPos.X - (WIDTH / 2f), boxPos.Y + height, WIDTH, lineSize.height + 5f, World.HudColor);
            }

            ctx.PopTransform();
        }

        private void DrawPopMessages(RenderContext ctx, D2DSize vpSize, FighterPlane viewPlane)
        {
            for (int i = 0; i < _popMessages.Count; i++)
            {
                var msg = _popMessages[i];

                if (msg.Displayed && msg.TargetPlayerID.Equals(viewPlane.ID))
                {
                    var color = Utilities.LerpColor(D2DColor.Red, D2DColor.Transparent, msg.Age / msg.LIFESPAN);
                    var rect = new D2DRect(msg.RenderPos, new D2DSize(600, 50));
                    ctx.DrawText(msg.Message, color, _textConsolas30Centered, rect);
                }
            }
        }

        private void DrawScoreCard(RenderContext ctx, D2DSize viewportsize)
        {
            var size = new D2DSize(viewportsize.width * 0.7f, viewportsize.height * 0.6f);
            var pos = new D2DPoint(viewportsize.width * 0.5f, viewportsize.height * 0.5f);
            var rect = new D2DRect(pos, size);
            var leftPad = 130f;
            var topPad = 60f;
            var topLeft = new D2DPoint(rect.left + leftPad, rect.top + topPad);

            // Draw background.
            ctx.Gfx.FillRectangle(rect, new D2DColor(0.3f, World.HudColor));
            ctx.Gfx.DrawRectangle(rect, World.HudColor, 4f);

            // Title
            var titleRect = new D2DRect(rect.left, rect.top + 10f, size.width, 30f);
            ctx.Gfx.DrawRectangle(titleRect, World.HudColor);
            ctx.Gfx.DrawText("SCORE", _whiteColorBrush, _textConsolas30Centered, titleRect);

            // Lines
            var lineHeight = 20f;
            var linePosY = topLeft.Y;

            var sortedPlanes = _objs.Planes.OrderByDescending(p => p.Kills).ThenBy(p => p.Deaths);
            var numPlanes = sortedPlanes.Count();

            if (_scoreScrollPos >= numPlanes)
                _scoreScrollPos = numPlanes - 1;

            for (int i = _scoreScrollPos; i < numPlanes; i++)
            {
                var playerPlane = sortedPlanes.ElementAt(i);
                var lineRect = new D2DRect(topLeft.X, linePosY, 800f, lineHeight);
                var lineRectColumn1 = new D2DRect(topLeft.X + 200f, linePosY, 800f, lineHeight);
                var lineRectColumn2 = new D2DRect(topLeft.X + 300f, linePosY, 800f, lineHeight);

                if (linePosY < rect.bottom - lineHeight)
                {
                    ctx.Gfx.DrawText($"[ {playerPlane.PlayerName} ]", _whiteColorBrush, _textConsolas15, lineRect);
                    ctx.Gfx.DrawText($"Kills: {playerPlane.Kills}", _whiteColorBrush, _textConsolas15, lineRectColumn1);
                    ctx.Gfx.DrawText($"Deaths: {playerPlane.Deaths}", _whiteColorBrush, _textConsolas15, lineRectColumn2);

                    linePosY += lineHeight;
                }
            }

            // Draw scroll bar.
            var scrollBarPos = new D2DPoint(rect.right - 10f, Utilities.Lerp(rect.top + lineHeight + titleRect.Height, rect.bottom, ((float)_scoreScrollPos / numPlanes)));
            var scrollBarRect = new D2DRect(scrollBarPos, new D2DSize(10f, 20f));
            ctx.Gfx.FillRectangle(scrollBarRect, D2DColor.White);
        }

        private void DrawFreeCamPrompt(RenderContext ctx)
        {
            const string MSG = "Free Camera Mode";

            var pos = new D2DPoint(World.ViewPortRectUnscaled.Size.width * 0.5f, 100f);
            var rect = new D2DRect(pos, new D2DSize(600, 50));
            ctx.DrawText(MSG, _redColorBrush, _textConsolas25Centered, rect);
        }

        private void DrawOverlays(RenderContext ctx, GameObject viewObject)
        {
            DrawInfo(ctx.Gfx, _infoPosition, viewObject);
            DrawDeathScreenOverlay(ctx, viewObject);
        }

        private void DrawDeathScreenOverlay(RenderContext ctx, GameObject viewObject)
        {
            const float DISPLAY_TIME = 30f;

            if (viewObject is FighterPlane plane)
            {
                if (plane.IsDisabled && plane.DeathTime > 0 && plane.DeathTime < DISPLAY_TIME)
                {
                    var alpha = Math.Clamp(1f - Utilities.FactorWithEasing(plane.DeathTime, DISPLAY_TIME, EasingFunctions.Out.EaseCircle), 0f, 0.4f);

                    if (alpha > 0f)
                        ctx.Gfx.FillRectangle(World.ViewPortRectUnscaled, _deathScreenColor.WithAlpha(alpha));
                }
            }
        }

        public void DrawInfo(D2DGraphics gfx, D2DPoint pos, GameObject viewObject)
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

            gfx.DrawText(_stringBuilder.ToString(), _greenYellowColorBrush, _textConsolas12, World.ViewPortRect.Deflate(30f, 30f));
        }

        private void DrawScreenFlash(D2DGraphics gfx)
        {
            _screenFlashColor.a = _screenFlashOpacity;

            if (_screenFlashOpacity > 0.01f)
                gfx.FillRectangle(World.ViewPortRectUnscaled, _screenFlashColor);
        }

        private void UpdatePopMessages(float dt)
        {
            for (int i = _popMessages.Count - 1; i >= 0; i--)
            {
                var msg = _popMessages[i];
                msg.UpdatePos(dt);

                if (!msg.Displayed)
                    _popMessages.RemoveAt(i);
            }
        }

        private void UpdateGroundColorBrush(RenderContext ctx)
        {
            _groundBrush?.Dispose();
            _groundBrush = _ctx.Device.CreateLinearGradientBrush(new D2DPoint(0f, 50f), new D2DPoint(0f, 4000f), [new D2DGradientStop(0.2f, ctx.AddTimeOfDayColor(_groundColorDark)), new D2DGradientStop(0.1f, ctx.AddTimeOfDayColor(_groundColorLight))]);
        }

        private void PlayerScoredEvent(object? sender, PlayerScoredEventArgs e)
        {
            if (!World.FreeCameraMode)
            {
                var startPos = new D2DPoint(this.Width / 2f, this.Height * 0.40f);

                // Message for scoring player.
                string msg = string.Empty;

                if (e.WasHeadshot)
                    msg = $"Headshot {e.Target.PlayerName}!";
                else
                    msg = $"Destroyed {e.Target.PlayerName}!";

                var scoringPlayerMsg = new PopMessage(msg, startPos, e.Player.ID);
                _popMessages.Add(scoringPlayerMsg);

                // Message for destroyed player.
                var killedPlayerMsg = new PopMessage($"Destroyed by {e.Player.PlayerName}", startPos, e.Target.ID);
                _popMessages.Add(killedPlayerMsg);
            }
        }

        private void PlayerKilledEvent(object? sender, EventMessage e)
        {
            AddNewEventMessage(e);
        }

        private void NewPlayerEvent(object? sender, FighterPlane e)
        {
            AddNewEventMessage($"'{e.PlayerName}' has joined.", EventType.Net);
        }

        private void NetMan_NewChatMessage(object? sender, ChatPacket e)
        {
            AddNewEventMessage($"{e.PlayerName}: {e.Message}", EventType.Chat);
        }

        public void AddNewEventMessage(string message, EventType type)
        {
            _messageEvents.Add(new EventMessage(message, type));
        }

        public void AddNewEventMessage(EventMessage msg)
        {
            _messageEvents.Add(msg);
        }

        public void NewHudMessage(string message, D2DColor color)
        {
            _hudMessage = message;
            _hudMessageColor = color;
            _hudMessageTimeout.Restart();
        }

        public void ClearHudMessage()
        {
            _hudMessage = null;
            _hudMessageTimeout.Stop();
            _hudMessageTimeout.Reset();
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

        public void DoMouseWheelUp()
        {
            if (!_showScore)
            {
                ZoomIn();
            }
            else
            {
                _scoreScrollPos -= 1;

                if (_scoreScrollPos < 0)
                    _scoreScrollPos = 0;
            }
        }

        public void DoMouseWheelDown()
        {
            if (!_showScore)
            {
                ZoomOut();
            }
            else
            {
                _scoreScrollPos += 1;
            }
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
            _screenFlashColor = color;
            _screenFlash?.Restart();
        }

        private string GetInfo(GameObject viewObject)
        {
            _stringBuilder.Clear();

            var numObj = _objs.TotalObjects;

            _stringBuilder.AppendLine($"FPS: {Math.Round(_fpsSmooth.Add(_renderFPS), 1)}");

            if (_showInfo)
            {
                if (World.IsNetGame && _netMan != null)
                {
                    _stringBuilder.AppendLine($"Latency: {Math.Round(_netMan.Host.GetPlayerRTT(0), 2)}");
                    _stringBuilder.AppendLine($"Packet Delay: {Math.Round(_netMan.PacketDelay, 2)}");
                    _stringBuilder.AppendLine($"Packet Loss: {_netMan.Host.PacketLoss()}");
                    _stringBuilder.AppendLine($"Packets Deferred: {_netMan.NumDeferredPackets}");
                    _stringBuilder.AppendLine($"Packets Expired: {_netMan.NumExpiredPackets}\n");
                }

                _stringBuilder.AppendLine($"Num Objects: {numObj}");
                _stringBuilder.AppendLine($"On Screen: {GraphicsExtensions.OnScreen}");
                _stringBuilder.AppendLine($"Off Screen: {GraphicsExtensions.OffScreen}");
                _stringBuilder.AppendLine($"Planes: {_objs.Planes.Count}");
                _stringBuilder.AppendLine($"Update ms: {Math.Round(_updateTimeSmooth.Add(UpdateTime.TotalMilliseconds), 2)}");
                _stringBuilder.AppendLine($"Render ms: {Math.Round(_renderTimeSmooth.Current, 2)}");
                _stringBuilder.AppendLine($"Collision ms: {Math.Round(CollisionTime.TotalMilliseconds, 2)}");
                _stringBuilder.AppendLine($"Total ms: {Math.Round(_updateTimeSmooth.Current + CollisionTime.TotalMilliseconds + _renderTimeSmooth.Current, 2)}");

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

    }
}
