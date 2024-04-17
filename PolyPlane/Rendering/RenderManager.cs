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

        private readonly D2DColor _hudColor = new D2DColor(0.3f, D2DColor.GreenYellow);
        private readonly D2DPoint _infoPosition = new D2DPoint(20, 20);

        private bool _showInfo = false;

        private TimeSpan _renderTime = new TimeSpan();
        private Stopwatch _timer = new Stopwatch();
        private float _renderFPS = 0;
        private long _lastRenderTime = 0;
        private string _hudMessage = string.Empty;
        private D2DColor _hudMessageColor = D2DColor.Red;
        private GameTimer _hudMessageTimeout = new GameTimer(5f);
        private List<EventMessage> _messageEvents = new List<EventMessage>();

        private readonly string _defaultFontName = "Consolas";

        private Control _renderTarget;
        private readonly D2DColor _clearColor = D2DColor.Black;
        private GameObjectManager _objs;
        private NetEventManager _netMan;

        private D2DPoint _screenShakeTrans = D2DPoint.Zero;
        private D2DPoint _gforceTrans = D2DPoint.Zero;

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

        const int PROC_GEN_LEN = 20000;
        private int[] _groundObjsRnd; // Random data points sampled for ground objects.

        private const int NUM_CLOUDS = 2000;
        private const float MAX_CLOUD_Y = 400000f;
        private List<Cloud> _clouds = new List<Cloud>();

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

            InitProceduralGenStuff();
            InitGfx();
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

            _groundObjsRnd = new int[PROC_GEN_LEN];

            // Random values for ground obj gen.
            for (int i = 0; i < PROC_GEN_LEN; i++)
                _groundObjsRnd[i] = rnd.Next(PROC_GEN_LEN);

            // Generate a pseudo-random? list of clouds.
            // I tried to do clouds procedurally, but wasn't having much luck.
            // It turns out that we need a surprisingly few number of clouds
            // to cover a very large area, so we will just brute force this for now.
            var cloudRangeX = new D2DPoint(-MAX_CLOUD_Y, MAX_CLOUD_Y);
            var cloudRangeY = new D2DPoint(-30000, -2000);
            var cloudDeDup = new HashSet<D2DPoint>();
            const int MIN_PNTS = 5;
            const int MAX_PNTS = 25;
            const int MIN_RADIUS = 5;
            const int MAX_RADIUS = 20;

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

        public void RenderFrame(FighterPlane viewplane)
        {
            ResizeGfx();

            _renderTime = TimeSpan.Zero;
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


                DrawPlaneAndObjects(_ctx, viewplane);

                _gfx.PopTransform(); // Pop scale transform.

                //_gfx.PushTransform(); // Push GForce transform.
                //_gfx.TranslateTransform(_gforceTrans.X, _gforceTrans.Y);

                var hudVPSize = new D2DSize(this.Width, this.Height);
                DrawHud(_ctx, hudVPSize, viewplane);

                _gfx.PopTransform(); // Pop screen shake transform.
                //_gfx.PopTransform(); // Pop GForce transform.

                _timer.Stop();
                _renderTime = _timer.Elapsed;

                DrawOverlays(_ctx, viewplane);

                if (viewplane.GForce > 17f)
                    DoScreenShake(viewplane.GForce / 10f);

                DrawScreenFlash(_gfx);

                //_gforceTrans = -Helpers.AngleToVectorDegrees(viewplane.GForceDirection, viewplane.GForce);
            }

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
            var todColor = InterpolateColorGaussian(_todPallet, World.TimeOfDay, World.MAX_TIMEOFDAY);
            return Helpers.LerpColor(color, todColor, 0.3f);
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
            viewPortRect = viewPortRect.Inflate(500f, 500f); // Inflate slightly to prevent "pop-in".
            ctx.PushViewPort(viewPortRect);

            DrawGroundObjs(ctx, plane);
            DrawGround(ctx, plane);
            DrawGroundImpacts(ctx, plane);

            _objs.Decoys.ForEach(o => o.Render(ctx));
            _objs.Missiles.ForEach(o => o.Render(ctx));
            _objs.MissileTrails.ForEach(o => o.Render(ctx));
            _objs.Bullets.ForEach(o => o.Render(ctx));

            _objs.Planes.ForEach(o =>
            {
                if (o is FighterPlane tplane && !tplane.ID.Equals(plane.ID))
                {
                    o.Render(ctx);
                    //ctx.Gfx.DrawEllipse(new D2DEllipse(tplane.Position, new D2DSize(80f, 80f)), _hudColor, 2f);

                    DrawHealthBarClamped(ctx, tplane, new D2DPoint(tplane.Position.X, tplane.Position.Y - 110f), healthBarSize);
                }
            });

            plane.Render(ctx);

            _objs.Explosions.ForEach(o => o.Render(ctx));

            DrawClouds(ctx);

            ctx.PopViewPort();
            ctx.Gfx.PopTransform();
        }

        private void DrawGround(RenderContext ctx, FighterPlane plane)
        {
            var groundPos = new D2DPoint(plane.Position.X, 0f);

            if (!ctx.Viewport.Contains(groundPos))
                return;

            groundPos += new D2DPoint(0f, 2000f);
            var color1 = D2DColor.DarkGreen;
            var color2 = new D2DColor(1f, 0f, 0.29f, 0);
            using (var brush = ctx.Device.CreateLinearGradientBrush(new D2DPoint(plane.Position.X, 50f), new D2DPoint(plane.Position.X, 4000f), [new D2DGradientStop(0.2f, AddTimeOfDayColor(color1)), new D2DGradientStop(0.1f, AddTimeOfDayColor(color2))]))
            {
                // Draw the ground.
                ctx.Gfx.FillRectangle(new D2DRect(groundPos, new D2DSize(this.Width * World.ViewPortScaleMulti, 4000f)), brush);
            }

            //// Draw the ground.
            //var groundColor = D2DColor.DarkGreen;
            //groundColor = AddToDColor(groundColor);// Add time of day color
            //ctx.Gfx.FillRectangle(new D2DRect(new D2DPoint(plane.Position.X, 2000f), new D2DSize(this.Width * World.ViewPortScaleMulti, 4000f)), groundColor);
        }

        private void DrawGroundObjs(RenderContext ctx, FighterPlane plane)
        {
            var start = plane.Position.X - ((this.Width * World.ViewPortScaleMulti) * 0.5f);
            var end = plane.Position.X + ((this.Width * World.ViewPortScaleMulti) * 0.5f);

            for (int x = (int)start; x < (int)end; x += 1)
            {
                var xRound = (Math.Abs(x) % PROC_GEN_LEN);

                // ????
                var rndPnt = _groundObjsRnd[(xRound / 1)];
                var rndPnt2 = _groundObjsRnd[(xRound / 6)];
                var rndPnt3 = _groundObjsRnd[(xRound / 7)];

                var treePos = new D2DPoint(x, 0f);

                // Just fiddling with numbers here to produce a decent looking result... 
                if (rndPnt == 10)
                    DrawTree(ctx, treePos + new D2DPoint(rndPnt * 200, 0f), 20, 40 + rndPnt);

                if (rndPnt == 11)
                    DrawTree(ctx, treePos + new D2DPoint(rndPnt * 20, 0f), ((rndPnt) * 6) + 20, 60 + rndPnt);

                if (rndPnt == 13)
                    DrawPineTree(ctx, treePos + new D2DPoint((21 - rndPnt) * 28, 0f), 20f);

                if (rndPnt == 15)
                    DrawTree(ctx, treePos + new D2DPoint(400, 0), 30f, 71f);

                if (rndPnt == 16)
                    DrawTree(ctx, treePos, 60f);

                if (rndPnt == 18)
                    DrawPineTree(ctx, treePos + new D2DPoint((rndPnt) * 25, 0f), 40f, 20f);

                if (rndPnt == 1010)
                    DrawTree(ctx, treePos, 40f, 81f);

                if (rndPnt == 1011)
                    DrawTree(ctx, treePos, 100f, 72f);

                if (rndPnt2 == 10235)
                    DrawTree(ctx, treePos, 60f, 81f);

                if (rndPnt3 == 6134)
                    DrawPineTree(ctx, treePos + new D2DPoint((rndPnt) * 25, 0f), 60f, 21f + ((rndPnt % 20) * 2f));

                if (rndPnt == 14562)
                    DrawPineTree(ctx, treePos - new D2DPoint(0, 0f), 40f, 33f);
            }
        }

        private void DrawTree(RenderContext ctx, D2DPoint pos, float height = 20f, float radius = 50f)
        {
            if (!ctx.Viewport.Contains(pos))
                return;

            var trunk = new D2DPoint[]
            {
                new D2DPoint(-2, 0),
                new D2DPoint(2, 0),
                new D2DPoint(0, height),
            };

            var scale = 5f;
            var trunkColor = D2DColor.Chocolate;
            var leafColor = D2DColor.ForestGreen;

            // Add time of day color
            trunkColor = AddTimeOfDayColor(trunkColor);
            leafColor = AddTimeOfDayColor(leafColor);

            if (radius % 2 == 0)
                leafColor.g -= 0.1f;

            Helpers.ApplyTranslation(trunk, trunk, 180f, pos, scale);

            var leafPos = pos + new D2DPoint(0, -height * scale);
            ctx.DrawPolygon(trunk, trunkColor, 1f, D2DDashStyle.Solid, trunkColor);

            using (var brush = ctx.Device.CreateRadialGradientBrush(leafPos, D2DPoint.Zero, radius, radius, [new D2DGradientStop(0f, leafColor), new D2DGradientStop(1f, Helpers.LerpColor(leafColor, D2DColor.Black, 0.1f))]))
            {
                ctx.FillEllipse(new D2DEllipse(leafPos, new D2DSize(radius, radius)), brush);
            }

            //ctx.FillEllipse(new D2DEllipse(leafPos, new D2DSize(radius, radius)), leafColor);
        }

        private void DrawPineTree(RenderContext ctx, D2DPoint pos, float height = 20f, float width = 20f)
        {
            if (!ctx.Viewport.Contains(pos))
                return;

            var pineTop = new D2DPoint[]
            {
                new D2DPoint(-(width / 2f), 0),
                new D2DPoint((width / 2f), 0),
                new D2DPoint(0, height),
            };

            var scale = 5f;
            var trunkColor = D2DColor.BurlyWood;
            var leafColor = D2DColor.Green;

            // Add time of day color
            trunkColor = AddTimeOfDayColor(trunkColor);
            leafColor = AddTimeOfDayColor(leafColor);

            Helpers.ApplyTranslation(pineTop, pineTop, 180f, pos - new D2DPoint(0, height), scale);

            ctx.FillRectangle(new D2DRect(pos - new D2DPoint(0, height / 2f), new D2DSize(width / 2f, height * 1f)), trunkColor);
            ctx.DrawPolygon(pineTop, leafColor, 1f, D2DDashStyle.Solid, leafColor);
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

        private void DrawHealthBarClamped(RenderContext ctx, FighterPlane plane, D2DPoint position, D2DSize size)
        {
            var healthPct = plane.Hits / (float)FighterPlane.MAX_HITS;
            ctx.FillRectangle(new D2DRect(position.X - (size.width * 0.5f), position.Y - (size.height * 0.5f), size.width * healthPct, size.height), _hudColor);
            ctx.DrawRectangle(new D2DRect(position, size), _hudColor);

            // Draw player name.
            if (string.IsNullOrEmpty(plane.PlayerName))
                return;

            var rect = new D2DRect(position + new D2DPoint(0, -40), new D2DSize(300, 100));
            ctx.DrawTextCenter(plane.PlayerName, _hudColor, _defaultFontName, 30f, rect);
        }

        private void DrawHealthBar(D2DGraphics gfx, FighterPlane plane, D2DPoint position, D2DSize size)
        {
            var healthPct = plane.Hits / (float)FighterPlane.MAX_HITS;
            gfx.FillRectangle(new D2DRect(position.X - (size.width * 0.5f), position.Y - (size.height * 0.5f), size.width * healthPct, size.height), _hudColor);
            gfx.DrawRectangle(new D2DRect(position, size), _hudColor);

            // Draw ammo.
            gfx.DrawTextCenter($"MSL: {plane.NumMissiles}", _hudColor, _defaultFontName, 15f, new D2DRect(position + new D2DPoint(-110f, 30f), new D2DSize(50f, 20f)));
            gfx.DrawTextCenter($"DECOY: {plane.NumDecoys}", _hudColor, _defaultFontName, 15f, new D2DRect(position + new D2DPoint(0, 30f), new D2DSize(80f, 20f)));
            gfx.DrawTextCenter($"AMMO: {plane.NumBullets}", _hudColor, _defaultFontName, 15f, new D2DRect(position + new D2DPoint(110f, 30f), new D2DSize(70f, 20f)));

            // Draw player name.
            if (string.IsNullOrEmpty(plane.PlayerName))
                return;

            var rect = new D2DRect(position + new D2DPoint(0, -40), new D2DSize(300, 100));
            gfx.DrawTextCenter(plane.PlayerName, _hudColor, _defaultFontName, 30f, rect);
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

                DrawPlanePointers(ctx.Gfx, viewportsize, viewPlane);
                DrawMissilePointers(ctx.Gfx, viewportsize, viewPlane);
            }

            DrawHudMessage(ctx.Gfx, viewportsize);
            DrawRadar(ctx, viewportsize, viewPlane);

            var healthBarSize = new D2DSize(300, 30);
            var pos = new D2DPoint(viewportsize.width * 0.5f, viewportsize.height - (viewportsize.height * 0.85f));
            DrawHealthBar(ctx.Gfx, viewPlane, pos, healthBarSize);

            DrawMessages(ctx.Gfx, viewportsize, viewPlane);

            ctx.Gfx.PopTransform();
        }

        private void DrawMessages(D2DGraphics gfx, D2DSize viewportsize, FighterPlane plane)
        {
            const float FONT_SIZE = 10f;
            const int MAX_LINES = 10;
            const float WIDTH = 400f;
            const float HEIGHT = 100f;

            var lineSize = new D2DSize(WIDTH, HEIGHT / MAX_LINES);
            var boxPos = new D2DPoint(viewportsize.width * 0.25f, viewportsize.height * 0.75f);
            var linePos = boxPos;

            var start = 0;

            if (_messageEvents.Count >= MAX_LINES)
                start = _messageEvents.Count - MAX_LINES;

            for (int i = start; i < _messageEvents.Count; i++)
            {
                var msg = _messageEvents[i];
                var rect = new D2DRect(linePos, lineSize);

                gfx.DrawText(msg.Message, _hudColor, _defaultFontName, FONT_SIZE, rect);

                linePos += new D2DPoint(0, lineSize.height);
            }

            gfx.DrawRectangle(boxPos.X - (WIDTH / 2f) - 10f, boxPos.Y - lineSize.height, WIDTH, HEIGHT + lineSize.height, _hudColor);
        }

        private void DrawGuideIcon(D2DGraphics gfx, D2DSize viewportsize, FighterPlane viewPlane)
        {
            const float DIST = 300f;
            var pos = new D2DPoint(viewportsize.width * 0.5f, viewportsize.height * 0.5f);

            var mouseAngle = viewPlane.PlayerGuideAngle;
            var mouseVec = Helpers.AngleToVectorDegrees(mouseAngle, DIST);
            gfx.DrawEllipse(new D2DEllipse(pos + mouseVec, new D2DSize(5f, 5f)), _hudColor, 2f);

            var planeAngle = viewPlane.Rotation;
            var planeVec = Helpers.AngleToVectorDegrees(planeAngle, DIST);
            gfx.DrawCrosshair(pos + planeVec, 2f, _hudColor, 5f, 20f);
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

            gfx.DrawRectangle(rect, _hudColor);
            gfx.DrawTextCenter("THR", _hudColor, _defaultFontName, 15f, new D2DRect(pos + new D2DPoint(0, 40f), new D2DSize(50f, 20f)));

            var throtRect = new D2DRect(pos.X - (W * 0.5f), pos.Y - (H * 0.5f), W, (H * plane.ThrustAmount));
            gfx.RotateTransform(180f, pos);
            gfx.FillRectangle(throtRect, _hudColor);

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


            //gfx.DrawTextCenter($"{plane.Hits}/{Plane.MAX_HITS}", _hudColor, _defaultFontName, 15f, new D2DRect(pos + new D2DPoint(0, 40f), new D2DSize(50f, 20f)));
            gfx.DrawTextCenter($"MSL: {plane.NumMissiles}", _hudColor, _defaultFontName, 15f, new D2DRect(pos + new D2DPoint(0, 70f), new D2DSize(50f, 20f)));
            gfx.DrawTextCenter($"AMMO: {plane.NumBullets}", _hudColor, _defaultFontName, 15f, new D2DRect(pos + new D2DPoint(0, 100f), new D2DSize(70f, 20f)));
        }

        private void DrawGMeter(D2DGraphics gfx, D2DSize viewportsize, FighterPlane plane)
        {
            const float xPos = 80f;
            var pos = new D2DPoint(viewportsize.width * 0.17f + 50f, viewportsize.height * 0.50f);

            //var pos = new D2DPoint(xPos, viewportsize.height * 0.5f);
            var rect = new D2DRect(pos, new D2DSize(50, 20));

            gfx.DrawText($"G {Math.Round(plane.GForce, 1)}", _hudColor, _defaultFontName, 15f, rect);
        }

        private void DrawAltimeter(D2DGraphics gfx, D2DSize viewportsize, FighterPlane plane)
        {
            const float MIN_ALT = 3000f;
            const float W = 80f;
            const float H = 350f;
            const float HalfW = W * 0.5f;
            const float HalfH = H * 0.5f;
            const float MARKER_STEP = 100f;
            
            var pos = new D2DPoint(viewportsize.width * 0.85f, viewportsize.height * 0.3f);
            var rect = new D2DRect(pos, new D2DSize(W, H));
            var alt = plane.Altitude;
            var startAlt = alt - (alt % MARKER_STEP) + MARKER_STEP;
            var altWarningColor = new D2DColor(0.2f, D2DColor.Red);

            var highestAlt = startAlt + MARKER_STEP;
            var lowestAlt = startAlt - (MARKER_STEP * 2f);

            gfx.DrawRectangle(rect, _hudColor);
            gfx.DrawLine(new D2DPoint(pos.X - HalfW, pos.Y), new D2DPoint(pos.X + HalfW, pos.Y), D2DColor.GreenYellow, 1f, D2DDashStyle.Solid);

            if (highestAlt <= MIN_ALT || lowestAlt <= MIN_ALT)
            {
                var s = new D2DPoint(pos.X - HalfW, (pos.Y + (alt - MIN_ALT)));

                if (s.Y < pos.Y - HalfH)
                    s.Y = pos.Y - HalfH;

                gfx.FillRectangle(new D2DRect(s.X, s.Y, W, (pos.Y + (H * 0.5f)) - s.Y), altWarningColor);
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

                    gfx.DrawLine(start, end, _hudColor, 1f, D2DDashStyle.Dash);
                    var textRect = new D2DRect(start - new D2DPoint(25f, 0f), new D2DSize(60f, 30f));
                    gfx.DrawTextCenter(altMarker.ToString(), _hudColor, _defaultFontName, 15f, textRect);
                }
            }

            var actualRect = new D2DRect(new D2DPoint(pos.X, pos.Y + HalfH + 20f), new D2DSize(60f, 20f));
            gfx.DrawTextCenter(Math.Round(alt, 0).ToString(), _hudColor, _defaultFontName, 15f, actualRect);
        }


        private void DrawSpeedo(D2DGraphics gfx, D2DSize viewportsize, FighterPlane plane)
        {
            const float W = 80f;
            const float H = 350f;
            const float HalfW = W * 0.5f;
            const float HalfH = H * 0.5f;
            const float MARKER_STEP = 50f;

            var pos = new D2DPoint(viewportsize.width * 0.15f, viewportsize.height * 0.3f);
            var rect = new D2DRect(pos, new D2DSize(W, H));
            var spd = plane.Velocity.Length();
            var startSpd = (spd) - (spd % (MARKER_STEP)) + MARKER_STEP;

            gfx.DrawRectangle(rect, _hudColor);
            gfx.DrawLine(new D2DPoint(pos.X - HalfW, pos.Y), new D2DPoint(pos.X + HalfW, pos.Y), D2DColor.GreenYellow, 1f, D2DDashStyle.Solid);

            for (float y = 0; y < H; y += MARKER_STEP)
            {
                if (y % MARKER_STEP == 0)
                {
                    var start = new D2DPoint(pos.X - HalfW, (pos.Y - y + HalfH - MARKER_STEP) + (spd % MARKER_STEP));
                    var end = new D2DPoint(pos.X + HalfW, (pos.Y - y + HalfH - MARKER_STEP) + (spd % MARKER_STEP));

                    var div = y / MARKER_STEP;
                    var altMarker = startSpd + (-HalfH + (div * MARKER_STEP));

                    gfx.DrawLine(start, end, _hudColor, 1f, D2DDashStyle.Dash);
                    var textRect = new D2DRect(start - new D2DPoint(25f, 0f), new D2DSize(60f, 30f));
                    gfx.DrawTextCenter(altMarker.ToString(), _hudColor, _defaultFontName, 15f, textRect);
                }
            }

            var speedRect = new D2DRect(new D2DPoint(pos.X, pos.Y + HalfH + 20f), new D2DSize(60f, 20f));
            gfx.DrawTextCenter(Math.Round(spd, 0).ToString(), _hudColor, _defaultFontName, 15f, speedRect);

            var gforceRect = new D2DRect(new D2DPoint(pos.X, pos.Y - HalfH - 20f), new D2DSize(60f, 20f));
            gfx.DrawText($"G {Math.Round(plane.GForce, 1)}", _hudColor, _defaultFontName, 15f, gforceRect);
        }

        private void DrawPlanePointers(D2DGraphics gfx, D2DSize viewportsize, FighterPlane plane)
        {
            const float MIN_DIST = 600f;
            const float MAX_DIST = 10000f;
            var pos = new D2DPoint(viewportsize.width * 0.5f, viewportsize.height * 0.5f);

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

                gfx.DrawArrow(pos + (vec * 250f), pos + (vec * 270f), _hudColor, 2f);
            }

            if (plane.Radar.HasLock)
            {
                var lockPos = pos + new D2DPoint(0f, -200f);
                var lRect = new D2DRect(lockPos, new D2DSize(120, 30));
                gfx.DrawTextCenter("LOCKED", _hudColor, _defaultFontName, 25f, lRect);

            }
        }

        private void DrawRadar(RenderContext ctx, D2DSize viewportsize, FighterPlane plane)
        {
            const float SCALE = 0.8f;
            var pos = new D2DPoint(viewportsize.width * 0.8f, viewportsize.height * 0.75f);

            ctx.Gfx.PushTransform();
            ctx.Gfx.ScaleTransform(SCALE, SCALE, pos);

            plane.Radar.Position = pos;
            plane.Radar.Render(ctx);

            ctx.Gfx.PopTransform();
        }

        private void DrawMissilePointers(D2DGraphics gfx, D2DSize viewportsize, FighterPlane plane)
        {
            const float MIN_DIST = 3000f;
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

                if (!missile.MissedTarget)
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
            var closingRate = plane.ClosingRate(missile);

            // Is it going to hit soon, and has positive closing rate and is actively targeting us?
            return (navigationTime < minImpactTime && closingRate > 0f && missile.Target == plane);
        }

        public void DrawInfo(D2DGraphics gfx, D2DPoint pos, FighterPlane viewplane)
        {
            var infoText = GetInfo(viewplane);

            //if (_showHelp)
            //{
            //    infoText += $@"
            //H: Hide help

            //P: Pause
            //B: Motion Blur
            //T: Trails
            //N: Pause/One Step
            //R: Spawn Target
            //A: Spawn target at click pos
            //M: Move ship to click pos
            //C: Clear all
            //I: Toggle Aero Display
            //O: Toggle Missile View
            //U: Toggle Guidance Tracking Dots
            //S: Toggle Missile Type
            //Y: Cycle Target Types
            //K: Toggle Turbulence
            //L: Toggle Wind
            //+/-: Zoom
            //Shift + (+/-): Change Delta Time
            //S: Missile Type
            //Shift + Mouse-Wheel or E: Guidance Type
            //Left-Click: Thrust ship
            //Right-Click: Fire auto cannon
            //Middle-Click or Enter: Fire missile (Hold Shift to fire all types)
            //Mouse-Wheel: Rotate ship";
            //}
            //else
            //{
            //    infoText += "\n";
            //    infoText += "H: Show help";
            //}

            gfx.DrawText(infoText, D2DColor.GreenYellow, _defaultFontName, 12f, pos.X, pos.Y);
        }

        private void DrawOverlays(RenderContext ctx, FighterPlane viewplane)
        {
            if (_showInfo)
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
            //color = Helpers.LerpColor(color, D2DColor.Black, World.TimeOfDay / World.MAX_TIMEOFDAY);
            color = Helpers.LerpColor(color, D2DColor.Black, World.TimeOfDay / (World.MAX_TIMEOFDAY / 2f));

            var rect = new D2DRect(new D2DPoint(this.Width * 0.5f, this.Height * 0.5f), new D2DSize(this.Width, this.Height));

            ctx.Gfx.FillRectangle(rect, color);
        }

        private void DrawMovingBackground(RenderContext ctx, FighterPlane viewPlane)
        {
            float spacing = 75f;
            const float size = 4f;
            var d2dSz = new D2DSize(size, size);
            var color = new D2DColor(0.4f, D2DColor.Gray);

            var plrPos = viewPlane.Position;
            plrPos /= World.ViewPortScaleMulti;
            var roundPos = new D2DPoint((plrPos.X) % spacing, (plrPos.Y) % spacing);
            roundPos *= 4f;

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
            }
        }

        private void DrawCloud(RenderContext ctx, Cloud cloud)
        {
            const float SCALE = 5f;
            const float DARKER_COLOR = 0.6f;
            var color1 = new D2DColor(1f, DARKER_COLOR, DARKER_COLOR, DARKER_COLOR);
            var color2 = D2DColor.WhiteSmoke;

            var points = cloud.Points;
            Helpers.ApplyTranslation(cloud.PointsOrigin, cloud.Points, cloud.Rotation, cloud.Position, SCALE);

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

        private void MoveClouds(float dt)
        {
            const float RATE = 40f;
            for (int i = 0; i < _clouds.Count; i++)
            {
                var cloud = _clouds[i];

                // Smaller clouds move slightly faster?
                cloud.Position.X += (RATE - (cloud.Radius / 2)) * dt;

                float rotDir = 1f;

                // Fiddle rotation direction.
                if (cloud.Points.Length % 2 == 0)
                    rotDir = -1f;

                cloud.Rotation = Helpers.ClampAngle(cloud.Rotation + (3f * rotDir) * dt);

                // Wrap clouds.
                if (cloud.Position.X > MAX_CLOUD_Y)
                {
                    cloud.Position.X = -MAX_CLOUD_Y;
                }
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


        private string GetInfo(FighterPlane viewplane)
        {
            //var viewPlane = GetViewPlane();

            string infoText = string.Empty;
            //infoText += $"Paused: {_isPaused}\n\n";


            var numObj = _objs.TotalObjects;
            infoText += $"Num Objects: {numObj}\n";
            infoText += $"On Screen: {GraphicsExtensions.OnScreen}\n";
            infoText += $"Off Screen: {GraphicsExtensions.OffScreen}\n";
            infoText += $"Planes: {_objs.Planes.Count(p => !p.IsDamaged && !p.HasCrashed)}\n";


            infoText += $"FPS: {Math.Round(_renderFPS, 0)}\n";
            //infoText += $"Update ms: {_updateTime.TotalMilliseconds}\n";
            infoText += $"Render ms: {_renderTime.TotalMilliseconds}\n";
            infoText += $"Collision ms: {CollisionTime.TotalMilliseconds}\n";

            if (_netMan != null)
            {
                infoText += $"Packet Delay: {_netMan.PacketDelay}\n";
                infoText += $"Latency: {_netMan.Host.GetPlayerRTT(0)}\n";
                infoText += $"Packet Loss: {_netMan.Host.PacketLoss()}\n";
            }

            infoText += $"Zoom: {Math.Round(World.ZoomScale, 2)}\n";
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



            return infoText;
        }

    }
}
