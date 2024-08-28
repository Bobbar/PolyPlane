using PolyPlane.GameObjects;
using PolyPlane.GameObjects.Animations;
using PolyPlane.GameObjects.Tools;
using PolyPlane.Helpers;
using PolyPlane.Net;
using System.Diagnostics;
using unvell.D2DLib;

namespace PolyPlane.Rendering
{
    public class RenderManager : IDisposable
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

        private readonly D2DPoint _infoPosition = new D2DPoint(20, 20);

        private bool _showInfo = false;
        private bool _showHelp = false;
        private bool _showScore = false;
        private int _scoreScrollPos = 0;

        private SmoothDouble _renderTimeSmooth = new SmoothDouble(10);
        private Stopwatch _timer = new Stopwatch();
        private float _renderFPS = 0;
        private long _lastRenderTime = 0;
        private string _hudMessage = string.Empty;
        private GameTimer _hudMessageTimeout = new GameTimer(10f);
        private GameTimer _missileFlashTimer = new GameTimer(0.5f, 0.5f, false);
        private GameTimer _groundColorUpdateTimer = new GameTimer(4f, true);
        private List<EventMessage> _messageEvents = new List<EventMessage>();
        private List<PopMessage> _popMessages = new List<PopMessage>();

        private readonly string _defaultFontName = "Consolas";

        private Control _renderTarget;
        private readonly D2DColor _clearColor = D2DColor.Black;
        private GameObjectManager _objs = World.ObjectManager;
        private NetEventManager _netMan;

        private D2DPoint _screenShakeTrans = D2DPoint.Zero;

        private float _screenFlashOpacity = 0f;

        private D2DColor _hudMessageColor = D2DColor.Red;
        private D2DColor _screenFlashColor = D2DColor.Red;
        private readonly D2DColor _groundImpactOuterColor = new D2DColor(1f, 0.56f, 0.32f, 0.18f);
        private readonly D2DColor _groundImpactInnerColor = new D2DColor(1f, 0.35f, 0.2f, 0.1f);
        private readonly D2DColor _skyColorLight = new D2DColor(0.5f, D2DColor.SkyBlue);
        private readonly D2DColor _skyColorDark = new D2DColor(0.5f, D2DColor.Black);
        private readonly D2DColor _cloudColorLight = D2DColor.WhiteSmoke;
        private readonly D2DColor _cloudColorDark = new D2DColor(1f, 0.6f, 0.6f, 0.6f);
        private readonly D2DColor _groundColorLight = new D2DColor(1f, 0f, 0.29f, 0);
        private readonly D2DColor _groundColorDark = D2DColor.DarkGreen;

        private FloatAnimation _screenShakeX;
        private FloatAnimation _screenShakeY;
        private FloatAnimation _screenFlash;
        private const float VIEW_SCALE = 4f;
        private const float DEFAULT_DPI = 96f;
        private int _currentDPI = 96;

        private int Width => (int)(_renderTarget.Width / (_renderTarget.DeviceDpi / DEFAULT_DPI));
        private int Height => (int)(_renderTarget.Height / (_renderTarget.DeviceDpi / DEFAULT_DPI));

        private float _hudScale = 1f;


        private const int NUM_CLOUDS = 2000;
        private const int NUM_TREES = 1000;

        private const float MAX_CLOUD_X = 400000f;
        private const float CLOUD_SCALE = 5f;
        private const float GROUND_OBJ_SCALE = 4f;
        private const float SCREEN_SHAKE_G = 9f; // Amount of g-force before screen shake.
        private const float ZOOM_FACTOR = 0.07f; // Effects zoom in/out speed.
        private const float MESSAGEBOX_FONT_SIZE = 10f;

        private List<Cloud> _clouds = new List<Cloud>();
        private List<Tree> _trees = new List<Tree>();

        private D2DColor[] _todPallet =
        [
            new D2DColor(1f, 0f, 0f, 0f),
            new D2DColor(1f, 0f, 0f, 0f),
            new D2DColor(1f, 1f, 0.67f, 0f),
            new D2DColor(1f, 1f, 0.47f, 0f),
            new D2DColor(1f, 1f, 0f, 0.08f),
            new D2DColor(1f, 1f, 0f, 0.49f),
            new D2DColor(1f, 0.86f, 0f, 1f),
            new D2DColor(1f, 0.64f, 0.52f, 0.66f),
            new D2DColor(1f, 0.33f, 0.35f, 0.49f),
            new D2DColor(1f, 0.71f, 0.77f, 0.93f),
            new D2DColor(1f, 0.91f, 0.86f, 0.89f),
            new D2DColor(1f, 0.37f, 0.4f, 0.54f),
        ];

        private const double _gaussianSigma_2 = 0.035;
        private double _gaussianSigma = Math.Sqrt(2.0 * Math.PI * _gaussianSigma_2);

        private FPSLimiter _fpsLimiter = new FPSLimiter();

        public RenderManager(Control renderTarget, NetEventManager netMan)
        {
            _renderTarget = renderTarget;
            _netMan = netMan;

            if (_netMan != null)
                _netMan.NewChatMessage += NetMan_NewChatMessage;

            if (!World.IsNetGame)
            {
                _objs.PlayerKilledEvent += PlayerKilledEvent;
                _objs.NewPlayerEvent += NewPlayerEvent;
            }

            _objs.PlayerScoredEvent += PlayerScoredEvent;

            _missileFlashTimer.Start();

            InitProceduralGenStuff();
            InitRenderTarget();

            _groundColorUpdateTimer.TriggerCallback = () => UpdateGroundColor();
            _groundColorUpdateTimer.Start();
        }

        public void Dispose()
        {
            _hudMessageTimeout.Stop();
            _missileFlashTimer.Stop();
            _groundColorUpdateTimer.Stop();

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

            _device?.Dispose();
            _fpsLimiter?.Dispose();
        }

        private void PlayerScoredEvent(object? sender, PlayerScoredEventArgs e)
        {
            var msg = "+1 Kill!";
            var popMsg = new PopMessage() { Message = msg, Position = new D2DPoint(this.Width / 2f, this.Height * 0.40f), TargetPlayerID = e.Player.ID };
            _popMessages.Add(popMsg);
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

        private void InitRenderTarget()
        {
            _device?.Dispose();
            _device = D2DDevice.FromHwnd(_renderTarget.Handle);
            _device.Resize();
        }

        private void InitGfx()
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

            _screenFlash = new FloatAnimation(0.4f, 0f, 4f, EasingFunctions.EaseOutQuintic, v => _screenFlashOpacity = v);
            _screenShakeX = new FloatAnimation(5f, 0f, 2f, EasingFunctions.EaseOutElastic, v => _screenShakeTrans.X = v);
            _screenShakeY = new FloatAnimation(5f, 0f, 2f, EasingFunctions.EaseOutElastic, v => _screenShakeTrans.Y = v);

            _textConsolas12 = _ctx.Device.CreateTextFormat(_defaultFontName, 12f);
            _textConsolas15Centered = _ctx.Device.CreateTextFormat(_defaultFontName, 15f, D2DFontWeight.Normal, D2DFontStyle.Normal, D2DFontStretch.Normal, DWriteTextAlignment.Center, DWriteParagraphAlignment.Center);
            _textConsolas15 = _ctx.Device.CreateTextFormat(_defaultFontName, 15f);
            _textConsolas25Centered = _ctx.Device.CreateTextFormat(_defaultFontName, 25f, D2DFontWeight.Normal, D2DFontStyle.Normal, D2DFontStretch.Normal, DWriteTextAlignment.Center, DWriteParagraphAlignment.Center);
            _textConsolas30Centered = _ctx.Device.CreateTextFormat(_defaultFontName, 30f, D2DFontWeight.Normal, D2DFontStyle.Normal, D2DFontStretch.Normal, DWriteTextAlignment.Center, DWriteParagraphAlignment.Center);
            _textConsolas30 = _ctx.Device.CreateTextFormat(_defaultFontName, 30f);
            _messageBoxFont = _ctx.Device.CreateTextFormat(_defaultFontName, MESSAGEBOX_FONT_SIZE);

            _hudColorBrush = _ctx.Device.CreateSolidColorBrush(World.HudColor);
            _hudColorBrushLight = _ctx.Device.CreateSolidColorBrush(Utilities.LerpColor(World.HudColor, D2DColor.WhiteSmoke, 0.3f));
            _redColorBrush = _ctx.Device.CreateSolidColorBrush(D2DColor.Red);
            _whiteColorBrush = _ctx.Device.CreateSolidColorBrush(D2DColor.White);
            _greenYellowColorBrush = _ctx.Device.CreateSolidColorBrush(D2DColor.GreenYellow);

            _groundBrush = _ctx.Device.CreateLinearGradientBrush(new D2DPoint(0f, 50f), new D2DPoint(0f, 4000f), [new D2DGradientStop(0.2f, AddTimeOfDayColor(_groundColorDark)), new D2DGradientStop(0.1f, AddTimeOfDayColor(_groundColorLight))]);
        }

        public void ResizeGfx(bool force = false)
        {
            if (!force)
                if (World.ViewPortBaseSize.width == this.Width && World.ViewPortBaseSize.height == this.Height)
                    return;

            _device?.Resize();

            var scaleSize = GetViewportScaled();
            World.UpdateViewport(scaleSize);

            // Resizing graphics causes spikes in FPS. Try to limit them here.
            _fpsLimiter.Wait(60);
        }

        private Size GetViewportScaled()
        {
            var scaleSize = new Size((int)((float)_renderTarget.Size.Width / ((float)_currentDPI / World.DEFAULT_DPI)), (int)((float)_renderTarget.Size.Height / ((float)_currentDPI / World.DEFAULT_DPI)));
            return scaleSize;
        }

        public void InitProceduralGenStuff()
        {
            var rnd = new Random(1234);

            // Generate a pseudo-random? list of clouds.
            // I tried to do clouds procedurally, but wasn't having much luck.
            // It turns out that we need a surprisingly few number of clouds
            // to cover a very large area, so we will just brute force this for now.
            var cloudRangeX = new D2DPoint(-MAX_CLOUD_X, MAX_CLOUD_X);
            var cloudRangeY = new D2DPoint(-30000, -2000);
            var cloudDeDup = new HashSet<D2DPoint>();
            const int MIN_PNTS = 12;
            const int MAX_PNTS = 28;
            const int MIN_RADIUS = 5;
            const int MAX_RADIUS = 30;

            for (int i = 0; i < NUM_CLOUDS; i++)
            {
                var rndPos = new D2DPoint(rnd.NextFloat(cloudRangeX.X, cloudRangeX.Y), rnd.NextFloat(cloudRangeY.X, cloudRangeY.Y));

                while (!cloudDeDup.Add(rndPos))
                    rndPos = new D2DPoint(rnd.NextFloat(cloudRangeX.X, cloudRangeX.Y), rnd.NextFloat(cloudRangeY.X, cloudRangeY.Y));

                var rndCloud = Cloud.RandomCloud(rnd, rndPos, MIN_PNTS, MAX_PNTS, MIN_RADIUS, MAX_RADIUS);
                _clouds.Add(rndCloud);
            }

            // Add a more dense layer near the ground?
            var cloudLayerRangeY = new D2DPoint(-2500, -2000);
            for (int i = 0; i < NUM_CLOUDS / 2; i++)
            {
                var rndPos = new D2DPoint(rnd.NextFloat(cloudRangeX.X, cloudRangeX.Y), rnd.NextFloat(cloudLayerRangeY.X, cloudLayerRangeY.Y));

                while (!cloudDeDup.Add(rndPos))
                    rndPos = new D2DPoint(rnd.NextFloat(cloudRangeX.X, cloudRangeX.Y), rnd.NextFloat(cloudLayerRangeY.X, cloudLayerRangeY.Y));

                var rndCloud = Cloud.RandomCloud(rnd, rndPos, MIN_PNTS, MAX_PNTS, MIN_RADIUS, MAX_RADIUS);
                _clouds.Add(rndCloud);
            }


            // Gen trees.
            var treeDeDup = new HashSet<D2DPoint>();

            var trunkColorNormal = D2DColor.Chocolate;
            var trunkColorNormalDark = new D2DColor(1f, 0.29f, 0.18f, 0.105f);
            var leafColorNormal = D2DColor.ForestGreen;
            var trunkColorPine = D2DColor.BurlyWood;
            var leafColorPine = D2DColor.Green;
            var minDist = rnd.NextFloat(20f, 200f);

            for (int i = 0; i < NUM_TREES; i++)
            {
                var rndPos = new D2DPoint(rnd.NextFloat(cloudRangeX.X, cloudRangeX.Y), 0f);

                while (!treeDeDup.Add(rndPos) || (_trees.Count > 0 && _trees.Min(t => t.Position.DistanceTo(rndPos)) < minDist))
                    rndPos = new D2DPoint(rnd.NextFloat(cloudRangeX.X, cloudRangeX.Y), 0f);

                var type = rnd.Next(10);
                var height = 10f + (rnd.NextFloat(1f, 3f) * 20f);

                Tree newTree;

                if (type <= 8)
                {
                    var radius = rnd.NextFloat(40f, 80f);

                    var leafColor = leafColorNormal;
                    leafColor.g -= rnd.NextFloat(0.0f, 0.2f);

                    var trunkColor = Utilities.LerpColor(trunkColorNormal, trunkColorNormalDark, rnd.NextFloat(0f, 1f));
                    var trunkWidth = rnd.NextFloat(2f, 7f);

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

        public void AddNewEventMessage(string message, EventType type)
        {
            _messageEvents.Add(new EventMessage(message, type));
        }

        public void AddNewEventMessage(EventMessage msg)
        {
            _messageEvents.Add(msg);
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

        public void ZoomIn()
        {
            var amt = ZOOM_FACTOR * World.ZoomScale;
            World.ZoomScale += amt;

            ResizeGfx(force: true);
        }

        public void ZoomOut()
        {
            var amt = ZOOM_FACTOR * World.ZoomScale;
            World.ZoomScale -= amt;

            ResizeGfx(force: true);
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

        public void RenderFrame(FighterPlane viewplane)
        {
            InitGfx();
            ResizeGfx();

            _timer.Restart();

            UpdateTimersAndAnims();

            _gfx.BeginRender(_clearColor);

            if (viewplane != null)
            {
                var viewPortRect = new D2DRect(viewplane.Position, new D2DSize((World.ViewPortSize.width / VIEW_SCALE), World.ViewPortSize.height / VIEW_SCALE));
                _ctx.Viewport = viewPortRect;

                _gfx.PushTransform(); // Push screen shake transform.
                _gfx.TranslateTransform(_screenShakeTrans.X, _screenShakeTrans.Y);

                // Sky and background.
                DrawSky(_ctx, viewplane);
                DrawMovingBackground(_ctx, viewplane);

                _gfx.PushTransform(); // Push scale transform.
                _gfx.ScaleTransform(World.ZoomScale, World.ZoomScale);

                // Draw plane and other objects.
                DrawPlaneAndObjects(_ctx, viewplane);

                _gfx.PopTransform(); // Pop scale transform.

                // Draw HUD.
                var hudVPSize = new D2DSize(this.Width, this.Height);
                DrawHud(_ctx, hudVPSize, viewplane);

                _gfx.PopTransform(); // Pop screen shake transform.

                // Add overlays.
                DrawOverlays(_ctx, viewplane);

                if (viewplane.GForce > SCREEN_SHAKE_G)
                    DoScreenShake(viewplane.GForce / 5f);

                DrawScreenFlash(_gfx);
            }

            _timer.Stop();
            _renderTimeSmooth.Add(_timer.Elapsed.TotalMilliseconds);

            _gfx.EndRender();

            var now = DateTime.UtcNow.Ticks;
            var fps = TimeSpan.TicksPerSecond / (float)(now - _lastRenderTime);
            _lastRenderTime = now;
            _renderFPS = fps;
        }

        private void DrawPopMessages(RenderContext ctx, D2DSize vpSize, FighterPlane viewPlane)
        {
            for (int i = 0; i < _popMessages.Count; i++)
            {
                var msg = _popMessages[i];

                if (msg.Displayed && msg.TargetPlayerID.Equals(viewPlane.ID))
                {
                    var rect = new D2DRect(msg.RenderPos, new D2DSize(200, 50));
                    var color = Utilities.LerpColor(D2DColor.Red, D2DColor.Transparent, msg.Age / msg.LIFESPAN);
                    ctx.Gfx.DrawTextCenter(msg.Message, color, _defaultFontName, 30f, rect);
                }
                else
                {
                    _popMessages.RemoveAt(i);
                }

                msg.UpdatePos(World.DT);
            }
        }

        private void UpdateTimersAndAnims()
        {
            if (World.IsPaused)
                return;

            _hudMessageTimeout.Update(World.DT);
            _missileFlashTimer.Update(World.DT);
            _groundColorUpdateTimer.Update(World.DT);

            if (!_missileFlashTimer.IsInCooldown && !_missileFlashTimer.IsRunning)
                _missileFlashTimer.Restart();

            _screenFlash.Update(World.DT, World.RenderScale);
            _screenShakeX.Update(World.DT, World.RenderScale);
            _screenShakeY.Update(World.DT, World.RenderScale);
            MoveClouds(World.DT);
        }

        private void UpdateGroundColor()
        {
            _groundBrush?.Dispose();
            _groundBrush = _ctx.Device.CreateLinearGradientBrush(new D2DPoint(0f, 50f), new D2DPoint(0f, 4000f), [new D2DGradientStop(0.2f, AddTimeOfDayColor(_groundColorDark)), new D2DGradientStop(0.1f, AddTimeOfDayColor(_groundColorLight))]);
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

        private D2DColor AddTimeOfDayColor(D2DColor color)
        {
            var todColor = GetTimeOfDayColor();
            return Utilities.LerpColor(color, todColor, 0.3f);
        }

        private D2DColor GetTimeOfDayColor()
        {
            var todColor = InterpolateColorGaussian(_todPallet, World.TimeOfDay, World.MAX_TIMEOFDAY);
            return todColor;
        }

        private D2DColor GetShadowColor()
        {
            var shadowColor = new D2DColor(0.4f, Utilities.LerpColor(GetTimeOfDayColor(), D2DColor.Black, 0.7f));
            return shadowColor;
        }

        private D2DColor InterpolateColorGaussian(D2DColor[] colors, float value, float maxValue)
        {
            var x = Math.Min(1.0f, value / maxValue);

            double r = 0.0, g = 0.0, b = 0.0;
            double total = 0.0;
            double step = 1.0 / (double)(colors.Length - 1);
            double mu = 0.0;

            for (int i = 0; i < colors.Length; i++)
            {
                total += Math.Exp(-(x - mu) * (x - mu) / (2.0 * _gaussianSigma_2)) / _gaussianSigma;
                mu += step;
            }

            mu = 0.0;
            for (int i = 0; i < colors.Length; i++)
            {
                var color = colors[i];
                double percent = Math.Exp(-(x - mu) * (x - mu) / (2.0 * _gaussianSigma_2)) / _gaussianSigma;
                mu += step;

                r += color.r * percent / total;
                g += color.g * percent / total;
                b += color.b * percent / total;
            }

            return new D2DColor(1f, (float)r, (float)g, (float)b);
        }

        private void DrawScreenFlash(D2DGraphics gfx)
        {
            _screenFlashColor.a = _screenFlashOpacity;

            if (_screenFlashOpacity > 0.01f)
                gfx.FillRectangle(World.ViewPortRectUnscaled, _screenFlashColor);
        }

        public void DoScreenShake()
        {
            float amt = 10f;
            _screenShakeX.Start = Utilities.Rnd.NextFloat(-amt, amt);
            _screenShakeY.Start = Utilities.Rnd.NextFloat(-amt, amt);

            _screenShakeX.Reset();
            _screenShakeY.Reset();
        }

        public void DoScreenShake(float amt)
        {
            _screenShakeX.Start = Utilities.Rnd.NextFloat(-amt, amt);
            _screenShakeY.Start = Utilities.Rnd.NextFloat(-amt, amt);

            _screenShakeX.Reset();
            _screenShakeY.Reset();
        }

        public void DoScreenFlash(D2DColor color)
        {
            _screenFlashColor = color;
            _screenFlash.Reset();
        }

        private void DrawPlaneAndObjects(RenderContext ctx, FighterPlane plane)
        {
            var healthBarSize = new D2DSize(80, 20);

            ctx.Gfx.PushTransform();

            var zAmt = World.ZoomScale;
            var pos = new D2DPoint(World.ViewPortSize.width * 0.5f, World.ViewPortSize.height * 0.5f);
            pos *= zAmt;

            var offset = new D2DPoint(-plane.Position.X, -plane.Position.Y);
            offset *= zAmt;

            ctx.Gfx.ScaleTransform(VIEW_SCALE, VIEW_SCALE, plane.Position);
            ctx.Gfx.TranslateTransform(offset.X, offset.Y);
            ctx.Gfx.TranslateTransform(pos.X, pos.Y);

            var viewPortRect = new D2DRect(plane.Position, new D2DSize((World.ViewPortSize.width / VIEW_SCALE), World.ViewPortSize.height / VIEW_SCALE));
            viewPortRect = viewPortRect.Inflate(600f, 300f); // Inflate slightly to prevent "pop-in".

            ctx.PushViewPort(viewPortRect);

            DrawGround(ctx, plane);
            DrawGroundObjs(ctx, plane);
            DrawGroundImpacts(ctx, plane);

            RenderObjectsClamped(ctx, _objs.Decoys);
            _objs.MissileTrails.ForEach(o => o.Render(ctx));

            _objs.Missiles.ForEach(o =>
            {
                o.Render(ctx);

                // Circle enemy missiles.
                if (!o.Owner.Equals(plane))
                    ctx.DrawEllipse(new D2DEllipse(o.Position, new D2DSize(50f, 50f)), new D2DColor(0.4f, D2DColor.Red), 8f);
            });

            RenderObjectsClamped(ctx, _objs.Bullets);

            _objs.Planes.ForEach(p =>
            {
                if (!p.Equals(plane))
                {
                    DrawPlaneShadow(ctx, p);
                    p.Render(ctx);
                    DrawHealthBarClamped(ctx, p, new D2DPoint(p.Position.X, p.Position.Y - 110f), healthBarSize);
                    DrawMuzzleFlash(ctx, p);

                    // Draw circle around locked on plane.
                    if (plane.Radar.LockedObj != null && plane.Radar.LockedObj.Equals(p))
                        ctx.DrawEllipse(new D2DEllipse(p.Position, new D2DSize(80f, 80f)), World.HudColor, 4f);
                }
            });

            DrawPlaneShadow(ctx, plane);
            plane.Render(ctx);
            RenderObjectsClamped(ctx, _objs.Debris);
            RenderObjectsClamped(ctx, _objs.Explosions);

            DrawClouds(ctx);
            DrawPlaneCloudShadows(ctx);
            DrawLightingEffects(ctx, plane);
            DrawMuzzleFlash(ctx, plane);

            ctx.PopViewPort();
            ctx.Gfx.PopTransform();
        }

        /// <summary>
        /// Renders the specified list of objects and skips any which are outside the viewport.
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="objs"></param>
        private void RenderObjectsClamped(RenderContext ctx, List<GameObject> objs)
        {
            foreach (var obj in objs)
            {
                if (ctx.Viewport.Contains(obj.Position))
                {
                    obj.Render(ctx);
                }
            }
        }

        private void DrawPlaneCloudShadows(RenderContext ctx)
        {
            var shadowColor = new D2DColor(0.07f, GetShadowColor());
            foreach (var plane in _objs.Planes)
                ctx.DrawPolygon(plane.Polygon.Poly, shadowColor, 0f, D2DDashStyle.Solid, shadowColor);
        }

        private void DrawPlaneShadow(RenderContext ctx, FighterPlane plane)
        {
            const float MAX_WIDTH = 100f;
            const float HEIGHT = 10f;
            const float MAX_SIZE_ALT = 500f;
            const float MAX_SHOW_ALT = 1400f;
            const float Y_POS = 20f;

            var shadowPos = new D2DPoint(plane.Position.X, Y_POS);

            if (plane.Altitude > MAX_SHOW_ALT || !ctx.Viewport.Contains(shadowPos))
                return;

            var shadowWidth = Utilities.Lerp(0, MAX_WIDTH, Utilities.Factor(MAX_SIZE_ALT, plane.Altitude));
            if (plane.Altitude <= 0f)
                shadowWidth = MAX_WIDTH;

            ctx.FillEllipse(new D2DEllipse(shadowPos, new D2DSize(shadowWidth, HEIGHT)), GetShadowColor());
        }

        private void DrawLightingEffects(RenderContext ctx, FighterPlane plane)
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

            _objs.Bullets.ForEach(o =>
            {
                if (ctx.Viewport.Contains(o.Position))
                {
                    ctx.Gfx.PushTransform();
                    ctx.Gfx.TranslateTransform(o.Position.X * ctx.CurrentScale, o.Position.Y * ctx.CurrentScale);
                    ctx.Gfx.FillEllipseSimple(D2DPoint.Zero, BULLET_LIGHT_RADIUS, _bulletLightingBrush);
                    ctx.Gfx.PopTransform();
                }
            });

            _objs.Missiles.ForEach(o =>
            {
                if (o is GuidedMissile missile && missile.FlameOn && missile.CurrentFuel > 0f)
                {
                    if (ctx.Viewport.Contains(missile.Position))
                    {
                        ctx.Gfx.PushTransform();
                        ctx.Gfx.TranslateTransform(missile.CenterOfThrust.X * ctx.CurrentScale, missile.CenterOfThrust.Y * ctx.CurrentScale);
                        ctx.Gfx.FillEllipseSimple(D2DPoint.Zero, MISSILE_LIGHT_RADIUS, _missileLightingBrush);
                        ctx.Gfx.PopTransform();
                    }
                }
            });

            _objs.Decoys.ForEach(o =>
            {
                if (o is Decoy decoy)
                {
                    if (ctx.Viewport.Contains(decoy.Position) && (decoy.CurrentFrame % 21 == 0 || decoy.CurrentFrame % 33 == 0))
                    {
                        ctx.Gfx.PushTransform();
                        ctx.Gfx.TranslateTransform(decoy.Position.X * ctx.CurrentScale, decoy.Position.Y * ctx.CurrentScale);
                        ctx.Gfx.FillEllipseSimple(D2DPoint.Zero, DECOY_LIGHT_RADIUS, _decoyLightBrush);
                        ctx.Gfx.PopTransform();
                    }
                }
            });
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
                ctx.Gfx.PushTransform();
                ctx.Gfx.TranslateTransform(plane.GunPosition.X * ctx.CurrentScale, plane.GunPosition.Y * ctx.CurrentScale);
                ctx.Gfx.FillEllipseSimple(D2DPoint.Zero, MUZZ_FLASH_RADIUS, _muzzleFlashBrush);
                ctx.Gfx.PopTransform();
            }
        }

        private void DrawGround(RenderContext ctx, FighterPlane plane)
        {
            var groundPos = new D2DPoint(plane.Position.X, 0f);

            if (!ctx.Viewport.Contains(groundPos))
                return;

            const float HEIGHT = 500f;
            var yPos = HEIGHT / ctx.CurrentScale;
            groundPos += new D2DPoint(0f, yPos);

            // Draw the ground.
            ctx.Gfx.FillRectangle(new D2DRect(groundPos, new D2DSize(this.Width * World.ViewPortScaleMulti, (HEIGHT * 2f) / ctx.CurrentScale)), _groundBrush);
        }

        private void DrawGroundImpacts(RenderContext ctx, FighterPlane plane)
        {
            if (_groundClipLayer == null)
                _groundClipLayer = ctx.Device.CreateLayer();

            var rect = new D2DRect(new D2DPoint(plane.Position.X, 2000f), new D2DSize(this.Width * World.ViewPortScaleMulti, 4000f));

            using (var clipGeo = ctx.Device.CreateRectangleGeometry(rect))
            {
                ctx.Gfx.PushLayer(_groundClipLayer, ctx.Viewport, clipGeo);

                foreach (var impact in _objs.GroundImpacts)
                {
                    if (ctx.Viewport.Contains(impact.Position))
                    {
                        ctx.FillEllipse(new D2DEllipse(impact.Position, new D2DSize(impact.Size.width + 4f, impact.Size.height + 4f)), _groundImpactOuterColor);
                        ctx.FillEllipse(new D2DEllipse(impact.Position, new D2DSize(impact.Size.width, impact.Size.height)), _groundImpactInnerColor);
                    }
                }

                ctx.Gfx.PopLayer();
            }
        }

        private void DrawGroundObjs(RenderContext ctx, FighterPlane plane)
        {
            var todColor = GetTimeOfDayColor();

            foreach (var tree in _trees)
            {
                if (ctx.Viewport.Contains(tree.Position))
                {
                    tree.Render(ctx, todColor, GROUND_OBJ_SCALE);
                }
            }
        }

        //private void DrawHouse(RenderContext ctx, D2DPoint pos)
        //{
        //    var housePoly = new D2DPoint[]
        //    {
        //        new D2DPoint(0f, -1f),
        //        new D2DPoint(1f, -1f),
        //        new D2DPoint(1f, 1f),
        //        new D2DPoint(0f, 1f),
        //    };

        //    var roofPoly = new D2DPoint[]
        //    {
        //        new D2DPoint(-0.2f, -1f),
        //        new D2DPoint(0.5f, -1.8f),
        //        new D2DPoint(1.2f, -1f),
        //    };

        //    const float SCALE = 200f;
        //    Utilities.ApplyTranslation(housePoly, housePoly, 0f, pos, SCALE);
        //    Utilities.ApplyTranslation(roofPoly, roofPoly, 0f, pos , SCALE);


        //    ctx.DrawPolygon(housePoly, D2DColor.Gray, 1, D2DDashStyle.Solid, D2DColor.Gray);
        //    ctx.DrawPolygon(roofPoly, D2DColor.Gray, 1, D2DDashStyle.Solid, D2DColor.DarkRed);
        //}

        private void DrawHealthBarClamped(RenderContext ctx, FighterPlane plane, D2DPoint position, D2DSize size)
        {
            if (!ctx.Viewport.Contains(position))
                return;

            var healthPct = plane.Health / FighterPlane.MAX_HEALTH;

            if (healthPct > 0f && healthPct < 0.05f)
                healthPct = 0.05f;

            ctx.FillRectangle(new D2DRect(position.X - (size.width * 0.5f), position.Y - (size.height * 0.5f), size.width * healthPct, size.height), World.HudColor);
            ctx.DrawRectangle(new D2DRect(position, size), World.HudColor);

            // Draw player name.
            if (string.IsNullOrEmpty(plane.PlayerName))
                return;

            var rect = new D2DRect(position + new D2DPoint(0, -40), new D2DSize(300, 100));
            ctx.DrawText(plane.PlayerName, _hudColorBrush, _textConsolas30Centered, rect);
        }

        private void DrawHealthBar(D2DGraphics gfx, FighterPlane plane, D2DPoint position, D2DSize size)
        {
            var healthPct = plane.Health / FighterPlane.MAX_HEALTH;

            if (healthPct > 0f && healthPct < 0.05f)
                healthPct = 0.05f;

            gfx.FillRectangle(new D2DRect(position.X - (size.width * 0.5f), position.Y - (size.height * 0.5f), size.width * healthPct, size.height), World.HudColor);
            gfx.DrawRectangle(new D2DRect(position, size), World.HudColor);

            // Draw ammo.
            gfx.DrawText($"MSL: {plane.NumMissiles}", _hudColorBrush, _textConsolas15Centered, new D2DRect(position + new D2DPoint(-110f, 30f), new D2DSize(50f, 20f)));
            gfx.DrawText($"DECOY: {plane.NumDecoys}", _hudColorBrush, _textConsolas15Centered, new D2DRect(position + new D2DPoint(0, 30f), new D2DSize(80f, 20f)));
            gfx.DrawText($"AMMO: {plane.NumBullets}", _hudColorBrush, _textConsolas15Centered, new D2DRect(position + new D2DPoint(110f, 30f), new D2DSize(70f, 20f)));

            // Draw player name.
            if (string.IsNullOrEmpty(plane.PlayerName))
                return;

            var rect = new D2DRect(position + new D2DPoint(0, -40), new D2DSize(300, 100));
            gfx.DrawText(plane.PlayerName, _hudColorBrush, _textConsolas30Centered, rect);
        }

        private void DrawHud(RenderContext ctx, D2DSize viewportsize, FighterPlane viewPlane)
        {
            ctx.Gfx.PushTransform();
            ctx.Gfx.ScaleTransform(_hudScale, _hudScale, new D2DPoint(viewportsize.width * 0.5f, viewportsize.height * 0.5f));

            DrawAltimeter(ctx.Gfx, viewportsize, viewPlane);
            DrawSpeedo(ctx.Gfx, viewportsize, viewPlane);

            if (!viewPlane.IsDisabled)
            {
                if (viewPlane.IsAI == false)
                {
                    DrawGuideIcon(ctx.Gfx, viewportsize, viewPlane);
                    DrawGroundWarning(ctx, viewportsize, viewPlane);
                }

                DrawPlanePointers(ctx, viewportsize, viewPlane);
                DrawMissilePointers(ctx.Gfx, viewportsize, viewPlane);
            }

            DrawHudMessage(ctx.Gfx, viewportsize);
            DrawRadar(ctx, viewportsize, viewPlane);

            var healthBarSize = new D2DSize(300, 30);
            var pos = new D2DPoint(viewportsize.width * 0.5f, viewportsize.height - (viewportsize.height * 0.85f));
            DrawHealthBar(ctx.Gfx, viewPlane, pos, healthBarSize);

            DrawMessageBox(ctx, viewportsize, viewPlane);

            DrawPopMessages(ctx, viewportsize, viewPlane);


            if (_showScore)
                DrawScoreCard(ctx, viewportsize, viewPlane);

            ctx.Gfx.PopTransform();
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

        private void DrawMessageBox(RenderContext ctx, D2DSize viewportsize, FighterPlane plane)
        {
            const float SCALE = 1f;
            const float ACTIVE_SCALE = 1.4f;
            const float FONT_SIZE = 10f;
            const int MAX_LINES = 10;
            const float WIDTH = 400f;
            const float HEIGHT = 100f;

            var lineSize = new D2DSize(WIDTH, HEIGHT / MAX_LINES);
            var chatActive = _netMan != null && _netMan.ChatInterface.ChatIsActive;
            var scale = SCALE;

            if (chatActive)
                scale = ACTIVE_SCALE;

            var boxPos = new D2DPoint(160f * ((HudScale * 2f) * scale), viewportsize.height - ((100f * (HudScale * 2f)) * scale));
            var linePos = boxPos;

            ctx.Gfx.PushTransform();
            ctx.Gfx.ScaleTransform(scale, scale, boxPos);
            ctx.Gfx.FillRectangle(boxPos.X - (WIDTH / 2f) - 10f, boxPos.Y - lineSize.height, WIDTH, HEIGHT + lineSize.height, new D2DColor(0.05f, World.HudColor));

            var start = 0;

            if (_messageEvents.Count >= MAX_LINES)
                start = _messageEvents.Count - MAX_LINES;

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

            ctx.Gfx.DrawRectangle(boxPos.X - (WIDTH / 2f) - 10f, boxPos.Y - lineSize.height, WIDTH, HEIGHT + lineSize.height, World.HudColor);

            // Draw current chat message.
            if (chatActive)
            {
                var rect = new D2DRect(new D2DPoint(boxPos.X, boxPos.Y + HEIGHT + 6f), lineSize);
                var curText = _netMan.ChatInterface.CurrentText;

                if (string.IsNullOrEmpty(curText))
                    ctx.Gfx.DrawText("Type chat message...", _hudColorBrush, _messageBoxFont, rect);
                else
                    ctx.Gfx.DrawText(_netMan.ChatInterface.CurrentText, _whiteColorBrush, _messageBoxFont, rect);

                ctx.Gfx.DrawRectangle(boxPos.X - (WIDTH / 2f) - 10f, boxPos.Y + HEIGHT, WIDTH, lineSize.height + 5f, World.HudColor);
            }

            ctx.Gfx.PopTransform();
        }

        private void DrawScoreCard(RenderContext ctx, D2DSize viewportsize, FighterPlane plane)
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

            var sortedPlanes = _objs.Planes.OrderByDescending(p => p.Kills).ThenBy(p => p.Deaths).ToArray();

            if (_scoreScrollPos >= sortedPlanes.Length)
                _scoreScrollPos = sortedPlanes.Length - 1;

            for (int i = _scoreScrollPos; i < sortedPlanes.Length; i++)
            {
                var playerPlane = sortedPlanes[i];
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
            var scrollBarPos = new D2DPoint(rect.right - 10f, Utilities.Lerp(rect.top + lineHeight + titleRect.Height, rect.bottom, ((float)_scoreScrollPos / sortedPlanes.Length)));
            var scrollBarRect = new D2DRect(scrollBarPos, new D2DSize(10f, 20f));
            ctx.Gfx.FillRectangle(scrollBarRect, D2DColor.White);
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

        private void DrawHudMessage(D2DGraphics gfx, D2DSize viewportsize)
        {
            const float FONT_SIZE = 40f;
            if (_hudMessageTimeout.IsRunning && !string.IsNullOrEmpty(_hudMessage))
            {
                var pos = new D2DPoint(viewportsize.width * 0.5f, 300f);
                var initSize = new D2DSize(600, 100);
                var size = gfx.MeasureText(_hudMessage, _defaultFontName, FONT_SIZE, initSize);
                var rect = new D2DRect(pos, size);

                gfx.FillRectangle(rect.Inflate(10f, 10f), new D2DColor(0.7f, D2DColor.Gray));
                gfx.DrawTextCenter(_hudMessage, _hudMessageColor, _defaultFontName, FONT_SIZE, rect);
            }

            if (!_hudMessageTimeout.IsRunning)
                _hudMessage = string.Empty;
        }

        private void DrawAltimeter(D2DGraphics gfx, D2DSize viewportsize, FighterPlane plane)
        {
            const float MIN_ALT = 3000f;
            const float W = 80f;
            const float H = 350f;
            const float HalfW = W * 0.5f;
            const float HalfH = H * 0.5f;
            const float MARKER_STEP = 175f;

            var pos = new D2DPoint(viewportsize.width * 0.85f, viewportsize.height * 0.3f);
            var rect = new D2DRect(pos, new D2DSize(W, H));
            var alt = plane.Altitude;
            var startAlt = alt - (alt % MARKER_STEP) + MARKER_STEP;
            var altWarningColor = new D2DColor(0.2f, D2DColor.Red);

            var highestAlt = startAlt + MARKER_STEP;
            var lowestAlt = startAlt - (MARKER_STEP * 2f);

            gfx.DrawRectangle(rect, World.HudColor);
            gfx.DrawLine(new D2DPoint(pos.X - HalfW, pos.Y), new D2DPoint(pos.X + HalfW, pos.Y), D2DColor.GreenYellow, 1f, D2DDashStyle.Solid);

            if (highestAlt <= MIN_ALT || lowestAlt <= MIN_ALT)
            {
                var s = new D2DPoint(pos.X - HalfW, (pos.Y + (alt - MIN_ALT)));

                if (s.Y < pos.Y - HalfH)
                    s.Y = pos.Y - HalfH;

                var sRect = new D2DRect(s.X, s.Y, W, (pos.Y + (H * 0.5f)) - s.Y);

                if (sRect.Height > 0f)
                    gfx.FillRectangle(sRect, altWarningColor);
            }

            for (float y = 0; y <= H; y += MARKER_STEP)
            {
                if (y % MARKER_STEP == 0)
                {
                    var posY = (pos.Y - y + HalfH - MARKER_STEP) + (alt % MARKER_STEP);

                    if (posY < (pos.Y - HalfH))
                        continue;

                    var start = new D2DPoint(pos.X - HalfW, posY);
                    var end = new D2DPoint(pos.X + HalfW, posY);

                    var div = y / MARKER_STEP;
                    var altMarker = startAlt + (-HalfH + (div * MARKER_STEP));

                    gfx.DrawLine(start, end, World.HudColor, 1f, D2DDashStyle.Dash);
                    var textRect = new D2DRect(start - new D2DPoint(25f, 0f), new D2DSize(60f, 30f));
                    gfx.DrawText(altMarker.ToString(), _hudColorBrush, _textConsolas15Centered, textRect);
                }
            }

            var actualRect = new D2DRect(new D2DPoint(pos.X, pos.Y + HalfH + 20f), new D2DSize(60f, 20f));
            gfx.DrawText(Math.Round(alt, 0).ToString(), _hudColorBrush, _textConsolas15Centered, actualRect);
        }


        private void DrawSpeedo(D2DGraphics gfx, D2DSize viewportsize, FighterPlane plane)
        {
            const float MIN_SPEED = 250f;
            const float W = 80f;
            const float H = 350f;
            const float HalfW = W * 0.5f;
            const float HalfH = H * 0.5f;
            const float MARKER_STEP = 50f;

            var pos = new D2DPoint(viewportsize.width * 0.15f, viewportsize.height * 0.3f);
            var rect = new D2DRect(pos, new D2DSize(W, H));
            var spd = plane.AirSpeedIndicated;

            var startSpd = (spd) - (spd % (MARKER_STEP)) + MARKER_STEP;
            var spdWarningColor = new D2DColor(0.2f, D2DColor.Red);

            var highestSpd = startSpd + MARKER_STEP;
            //var lowestSpd = startSpd - (MARKER_STEP * 2f);
            var lowestSpd = (startSpd - HalfH) - MARKER_STEP;

            gfx.DrawRectangle(rect, World.HudColor);
            gfx.DrawLine(new D2DPoint(pos.X - HalfW, pos.Y), new D2DPoint(pos.X + HalfW, pos.Y), D2DColor.GreenYellow, 1f, D2DDashStyle.Solid);

            if (highestSpd <= MIN_SPEED || lowestSpd <= MIN_SPEED)
            {
                var s = new D2DPoint(pos.X - HalfW, (pos.Y + (spd - MIN_SPEED)));

                if (s.Y < pos.Y - HalfH)
                    s.Y = pos.Y - HalfH;

                var sRect = new D2DRect(s.X, s.Y, W, (pos.Y + (H * 0.5f)) - s.Y);

                if (sRect.Height > 0f)
                    gfx.FillRectangle(sRect, spdWarningColor);
            }


            for (float y = 0; y < H; y += MARKER_STEP)
            {
                if (y % MARKER_STEP == 0)
                {
                    var start = new D2DPoint(pos.X - HalfW, (pos.Y - y + HalfH - MARKER_STEP) + (spd % MARKER_STEP));
                    var end = new D2DPoint(pos.X + HalfW, (pos.Y - y + HalfH - MARKER_STEP) + (spd % MARKER_STEP));

                    var div = y / MARKER_STEP;
                    var altMarker = startSpd + (-HalfH + (div * MARKER_STEP));

                    gfx.DrawLine(start, end, World.HudColor, 1f, D2DDashStyle.Dash);
                    var textRect = new D2DRect(start - new D2DPoint(25f, 0f), new D2DSize(60f, 30f));
                    gfx.DrawText(altMarker.ToString(), _hudColorBrush, _textConsolas15Centered, textRect);
                }
            }

            var speedRect = new D2DRect(new D2DPoint(pos.X, pos.Y + HalfH + 20f), new D2DSize(60f, 20f));
            gfx.DrawText(Math.Round(spd, 0).ToString(), _hudColorBrush, _textConsolas15Centered, speedRect);

            var gforceRect = new D2DRect(new D2DPoint(pos.X, pos.Y - HalfH - 20f), new D2DSize(60f, 20f));
            gfx.DrawText($"G {Math.Round(plane.GForce, 1)}", _hudColorBrush, _textConsolas15, gforceRect);
        }

        private void DrawPlanePointers(RenderContext ctx, D2DSize viewportsize, FighterPlane plane)
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
                    var lead = LeadTarget(target, plane);
                    var angleDiff = Utilities.AngleDiff(angle, lead);

                    if (Math.Abs(angleDiff) < 70f && plane.IsObjInFOV(target, 70f))
                    {
                        var leadVec = Utilities.AngleToVectorDegrees(lead);

                        ctx.Gfx.DrawLine(pos + (vec * POINTER_DIST), pos + (leadVec * POINTER_DIST), color, 1f, D2DDashStyle.Dash);
                        ctx.Gfx.FillEllipseSimple(pos + (leadVec * POINTER_DIST), 3f, color);
                    }
                }
            }

            if (plane.Radar.HasLock)
            {
                var lockPos = pos + new D2DPoint(0f, -200f);
                var lRect = new D2DRect(lockPos, new D2DSize(120, 30));
                ctx.Gfx.DrawText("LOCKED", _hudColorBrushLight, _textConsolas25Centered, lRect);
            }
        }

        private float LeadTarget(GameObject target, FighterPlane plane)
        {
            const float pValue = 10f;

            var los = target.Position - plane.Position;
            var navigationTime = los.Length() / ((plane.AirSpeedTrue + Bullet.SPEED) * World.DT);
            var targRelInterceptPos = los + ((target.Velocity * World.DT) * navigationTime);

            targRelInterceptPos *= pValue;

            var leadRotation = ((target.Position + targRelInterceptPos) - plane.Position).Angle();
            var targetRot = leadRotation;

            return targetRot;
        }

        private void DrawRadar(RenderContext ctx, D2DSize viewportsize, FighterPlane plane)
        {
            const float SCALE = 0.8f;
            var pos = new D2DPoint(viewportsize.width - (100f * (HudScale * 2.5f)), viewportsize.height - (90f * (HudScale * 2.5f)));

            ctx.Gfx.PushTransform();
            ctx.Gfx.ScaleTransform(SCALE, SCALE, pos);

            plane.Radar.Position = pos;
            plane.Radar.Render(ctx);

            ctx.Gfx.PopTransform();
        }

        private void DrawMissilePointers(D2DGraphics gfx, D2DSize viewportsize, FighterPlane plane)
        {
            const float MIN_DIST = 1000f;

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

                var dir = missile.Position - plane.Position;
                var angle = dir.Angle();
                var color = D2DColor.Red;
                var vec = Utilities.AngleToVectorDegrees(angle);
                var pos1 = pos + (vec * 200f);
                var pos2 = pos1 + (vec * 20f);
                var distFact = 1f - Utilities.Factor(dist, MIN_DIST * 10f);

                // Display warning if impact time is less than 10 seconds?
                const float MIN_IMPACT_TIME = 20f;
                if (MissileIsImpactThreat(plane, missile, MIN_IMPACT_TIME))
                    warningMessage = true;

                var impactTime = Utilities.ImpactTime(plane, missile);

                if (impactTime > MIN_IMPACT_TIME * 1.5f)
                    continue;

                var impactFact = 1f - Utilities.FactorWithEasing(impactTime, MIN_IMPACT_TIME, EasingFunctions.EaseOutQuad);

                if (!missile.MissedTarget && warningMessage)
                    gfx.DrawArrow(pos1, pos2, color, (impactFact * 30f) + 1f);
            }

            if (warningMessage)
            {
                var rect = new D2DRect(pos - new D2DPoint(0, -200), new D2DSize(120, 30));
                var warnColor = D2DColor.Red.WithAlpha(_missileFlashTimer.Value / _missileFlashTimer.Interval);
                gfx.DrawRectangle(rect, warnColor);
                gfx.DrawTextCenter("MISSILE", warnColor, _defaultFontName, 30f, rect);
            }

            if (plane.HasRadarLock)
            {
                var lockRect = new D2DRect(pos - new D2DPoint(0, -160), new D2DSize(120, 30));
                var lockColor = D2DColor.Red.WithAlpha(0.7f);
                gfx.DrawRectangle(lockRect, lockColor);
                gfx.DrawTextCenter("LOCK", lockColor, _defaultFontName, 30f, lockRect);
            }
        }

        private bool MissileIsImpactThreat(FighterPlane plane, Missile missile, float minImpactTime)
        {
            var navigationTime = Utilities.ImpactTime(plane, missile);

            // Is it going to hit soon and is actively targeting us?
            return (navigationTime < minImpactTime && missile.Target.Equals(plane) && missile.ClosingRate(plane) > 0f);
        }


        private void DrawOverlays(RenderContext ctx, FighterPlane viewplane)
        {
            DrawInfo(ctx.Gfx, _infoPosition, viewplane);

            if (viewplane.IsDisabled)
                ctx.Gfx.FillRectangle(World.ViewPortRectUnscaled, new D2DColor(0.2f, D2DColor.Red));
        }

        private void DrawSky(RenderContext ctx, FighterPlane viewPlane)
        {
            const float MAX_ALT_OFFSET = 10000f;

            var plrAlt = viewPlane.Altitude;
            if (viewPlane.Position.Y >= 0)
                plrAlt = 0f;

            var color1 = _skyColorLight;
            var color2 = _skyColorDark;
            var color = Utilities.LerpColor(color1, color2, (plrAlt / (World.MAX_ALTITUDE - MAX_ALT_OFFSET)));

            // Add time of day color.
            color = Utilities.LerpColor(color, D2DColor.Black, Utilities.Factor(World.TimeOfDay, World.MAX_TIMEOFDAY - 5f));
            color = Utilities.LerpColor(color, AddTimeOfDayColor(color), 0.2f);

            var rect = new D2DRect(new D2DPoint(this.Width * 0.5f, this.Height * 0.5f), new D2DSize(this.Width, this.Height));

            ctx.Gfx.FillRectangle(rect, color);
        }

        private void DrawMovingBackground(RenderContext ctx, FighterPlane viewPlane)
        {
            float spacing = 75f;
            const float size = 4f;
            var d2dSz = new D2DSize(size, size);
            var color = new D2DColor(0.3f, D2DColor.Gray);

            var plrPos = viewPlane.Position;
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

        private void DrawClouds(RenderContext ctx)
        {
            for (int i = 0; i < _clouds.Count; i++)
            {
                var cloud = _clouds[i];

                if (ctx.Viewport.Contains(cloud.Position))
                {
                    DrawCloud(ctx, cloud);
                }

                DrawCloudShadow(ctx, cloud);
            }
        }

        private void DrawCloud(RenderContext ctx, Cloud cloud)
        {
            var color1 = _cloudColorDark;
            var color2 = _cloudColorLight;
            var points = cloud.Points;

            // Find min/max height.
            var minY = points.Min(p => p.Y);
            var maxY = points.Max(p => p.Y);

            for (int i = 0; i < points.Length; i++)
            {
                var point = points[i];
                var dims = cloud.Dims[i];

                // Lerp slightly darker colors to give the cloud some depth.

                //// Darken by number of clouds.
                //var amt = Utilities.Factor(i, (float)points.Length);
                //var color = Utilities.LerpColor(color1, color2, amt); 

                ////Darker clouds on top.
                //var amt = Utilities.Factor(point.Y, minY, maxY);
                //var color = Utilities.LerpColor(color1, color2, amt); 

                //Darker clouds on bottom.
                var amt = Utilities.Factor(point.Y, minY, maxY);
                var color = Utilities.LerpColor(color1, color2, 1f - amt);

                // Add time of day color.
                color = AddTimeOfDayColor(color);

                ctx.FillEllipse(new D2DEllipse(point, dims), color);
            }
        }

        private void DrawCloudShadow(RenderContext ctx, Cloud cloud)
        {
            if (cloud.Position.Y < -8000f)
                return;

            var todOffset = Utilities.Lerp(600f, -600f, Utilities.Factor(World.TimeOfDay, World.MAX_TIMEOFDAY));

            if (!ctx.Viewport.Contains(new D2DPoint(cloud.Position.X + todOffset, 0f)))
                return;

            var shadowColor = new D2DColor(0.05f, Utilities.LerpColor(GetTimeOfDayColor(), D2DColor.Black, 0.7f));

            for (int i = 0; i < cloud.Points.Length; i++)
            {
                var point = cloud.Points[i];
                var dims = cloud.Dims[i];

                var groundPos = new D2DPoint(point.X, -80f + Math.Abs((point.Y * 0.1f)));
                groundPos.X += todOffset;

                if (ctx.Viewport.Contains(groundPos))
                    ctx.FillEllipse(new D2DEllipse(groundPos, new D2DSize(dims.width * 4f, dims.height * 0.5f)), shadowColor);
            }
        }

        private void MoveClouds(float dt)
        {
            const float RATE = 40f;
            for (int i = 0; i < _clouds.Count; i++)
            {
                var cloud = _clouds[i];

                var altFact = 30f * Utilities.Factor(Math.Abs(cloud.Position.Y), 30000f); // Higher clouds move slower?
                var sizeOffset = (cloud.Radius / 2f); // Smaller clouds move slightly faster?
                cloud.Position.X += ((RATE - altFact) - sizeOffset) * dt;

                float rotDir = 1f;

                // Fiddle rotation direction.
                if (cloud.Points.Length % 2 == 0)
                    rotDir = -1f;

                cloud.Rotation = Utilities.ClampAngle(cloud.Rotation + (0.8f * rotDir) * dt);

                // Wrap clouds.
                if (cloud.Position.X > MAX_CLOUD_X)
                {
                    cloud.Position.X = -MAX_CLOUD_X;
                }

                // Rotate then scale.
                Utilities.ApplyTranslation(cloud.PointsOrigin, cloud.Points, cloud.Rotation, cloud.Position, CLOUD_SCALE);
                Utilities.ApplyTranslation(cloud.Points, cloud.Points, cloud.Position, 0f, D2DPoint.Zero, cloud.ScaleX, cloud.ScaleY);
            }
        }

        public void DrawInfo(D2DGraphics gfx, D2DPoint pos, FighterPlane viewplane)
        {
            var infoText = GetInfo(viewplane);

            if (_showHelp)
            {
                infoText += "\nH: Hide help\n\n";

                if (!World.IsNetGame)
                {
                    infoText += $"Alt + Enter: Toggle Fullscreen\n";
                    infoText += $"P: Pause\n";
                    infoText += $"U: Spawn AI Plane\n";
                    infoText += $"C: Remove AI Planes\n";
                }

                infoText += $"Y: Start Chat Message\n";
                infoText += $"Tab: Show Scores\n";
                infoText += $"(+/-): Zoom\n";
                infoText += $"Shift + (+/-): HUD Scale\n";
                infoText += $"Left-Click: Fire Bullets\n";
                infoText += $"Right-Click: Drop Decoys\n";
                infoText += $"Middle-Click/Space Bar: Fire Missile\n";
                infoText += $"L: Toggle Lead Indicators\n";

                infoText += $"\nSpectate (While crashed)\n";
                infoText += $"([/]): Prev/Next Spectate Plane\n";
                infoText += $"Backspace: Reset Spectate\n";
            }
            else
            {
                infoText += "\n";
                infoText += "H: Show help";
            }

            gfx.DrawText(infoText, _greenYellowColorBrush, _textConsolas12, World.ViewPortRect.Deflate(20f, 20f));
        }

        private string GetInfo(FighterPlane viewplane)
        {
            string infoText = string.Empty;

            var numObj = _objs.TotalObjects;
            infoText += $"FPS: {Math.Round(_renderFPS, 0)}\n";

            if (_netMan != null)
            {
                infoText += $"Packet Delay: {Math.Round(_netMan.PacketDelay, 2)}\n";
                infoText += $"Latency: {_netMan.Host.GetPlayerRTT(0)}\n";
                infoText += $"Packet Loss: {_netMan.Host.PacketLoss()}\n";
            }

            if (_showInfo)
            {
                infoText += $"Num Objects: {numObj}\n";
                infoText += $"On Screen: {GraphicsExtensions.OnScreen}\n";
                infoText += $"Off Screen: {GraphicsExtensions.OffScreen}\n";
                infoText += $"Planes: {_objs.Planes.Count}\n";
                infoText += $"Update ms: {Math.Round(UpdateTime.TotalMilliseconds, 2)}\n";
                infoText += $"Render ms: {Math.Round(_renderTimeSmooth.Current, 2)}\n";
                infoText += $"Collision ms: {Math.Round(CollisionTime.TotalMilliseconds, 2)}\n";
                infoText += $"Total ms: {Math.Round(UpdateTime.TotalMilliseconds + CollisionTime.TotalMilliseconds + _renderTimeSmooth.Current, 2)}\n";

                infoText += $"Zoom: {Math.Round(World.ZoomScale, 2)}\n";
                infoText += $"HUD Scale: {_hudScale}\n";
                infoText += $"DT: {Math.Round(World.DT, 4)}\n";
                infoText += $"AutoPilot: {(viewplane.AutoPilotOn ? "On" : "Off")}\n";
                infoText += $"Position: {viewplane?.Position}\n";
                infoText += $"Kills: {viewplane.Kills}\n";
                infoText += $"Bullets (Fired/Hit): ({viewplane.BulletsFired} / {viewplane.BulletsHit}) \n";
                infoText += $"Missiles (Fired/Hit): ({viewplane.MissilesFired} / {viewplane.MissilesHit}) \n";
                infoText += $"Headshots: {viewplane.Headshots}\n";
                infoText += $"Interp: {World.InterpOn.ToString()}\n";
                infoText += $"GunsOnly: {World.GunsOnly.ToString()}\n";
                infoText += $"TimeOfDay: {World.TimeOfDay.ToString()}\n";
                infoText += $"VP: {this.Width}, {this.Height}\n";
                infoText += $"DPI: {this._renderTarget.DeviceDpi}\n";
                infoText += $"TimeOffset: {World.ServerTimeOffset}\n";


            }

            return infoText;
        }

    }
}
