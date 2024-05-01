using PolyPlane.GameObjects;
using PolyPlane.GameObjects.Animations;
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
        private D2DGraphics _gfx;
        private RenderContext _ctx;
        private D2DLayer _groundClipLayer = null;
        private D2DRadialGradientBrush _bulletLightingBrush = null;
        private D2DRadialGradientBrush _missileLightingBrush = null;
        private D2DRadialGradientBrush _muzzleFlashBrush = null;
        private D2DRadialGradientBrush _decoyLightBrush = null;

        private readonly D2DPoint _infoPosition = new D2DPoint(20, 20);

        private bool _showInfo = false;
        private bool _showHelp = false;

        private SmoothDouble _renderTimeSmooth = new SmoothDouble(10);
        private Stopwatch _timer = new Stopwatch();
        private float _renderFPS = 0;
        private long _lastRenderTime = 0;
        private string _hudMessage = string.Empty;
        private D2DColor _hudMessageColor = D2DColor.Red;
        private GameTimer _hudMessageTimeout = new GameTimer(10f);
        private List<EventMessage> _messageEvents = new List<EventMessage>();

        private readonly string _defaultFontName = "Consolas";

        private Control _renderTarget;
        private readonly D2DColor _clearColor = D2DColor.Black;
        private GameObjectManager _objs;
        private NetEventManager _netMan;

        private D2DPoint _screenShakeTrans = D2DPoint.Zero;

        private float _screenFlashOpacity = 0f;
        private D2DColor _screenFlashColor = D2DColor.Red;
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


        public RenderManager(Control renderTarget, GameObjectManager objs, NetEventManager netMan)
        {
            _renderTarget = renderTarget;
            _objs = objs;
            _netMan = netMan;

            if (_netMan != null)
                _netMan.NewChatMessage += NetMan_NewChatMessage;

            _objs.PlayerKilledEvent += PlayerKilledEvent;
            _objs.NewPlayerEvent += NewPlayerEvent;

            InitProceduralGenStuff();
            InitGfx();
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

        public void InitGfx()
        {
            _device?.Dispose();
            _device = D2DDevice.FromHwnd(_renderTarget.Handle);
            _gfx = new D2DGraphics(_device);
            _gfx.Antialias = true;
            _device.Resize();
            _ctx = new RenderContext(_gfx, _device);

            _currentDPI = _renderTarget.DeviceDpi;

            var scaleSize = GetViewportScaled();
            World.UpdateViewport(scaleSize);

            _screenFlash = new FloatAnimation(0.4f, 0f, 4f, EasingFunctions.EaseQuinticOut, v => _screenFlashOpacity = v);
            _screenShakeX = new FloatAnimation(5f, 0f, 2f, EasingFunctions.EaseOutElastic, v => _screenShakeTrans.X = v);
            _screenShakeY = new FloatAnimation(5f, 0f, 2f, EasingFunctions.EaseOutElastic, v => _screenShakeTrans.Y = v);
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

                    var trunkColor = Helpers.LerpColor(trunkColorNormal, trunkColorNormalDark, rnd.NextFloat(0f, 1f));
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

        public void RenderFrame(FighterPlane viewplane)
        {
            ResizeGfx();

            _timer.Restart();

            UpdateTimersAndAnims();

            if (viewplane != null)
            {
                var viewPortRect = new D2DRect(viewplane.Position, new D2DSize((World.ViewPortSize.width / VIEW_SCALE), World.ViewPortSize.height / VIEW_SCALE));
                _ctx.Viewport = viewPortRect;

                _gfx.BeginRender(_clearColor);

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

                if (viewplane.GForce > 17f)
                    DoScreenShake(viewplane.GForce / 10f);

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

        private void UpdateTimersAndAnims()
        {
            _hudMessageTimeout.Update(World.DT);
            _screenFlash.Update(World.DT, World.ViewPortSize, World.RenderScale);
            _screenShakeX.Update(World.DT, World.ViewPortSize, World.RenderScale);
            _screenShakeY.Update(World.DT, World.ViewPortSize, World.RenderScale);
            MoveClouds(World.DT);
        }

        public void ResizeGfx(bool force = false)
        {
            if (!force)
                if (World.ViewPortBaseSize.width == this.Width && World.ViewPortBaseSize.height == this.Height)
                    return;

            _device?.Resize();

            var scaleSize = GetViewportScaled();
            World.UpdateViewport(scaleSize);
        }

        private Size GetViewportScaled()
        {
            var scaleSize = new Size((int)((float)_renderTarget.Size.Width / ((float)_currentDPI / (float)DEFAULT_DPI)), (int)((float)_renderTarget.Size.Height / ((float)_currentDPI / DEFAULT_DPI)));
            return scaleSize;
        }

        public void Dispose()
        {
            _groundClipLayer?.Dispose();
            _bulletLightingBrush?.Dispose();
            _missileLightingBrush?.Dispose();
            _muzzleFlashBrush?.Dispose();
            _decoyLightBrush?.Dispose();
            _device?.Dispose();
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
            return Helpers.LerpColor(color, todColor, 0.3f);
        }

        private D2DColor GetTimeOfDayColor()
        {
            var todColor = InterpolateColorGaussian(_todPallet, World.TimeOfDay, World.MAX_TIMEOFDAY);
            return todColor;
        }

        private D2DColor GetShadowColor()
        {
            var shadowColor = new D2DColor(0.4f, Helpers.LerpColor(GetTimeOfDayColor(), D2DColor.Black, 0.7f));
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
            gfx.FillRectangle(World.ViewPortRect, _screenFlashColor);
        }

        public void DoScreenShake()
        {
            float amt = 10f;
            _screenShakeX.Start = Helpers.Rnd.NextFloat(-amt, amt);
            _screenShakeY.Start = Helpers.Rnd.NextFloat(-amt, amt);

            _screenShakeX.Reset();
            _screenShakeY.Reset();
        }

        public void DoScreenShake(float amt)
        {
            _screenShakeX.Start = Helpers.Rnd.NextFloat(-amt, amt);
            _screenShakeY.Start = Helpers.Rnd.NextFloat(-amt, amt);

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

            _objs.Decoys.ForEach(o => o.Render(ctx));
            _objs.Missiles.ForEach(o =>
            {
                o.Render(ctx);

                // Circle enemy missiles.
                if (!o.Owner.ID.Equals(plane.ID))
                    ctx.DrawEllipse(new D2DEllipse(o.Position, new D2DSize(50f, 50f)), new D2DColor(0.4f, D2DColor.Red), 8f);
            });

            _objs.MissileTrails.ForEach(o => o.Render(ctx));
            _objs.Bullets.ForEach(o => o.Render(ctx));

            _objs.Planes.ForEach(o =>
            {
                if (o is FighterPlane tplane && !tplane.ID.Equals(plane.ID))
                {
                    DrawPlaneShadow(ctx, tplane);
                    o.Render(ctx);
                    DrawHealthBarClamped(ctx, tplane, new D2DPoint(tplane.Position.X, tplane.Position.Y - 110f), healthBarSize);
                    DrawMuzzleFlash(ctx, tplane);

                    // Draw circle around locked on plane.
                    if (plane.Radar.LockedObj != null && plane.Radar.LockedObj.ID.Equals(tplane.ID))
                        ctx.DrawEllipse(new D2DEllipse(tplane.Position, new D2DSize(80f, 80f)), World.HudColor, 4f);
                }
            });

            DrawPlaneShadow(ctx, plane);
            plane.Render(ctx);

            _objs.Explosions.ForEach(o => o.Render(ctx));

            DrawClouds(ctx);
            DrawPlaneCloudShadows(ctx);
            DrawLightingEffects(ctx, plane);
            DrawMuzzleFlash(ctx, plane);

            ctx.PopViewPort();
            ctx.Gfx.PopTransform();
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

            if (plane.Altitude > MAX_SHOW_ALT)
                return;

            var shadowPos = new D2DPoint(plane.Position.X, Y_POS);
            var shadowWidth = Helpers.Lerp(0, MAX_WIDTH, Helpers.Factor(MAX_SIZE_ALT, plane.Altitude));
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
            const float MUZZ_FLASH_RADIUS = 60f;
            if (_muzzleFlashBrush == null)
                _muzzleFlashBrush = ctx.Device.CreateRadialGradientBrush(D2DPoint.Zero, D2DPoint.Zero, MUZZ_FLASH_RADIUS, MUZZ_FLASH_RADIUS, new D2DGradientStop[] { new D2DGradientStop(1.4f, D2DColor.Transparent), new D2DGradientStop(0.2f, new D2DColor(0.3f, D2DColor.Orange)) });

            if (plane.FiringBurst && plane.NumBullets > 0 && plane.CurrentFrame % 10 == 0)
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

            var color1 = D2DColor.DarkGreen;
            var color2 = new D2DColor(1f, 0f, 0.29f, 0);
            using (var brush = ctx.Device.CreateLinearGradientBrush(new D2DPoint(plane.Position.X, 50f), new D2DPoint(plane.Position.X, 4000f), [new D2DGradientStop(0.2f, AddTimeOfDayColor(color1)), new D2DGradientStop(0.1f, AddTimeOfDayColor(color2))]))
            {
                // Draw the ground.
                ctx.Gfx.FillRectangle(new D2DRect(groundPos, new D2DSize(this.Width * World.ViewPortScaleMulti, (HEIGHT * 2f) / ctx.CurrentScale)), brush);
            }
        }

        private void DrawGroundImpacts(RenderContext ctx, FighterPlane plane)
        {
            if (_groundClipLayer == null)
                _groundClipLayer = ctx.Device.CreateLayer();

            var color1 = new D2DColor(1f, 0.56f, 0.32f, 0.18f);
            var color2 = new D2DColor(1f, 0.35f, 0.2f, 0.1f);
            var rect = new D2DRect(new D2DPoint(plane.Position.X, 2000f), new D2DSize(this.Width * World.ViewPortScaleMulti, 4000f));

            using (var clipGeo = ctx.Device.CreateRectangleGeometry(rect))
            {
                ctx.Gfx.PushLayer(_groundClipLayer, ctx.Viewport, clipGeo);

                foreach (var impact in _objs.GroundImpacts)
                {
                    ctx.FillEllipseSimple(impact, 15f, color1);
                    ctx.FillEllipseSimple(impact, 11f, color2);
                }

                ctx.Gfx.PopLayer();
            }
        }

        private void DrawGroundObjs(RenderContext ctx, FighterPlane plane)
        {
            foreach (var tree in _trees)
            {
                if (ctx.Viewport.Contains(tree.Position))
                {
                    tree.Render(ctx, GetTimeOfDayColor(), GROUND_OBJ_SCALE);
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
        //    Helpers.ApplyTranslation(housePoly, housePoly, 0f, pos, SCALE);
        //    Helpers.ApplyTranslation(roofPoly, roofPoly, 0f, pos , SCALE);


        //    ctx.DrawPolygon(housePoly, D2DColor.Gray, 1, D2DDashStyle.Solid, D2DColor.Gray);
        //    ctx.DrawPolygon(roofPoly, D2DColor.Gray, 1, D2DDashStyle.Solid, D2DColor.DarkRed);
        //}

        private void DrawHealthBarClamped(RenderContext ctx, FighterPlane plane, D2DPoint position, D2DSize size)
        {
            var healthPct = plane.Hits / (float)FighterPlane.MAX_HITS;
            ctx.FillRectangle(new D2DRect(position.X - (size.width * 0.5f), position.Y - (size.height * 0.5f), size.width * healthPct, size.height), World.HudColor);
            ctx.DrawRectangle(new D2DRect(position, size), World.HudColor);

            // Draw player name.
            if (string.IsNullOrEmpty(plane.PlayerName))
                return;

            var rect = new D2DRect(position + new D2DPoint(0, -40), new D2DSize(300, 100));
            ctx.DrawTextCenter(plane.PlayerName, World.HudColor, _defaultFontName, 30f, rect);
        }

        private void DrawHealthBar(D2DGraphics gfx, FighterPlane plane, D2DPoint position, D2DSize size)
        {
            var healthPct = plane.Hits / (float)FighterPlane.MAX_HITS;
            gfx.FillRectangle(new D2DRect(position.X - (size.width * 0.5f), position.Y - (size.height * 0.5f), size.width * healthPct, size.height), World.HudColor);
            gfx.DrawRectangle(new D2DRect(position, size), World.HudColor);

            // Draw ammo.
            gfx.DrawTextCenter($"MSL: {plane.NumMissiles}", World.HudColor, _defaultFontName, 15f, new D2DRect(position + new D2DPoint(-110f, 30f), new D2DSize(50f, 20f)));
            gfx.DrawTextCenter($"DECOY: {plane.NumDecoys}", World.HudColor, _defaultFontName, 15f, new D2DRect(position + new D2DPoint(0, 30f), new D2DSize(80f, 20f)));
            gfx.DrawTextCenter($"AMMO: {plane.NumBullets}", World.HudColor, _defaultFontName, 15f, new D2DRect(position + new D2DPoint(110f, 30f), new D2DSize(70f, 20f)));

            // Draw player name.
            if (string.IsNullOrEmpty(plane.PlayerName))
                return;

            var rect = new D2DRect(position + new D2DPoint(0, -40), new D2DSize(300, 100));
            gfx.DrawTextCenter(plane.PlayerName, World.HudColor, _defaultFontName, 30f, rect);
        }

        private void DrawHud(RenderContext ctx, D2DSize viewportsize, FighterPlane viewPlane)
        {
            ctx.Gfx.PushTransform();
            ctx.Gfx.ScaleTransform(_hudScale, _hudScale, new D2DPoint(viewportsize.width * 0.5f, viewportsize.height * 0.5f));

            DrawAltimeter(ctx.Gfx, viewportsize, viewPlane);
            DrawSpeedo(ctx.Gfx, viewportsize, viewPlane);

            if (!viewPlane.IsDamaged)
            {
                if (viewPlane.IsAI == false)
                {
                    DrawGuideIcon(ctx.Gfx, viewportsize, viewPlane);
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

            ctx.Gfx.PopTransform();
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
                var color = Helpers.LerpColor(World.HudColor, D2DColor.WhiteSmoke, 0.3f);

                switch (msg.Type)
                {
                    case EventType.Chat:
                        color = D2DColor.White;
                        break;
                }

                ctx.Gfx.DrawText(msg.Message, color, _defaultFontName, FONT_SIZE, rect);

                linePos += new D2DPoint(0, lineSize.height);
            }

            ctx.Gfx.DrawRectangle(boxPos.X - (WIDTH / 2f) - 10f, boxPos.Y - lineSize.height, WIDTH, HEIGHT + lineSize.height, World.HudColor);

            // Draw current chat message.
            if (chatActive)
            {
                var rect = new D2DRect(new D2DPoint(boxPos.X, boxPos.Y + HEIGHT + 6f), lineSize);

                var curText = _netMan.ChatInterface.CurrentText;

                if (string.IsNullOrEmpty(curText))
                    ctx.Gfx.DrawText("Type chat message...", World.HudColor, _defaultFontName, FONT_SIZE, rect);
                else
                    ctx.Gfx.DrawText(_netMan.ChatInterface.CurrentText, D2DColor.White, _defaultFontName, FONT_SIZE, rect);

                ctx.Gfx.DrawRectangle(boxPos.X - (WIDTH / 2f) - 10f, boxPos.Y + HEIGHT, WIDTH, lineSize.height + 5f, World.HudColor);
            }

            ctx.Gfx.PopTransform();
        }

        private void DrawGuideIcon(D2DGraphics gfx, D2DSize viewportsize, FighterPlane viewPlane)
        {
            const float DIST = 300f;
            var pos = new D2DPoint(viewportsize.width * 0.5f, viewportsize.height * 0.5f);

            var mouseAngle = viewPlane.PlayerGuideAngle;
            var mouseVec = Helpers.AngleToVectorDegrees(mouseAngle, DIST);
            gfx.DrawEllipse(new D2DEllipse(pos + mouseVec, new D2DSize(5f, 5f)), World.HudColor, 2f);

            var planeAngle = viewPlane.Rotation;
            var planeVec = Helpers.AngleToVectorDegrees(planeAngle, DIST);
            gfx.DrawCrosshair(pos + planeVec, 2f, World.HudColor, 5f, 20f);
        }

        private void DrawHudMessage(D2DGraphics gfx, D2DSize viewportsize)
        {
            const float FONT_SIZE = 40f;
            if (_hudMessageTimeout.IsRunning && !string.IsNullOrEmpty(_hudMessage))
            {
                var pos = new D2DPoint(viewportsize.width * 0.5f, 300f);
                var initSize = new D2DSize(400, 100);
                var size = gfx.MeasureText(_hudMessage, _defaultFontName, FONT_SIZE, initSize);
                var rect = new D2DRect(pos, size);

                gfx.FillRectangle(rect, D2DColor.Gray);
                gfx.DrawTextCenter(_hudMessage, _hudMessageColor, _defaultFontName, FONT_SIZE, rect);
            }

            if (!_hudMessageTimeout.IsRunning)
                _hudMessage = string.Empty;
        }

        private void DrawThrottle(D2DGraphics gfx, D2DSize viewportsize, FighterPlane plane)
        {
            const float W = 20f;
            const float H = 50f;
            const float xPos = 80f;
            const float yPos = 80f;


            var pos = new D2DPoint(xPos, (viewportsize.height * 0.5f) + yPos);
            //var pos = new D2DPoint(viewportsize.width * 0.1f, (viewportsize.height * 0.5f) + yPos);

            var rect = new D2DRect(pos, new D2DSize(W, H));

            gfx.PushTransform();

            gfx.DrawRectangle(rect, World.HudColor);
            gfx.DrawTextCenter("THR", World.HudColor, _defaultFontName, 15f, new D2DRect(pos + new D2DPoint(0, 40f), new D2DSize(50f, 20f)));

            var throtRect = new D2DRect(pos.X - (W * 0.5f), pos.Y - (H * 0.5f), W, (H * plane.ThrustAmount));
            gfx.RotateTransform(180f, pos);
            gfx.FillRectangle(throtRect, World.HudColor);

            gfx.PopTransform();
        }

        private void DrawStats(D2DGraphics gfx, D2DSize viewportsize, FighterPlane plane)
        {
            const float W = 20f;
            const float H = 50f;
            const float xPos = 110f;
            const float yPos = 110f;
            var pos = new D2DPoint(xPos, (viewportsize.height * 0.5f) + yPos);

            var rect = new D2DRect(pos, new D2DSize(W, H));


            //gfx.DrawTextCenter($"{plane.Hits}/{Plane.MAX_HITS}", World.HudColor, _defaultFontName, 15f, new D2DRect(pos + new D2DPoint(0, 40f), new D2DSize(50f, 20f)));
            gfx.DrawTextCenter($"MSL: {plane.NumMissiles}", World.HudColor, _defaultFontName, 15f, new D2DRect(pos + new D2DPoint(0, 70f), new D2DSize(50f, 20f)));
            gfx.DrawTextCenter($"AMMO: {plane.NumBullets}", World.HudColor, _defaultFontName, 15f, new D2DRect(pos + new D2DPoint(0, 100f), new D2DSize(70f, 20f)));
        }

        private void DrawGMeter(D2DGraphics gfx, D2DSize viewportsize, FighterPlane plane)
        {
            const float xPos = 80f;
            var pos = new D2DPoint(viewportsize.width * 0.17f + 50f, viewportsize.height * 0.50f);

            //var pos = new D2DPoint(xPos, viewportsize.height * 0.5f);
            var rect = new D2DRect(pos, new D2DSize(50, 20));

            gfx.DrawText($"G {Math.Round(plane.GForce, 1)}", World.HudColor, _defaultFontName, 15f, rect);
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
                    gfx.DrawTextCenter(altMarker.ToString(), World.HudColor, _defaultFontName, 15f, textRect);
                }
            }

            var actualRect = new D2DRect(new D2DPoint(pos.X, pos.Y + HalfH + 20f), new D2DSize(60f, 20f));
            gfx.DrawTextCenter(Math.Round(alt, 0).ToString(), World.HudColor, _defaultFontName, 15f, actualRect);
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
                    gfx.DrawTextCenter(altMarker.ToString(), World.HudColor, _defaultFontName, 15f, textRect);
                }
            }

            var speedRect = new D2DRect(new D2DPoint(pos.X, pos.Y + HalfH + 20f), new D2DSize(60f, 20f));
            gfx.DrawTextCenter(Math.Round(spd, 0).ToString(), World.HudColor, _defaultFontName, 15f, speedRect);

            var gforceRect = new D2DRect(new D2DPoint(pos.X, pos.Y - HalfH - 20f), new D2DSize(60f, 20f));
            gfx.DrawText($"G {Math.Round(plane.GForce, 1)}", World.HudColor, _defaultFontName, 15f, gforceRect);
        }

        private void DrawPlanePointers(RenderContext ctx, D2DSize viewportsize, FighterPlane plane)
        {
            const float MIN_DIST = 600f;
            const float MAX_DIST = 10000f;
            var pos = new D2DPoint(viewportsize.width * 0.5f, viewportsize.height * 0.5f);
            var color = Helpers.LerpColor(World.HudColor, D2DColor.WhiteSmoke, 0.3f);

            for (int i = 0; i < _objs.Planes.Count; i++)
            {
                var target = _objs.Planes[i];

                if (target == null)
                    continue;

                if (target.IsDamaged)
                    continue;

                var dist = D2DPoint.Distance(plane.Position, target.Position);

                if (dist < MIN_DIST || dist > MAX_DIST)
                    continue;

                var dir = target.Position - plane.Position;
                var angle = dir.Angle(true);
                var vec = Helpers.AngleToVectorDegrees(angle);

                ctx.Gfx.DrawArrow(pos + (vec * 250f), pos + (vec * 270f), color, 2f);


            }

            if (plane.Radar.HasLock)
            {
                var lockPos = pos + new D2DPoint(0f, -200f);
                var lRect = new D2DRect(lockPos, new D2DSize(120, 30));
                ctx.Gfx.DrawTextCenter("LOCKED", color, _defaultFontName, 25f, lRect);
            }
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
            const float MAX_DIST = 20000f;

            bool warningMessage = false;
            var pos = new D2DPoint(viewportsize.width * 0.5f, viewportsize.height * 0.5f);

            for (int i = 0; i < _objs.Missiles.Count; i++)
            {
                var missile = _objs.Missiles[i] as GuidedMissile;

                if (missile == null)
                    continue;

                if (missile.Owner.ID.Equals(plane.ID))
                    continue;

                if (missile.Target != null && !missile.Target.ID.Equals(plane.ID))
                    continue;

                var dist = D2DPoint.Distance(plane.Position, missile.Position);

                var dir = missile.Position - plane.Position;
                var angle = dir.Angle(true);
                var color = D2DColor.Red;
                var vec = Helpers.AngleToVectorDegrees(angle);
                var pos1 = pos + (vec * 200f);
                var pos2 = pos1 + (vec * 20f);
                var distFact = 1f - Helpers.Factor(dist, MIN_DIST * 10f);

                if (missile.IsDistracted)
                    color = D2DColor.Yellow;

                // Display warning if impact time is less than 10 seconds?
                const float MIN_IMPACT_TIME = 20f;
                if (MissileIsImpactThreat(plane, missile, MIN_IMPACT_TIME))
                    warningMessage = true;

                if (dist < MIN_DIST / 2f || dist > MAX_DIST)
                    continue;

                if (!missile.MissedTarget && warningMessage)
                    gfx.DrawArrow(pos1, pos2, color, (distFact * 30f) + 1f);
            }

            if (warningMessage)
            {
                var rect = new D2DRect(pos - new D2DPoint(0, -200), new D2DSize(120, 30));
                gfx.DrawTextCenter("MISSILE", D2DColor.Red, _defaultFontName, 30f, rect);
            }

            if (plane.HasRadarLock)
            {
                var lockRect = new D2DRect(pos - new D2DPoint(0, -160), new D2DSize(120, 30));
                gfx.DrawTextCenter("LOCK", D2DColor.Red, _defaultFontName, 30f, lockRect);
            }
        }

        private bool MissileIsImpactThreat(FighterPlane plane, Missile missile, float minImpactTime)
        {
            var navigationTime = Helpers.ImpactTime(plane, missile);

            // Is it going to hit soon and is actively targeting us?
            return (navigationTime < minImpactTime && missile.Target.ID.Equals(plane.ID));
        }


        private void DrawOverlays(RenderContext ctx, FighterPlane viewplane)
        {
            DrawInfo(ctx.Gfx, _infoPosition, viewplane);

            if (World.EnableTurbulence || World.EnableWind)
                DrawWindAndTurbulenceOverlay(ctx);

            if (viewplane.IsDamaged)
                ctx.Gfx.FillRectangle(World.ViewPortRect, new D2DColor(0.2f, D2DColor.Red));
        }

        private void DrawSky(RenderContext ctx, FighterPlane viewPlane)
        {
            const float MAX_ALT_OFFSET = 10000f;

            var plrAlt = viewPlane.Altitude;
            if (viewPlane.Position.Y >= 0)
                plrAlt = 0f;

            var color1 = new D2DColor(0.5f, D2DColor.SkyBlue);
            var color2 = new D2DColor(0.5f, D2DColor.Black);
            var color = Helpers.LerpColor(color1, color2, (plrAlt / (World.MAX_ALTITUDE - MAX_ALT_OFFSET)));

            // Add time of day color.
            color = Helpers.LerpColor(color, D2DColor.Black, Helpers.Factor(World.TimeOfDay, World.MAX_TIMEOFDAY - 5f));
            color = Helpers.LerpColor(color, AddTimeOfDayColor(color), 0.2f);

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

            for (float x = 0 - (spacing * 3f); x < this.Width + roundPos.X; x += spacing)
            {
                for (float y = 0 - (spacing * 3f); y < this.Height + roundPos.Y; y += spacing)
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
            const float DARKER_COLOR = 0.6f;
            var color1 = new D2DColor(1f, DARKER_COLOR, DARKER_COLOR, DARKER_COLOR);
            var color2 = D2DColor.WhiteSmoke;

            var points = cloud.Points;

            Helpers.ApplyTranslation(points, points, cloud.Position, 0f, D2DPoint.Zero, cloud.ScaleX, cloud.ScaleY);

            // Find min/max height.
            var minY = points.Min(p => p.Y);
            var maxY = points.Max(p => p.Y);

            for (int i = 0; i < points.Length; i++)
            {
                var point = points[i];
                var dims = cloud.Dims[i];

                // Lerp slightly darker colors to give the cloud some depth.

                //// Darken by number of clouds.
                //var amt = Helpers.Factor(i, (float)points.Length);
                //var color = Helpers.LerpColor(color1, color2, amt); 

                ////Darker clouds on top.
                //var amt = Helpers.Factor(point.Y, minY, maxY);
                //var color = Helpers.LerpColor(color1, color2, amt); 

                //Darker clouds on bottom.
                var amt = Helpers.Factor(point.Y, minY, maxY);
                var color = Helpers.LerpColor(color1, color2, 1f - amt);

                // Add time of day color.
                color = AddTimeOfDayColor(color);

                ctx.FillEllipse(new D2DEllipse(point, new D2DSize(dims.X, dims.Y)), color);
            }
        }

        private void DrawCloudShadow(RenderContext ctx, Cloud cloud)
        {
            if (cloud.Position.Y < -8000f)
                return;

            var todOffset = Helpers.Lerp(600f, -600f, Helpers.Factor(World.TimeOfDay, World.MAX_TIMEOFDAY));

            if (!ctx.Viewport.Contains(new D2DPoint(cloud.Position.X + todOffset, 0f)))
                return;

            var shadowColor = new D2DColor(0.05f, Helpers.LerpColor(GetTimeOfDayColor(), D2DColor.Black, 0.7f));

            for (int i = 0; i < cloud.Points.Length; i++)
            {
                var point = cloud.Points[i];
                var dims = cloud.Dims[i];

                var groundPos = new D2DPoint(point.X, -80f + Math.Abs((point.Y * 0.1f)));
                groundPos.X += todOffset;

                if (ctx.Viewport.Contains(groundPos))
                    ctx.FillEllipse(new D2DEllipse(groundPos, new D2DSize(dims.X * 4f, dims.Y * 0.5f)), shadowColor);
            }
        }

        private void MoveClouds(float dt)
        {
            const float RATE = 40f;
            for (int i = 0; i < _clouds.Count; i++)
            {
                var cloud = _clouds[i];

                var altFact = 30f * Helpers.Factor(Math.Abs(cloud.Position.Y), 30000f); // Higher clouds move slower?
                var sizeOffset = (cloud.Radius / 2f); // Smaller clouds move slightly faster?
                cloud.Position.X += ((RATE - altFact) - sizeOffset) * dt;

                float rotDir = 1f;

                // Fiddle rotation direction.
                if (cloud.Points.Length % 2 == 0)
                    rotDir = -1f;

                cloud.Rotation = Helpers.ClampAngle(cloud.Rotation + (0.8f * rotDir) * dt);

                // Wrap clouds.
                if (cloud.Position.X > MAX_CLOUD_X)
                {
                    cloud.Position.X = -MAX_CLOUD_X;
                }

                Helpers.ApplyTranslation(cloud.PointsOrigin, cloud.Points, cloud.Rotation, cloud.Position, CLOUD_SCALE);
            }
        }

        private void DrawWindAndTurbulenceOverlay(RenderContext ctx)
        {
            var pos = new D2DPoint(this.Width - 100f, 100f);

            ctx.FillEllipse(new D2DEllipse(pos, new D2DSize(World.AirDensity * 10f, World.AirDensity * 10f)), D2DColor.SkyBlue);

            ctx.DrawLine(pos, pos + (World.Wind * 2f), D2DColor.White, 2f);
        }

        private void DrawNearObj(D2DGraphics gfx, FighterPlane plane)
        {
            //_targets.ForEach(t =>
            //{
            //    if (t.IsObjNear(plane))
            //        gfx.FillEllipseSimple(t.Position, 5f, D2DColor.Red);

            //});

            _objs.Bullets.ForEach(b =>
            {
                if (b.IsObjNear(plane))
                    gfx.FillEllipseSimple(b.Position, 5f, D2DColor.Red);

            });

            _objs.Missiles.ForEach(m =>
            {
                if (m.IsObjNear(plane))
                    gfx.FillEllipseSimple(m.Position, 5f, D2DColor.Red);

            });

            _objs.Decoys.ForEach(d =>
            {
                if (d.IsObjNear(plane))
                    gfx.FillEllipseSimple(d.Position, 5f, D2DColor.Red);

            });
        }


        public void DrawInfo(D2DGraphics gfx, D2DPoint pos, FighterPlane viewplane)
        {
            var infoText = GetInfo(viewplane);

            if (_showHelp)
            {
                infoText += "\nH: Hide help\n";

                if (!World.IsNetGame)
                {
                    infoText += $"P: Pause\n";
                    infoText += $"U: Spawn AI Plane\n";
                }

                infoText += $"Y: Start Chat Message\n";
                infoText += $"(+/-): Zoom\n";
                infoText += $"Shift + (+/-): HUD Scale\n";
                infoText += $"Left-Click: Fire Bullets\n";
                infoText += $"Right-Click: Drop Decoys\n";
                infoText += $"Middle-Click/Space Bar: Fire Missile\n";

                infoText += $"\nSpectate (While crashed)\n";
                infoText += $"([/]): Prev/Next Spectate Plane\n";
                infoText += $"Backspace: Reset Spectate\n";
            }
            else
            {
                infoText += "\n";
                infoText += "H: Show help";
            }

            gfx.DrawText(infoText, D2DColor.GreenYellow, _defaultFontName, 12f, pos.X, pos.Y);
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
                infoText += $"Planes: {_objs.Planes.Count(p => !p.IsDamaged && !p.HasCrashed)}\n";
                infoText += $"Update ms: {Math.Round(UpdateTime.TotalMilliseconds, 2)}\n";
                infoText += $"Render ms: {Math.Round(_renderTimeSmooth.Current, 2)}\n";
                infoText += $"Collision ms: {Math.Round(CollisionTime.TotalMilliseconds, 2)}\n";

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
                infoText += $"TimeOfDay: {World.TimeOfDay.ToString()}\n";
                infoText += $"VP: {this.Width}, {this.Height}\n";
                infoText += $"DPI: {this._renderTarget.DeviceDpi}\n";

            }

            return infoText;
        }

    }
}
