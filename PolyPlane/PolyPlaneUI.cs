using PolyPlane.GameObjects;
using System.Collections.Concurrent;
using System.Diagnostics;


using unvell.D2DLib;

namespace PolyPlane
{
    public partial class PolyPlaneUI : Form
    {
        private D2DDevice _device;
        private D2DGraphics _gfx;
        private RenderContext _ctx;
        private Thread _renderThread;

        private const float ROTATE_RATE = 2f;
        private const float DT_ADJ_AMT = 0.00025f;
        private const float VIEW_SCALE = 4f;

        private ManualResetEventSlim _pauseRenderEvent = new ManualResetEventSlim(true);
        private ManualResetEventSlim _stopRenderEvent = new ManualResetEventSlim(true);

        private bool _isPaused = false;
        private bool _trailsOn = false;
        private bool _oneStep = false;
        private bool _moveShip = false;
        private bool _spawnTargetKey = false;
        private bool _killRender = false;
        private bool _fireBurst = false;
        private bool _motionBlur = false;
        private bool _shiftDown = false;
        private bool _renderEveryStep = false;
        private bool _showHelp = false;
        private float _aiTime = 0f;
        private float _nextAITime = 0f;
        private bool _godMode = false;
        private int _playerScore = 0;
        private int _playerDeaths = 0;

        private long _lastRenderTime = 0;
        private float _renderFPS = 0;

        private List<GameObject> _missiles = new List<GameObject>();
        private List<SmokeTrail> _missileTrails = new List<SmokeTrail>();
        private List<GameObject> _targets = new List<GameObject>();
        private List<GameObject> _bullets = new List<GameObject>();
        private List<GameObject> _explosions = new List<GameObject>();
        private List<GameObject> _flames = new List<GameObject>();
        private List<Plane> _aiPlanes = new List<Plane>();

        private ConcurrentQueue<GameObject> _newTargets = new ConcurrentQueue<GameObject>();
        private ConcurrentQueue<GameObject> _newMissiles = new ConcurrentQueue<GameObject>();
        private ConcurrentQueue<Plane> _newAIPlanes = new ConcurrentQueue<Plane>();

        private Plane _playerPlane;
        private D2DPoint _playerPlaneSlewPos = D2DPoint.Zero;
        private bool _slewEnable = false;

        private GuidanceType _guidanceType = GuidanceType.Advanced;
        private InterceptorTypes _interceptorType = InterceptorTypes.ControlSurfaceWithThrustVectoring;
        private TargetTypes _targetTypes = TargetTypes.Random;

        private readonly D2DColor _blurColor = new D2DColor(0.01f, D2DColor.Black);
        private readonly D2DColor _clearColor = D2DColor.Black;
        private readonly D2DPoint _infoPosition = new D2DPoint(20, 20);
        private readonly D2DPoint _radialPosition = new D2DPoint(600, 400);
        private readonly D2DColor _missileOverlayColor = new D2DColor(0.5f, 0.03f, 0.03f, 0.03f);
        private readonly D2DColor _hudColor = new D2DColor(0.3f, D2DColor.GreenYellow);
        private D2DLayer _missileOverlayLayer;
        private readonly string _defaultFontName = "Consolas";
        private Graph _fpsGraph;

        private GameTimer _burstTimer = new GameTimer(0.25f, true);
        private GameTimer _decoyTimer = new GameTimer(0.25f, true);
        private GameTimer _playerBurstTimer = new GameTimer(0.1f, true);

        private string _hudMessage = string.Empty;
        private D2DColor _hudMessageColor = D2DColor.Red;
        private GameTimer _hudMessageTimeout = new GameTimer(5f);

        private int _aiPlaneViewIdx = -1;

        private Random _rnd => Helpers.Rnd;

        public PolyPlaneUI()
        {
            InitializeComponent();

            this.MouseWheel += PolyPlaneUI_MouseWheel;
            this.Disposed += PolyPlaneUI_Disposed;

            _burstTimer.TriggerCallback = () => DoAIPlaneBursts();
            _decoyTimer.TriggerCallback = () => DoAIPlaneDecoys();
            _playerBurstTimer.TriggerCallback = () => _playerPlane.FireBullet(p => AddExplosion(p));
        }

        private void PolyPlaneUI_Disposed(object? sender, EventArgs e)
        {
            _device?.Dispose();
            _missileOverlayLayer?.Dispose();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            InitGfx();

            InitPlane();

            StartRenderThread();
        }

        private void InitPlane()
        {
            _playerPlane = new Plane(new D2DPoint(this.Width * 0.5f, -5000f));
            _playerPlane.FireBulletCallback = b => { _bullets.Add(b); };
            _playerPlane.AutoPilotOn = true;
            _playerPlane.ThrustOn = true;
            _playerPlane.Velocity = new D2DPoint(500f, 0f);
        }

        private void ResetPlane()
        {
            _playerPlane.AutoPilotOn = true;
            _playerPlane.ThrustOn = true;
            _playerPlane.Position = new D2DPoint(this.Width * 0.5f, -5000f);
            _playerPlane.Velocity = new D2DPoint(500f, 0f);
            _playerPlane.RotationSpeed = 0f;
            _playerPlane.Rotation = 0f;
            _playerPlane.SASOn = true;
            _playerPlane.IsDamaged = false;
            _playerPlane.Reset();
            _playerPlane.FixPlane();

            _flames.ForEach(f =>
            {
                if (f.Owner.ID == _playerPlane.ID)
                    f.IsExpired = true;

            });
        }

        private void SpawnAIPlane()
        {
            //var aiPlane = new Plane(new D2DPoint(_rnd.NextFloat(-(World.ViewPortSize.width * 0.5f), World.ViewPortSize.width * 0.5f), _rnd.NextFloat(-(World.ViewPortSize.height * 0.5f), -5000f)), _playerPlane);
            var aiPlane = new Plane(new D2DPoint(_rnd.NextFloat(-(World.ViewPortSize.width * 2f), World.ViewPortSize.width * 2f), _rnd.NextFloat(-(World.ViewPortSize.height * 0.5f), -10000f)), _playerPlane);

            aiPlane.FireBulletCallback = b => { _bullets.Add(b); };
            aiPlane.Velocity = new D2DPoint(400f, 0f);

            _newTargets.Enqueue(aiPlane);
            _newAIPlanes.Enqueue(aiPlane);
            //_aiPlanes.Add(aiPlane);
        }

        private void StartRenderThread()
        {
            _renderThread = new Thread(RenderLoop);
            _renderThread.Start();
        }

        private void InitGfx()
        {
            _device?.Dispose();
            _device = D2DDevice.FromHwnd(this.Handle);
            _gfx = new D2DGraphics(_device);
            _gfx.Antialias = true;
            _device.Resize();

            _missileOverlayLayer = _device.CreateLayer();
            _fpsGraph = new Graph(new SizeF(300, 100), new Color[] { Color.Red }, new string[] { "FPS" });

            _ctx = new RenderContext(_gfx);

            World.UpdateViewport(this.Size);
        }

        private void ResizeGfx(bool force = false)
        {
            if (!force)
                if (World.ViewPortBaseSize.height == this.Size.Height && World.ViewPortBaseSize.width == this.Size.Width)
                    return;

            StopRender();

            _device?.Resize();

            World.UpdateViewport(this.Size);

            ResumeRender();
        }

        private void RenderLoop()
        {
            while (!this.Disposing && !_killRender)
            {
                _stopRenderEvent.Wait();

                GraphicsExtensions.OnScreen = 0;
                GraphicsExtensions.OffScreen = 0;

                ResizeGfx();
                ProcessObjQueue();

                var viewPortRect = new D2DRect(_playerPlane.Position, new D2DSize((World.ViewPortSize.width / VIEW_SCALE), World.ViewPortSize.height / VIEW_SCALE));
                _ctx.Viewport = viewPortRect;

                if (_trailsOn || _motionBlur)
                    _gfx.BeginRender();
                else
                    _gfx.BeginRender(_clearColor);

                if (_motionBlur)
                    _gfx.FillRectangle(World.ViewPortRect, _blurColor);


                DrawSky(_ctx);
                DrawMovingBackground(_ctx);

                _gfx.PushTransform();
                _gfx.ScaleTransform(World.ZoomScale, World.ZoomScale);


                // Render stuff...
                if (!_isPaused || _oneStep)
                {
                    var partialDT = World.SUB_DT;

                    for (int i = 0; i < World.PHYSICS_STEPS; i++)
                    {
                        _missiles.ForEach(o => o.Update(partialDT, World.ViewPortSize, World.RenderScale));
                        _targets.ForEach(o => o.Update(partialDT, World.ViewPortSize, World.RenderScale));
                        _bullets.ForEach(o => o.Update(partialDT, World.ViewPortSize, World.RenderScale));

                        //if (!_slewEnable)
                        _playerPlane.Update(partialDT, World.ViewPortSize, World.RenderScale);

                        if (_renderEveryStep)
                        {
                            RenderObjects(_ctx);
                        }

                        DoCollisions();
                    }

                    World.UpdateAirDensityAndWind(World.DT);

                    _oneStep = false;

                    ConsiderTargetPlayer();
                    ConsiderDropDecoy();
                    ConsiderDefendMissile();
                    DoDecoySuccess();

                    _playerBurstTimer.Update(World.DT);
                    _hudMessageTimeout.Update(World.DT);
                    _explosions.ForEach(o => o.Update(World.DT, World.ViewPortSize, World.RenderScale));
                    _flames.ForEach(o => o.Update(World.DT, World.ViewPortSize, World.RenderScale));
                    _missileTrails.ForEach(t => t.Update(World.DT));

                    DoAIPlaneBurst(World.DT);
                    DoAIPlaneDecoy(World.DT);
                }


                if (!_renderEveryStep || _isPaused)
                {
                    RenderObjects(_ctx);
                }


                _gfx.PopTransform();

                DrawHud(_ctx, new D2DSize(this.Width, this.Height));
                DrawOverlays(_ctx);

                _gfx.EndRender();


                var fps = TimeSpan.TicksPerSecond / (float)(DateTime.Now.Ticks - _lastRenderTime);
                _lastRenderTime = DateTime.Now.Ticks;
                _renderFPS = fps;

                //_fpsGraph.Update(fps);

                if (!_pauseRenderEvent.Wait(0))
                {
                    _isPaused = true;
                    _pauseRenderEvent.Set();
                }


                if (_slewEnable)
                {
                    //_playerPlane.RotationSpeed = 0f;
                    _playerPlane.Position = _playerPlaneSlewPos;
                    _playerPlane.Reset();

                }
            }
        }

        private void ProcessObjQueue()
        {
            while (_newTargets.Count > 0)
            {
                if (_newTargets.TryDequeue(out GameObject obj))
                    _targets.Add(obj);
            }

            while (_newMissiles.Count > 0)
            {
                if (_newMissiles.TryDequeue(out GameObject obj))
                {
                    _missiles.Add(obj);
                    _missileTrails.Add(new SmokeTrail(obj, o =>
                    {
                        var m = o as GuidedMissile;
                        return m.CenterOfThrust;
                    }));
                }
            }

            while (_newAIPlanes.Count > 0)
            {
                if (_newAIPlanes.TryDequeue(out Plane plane))
                    _aiPlanes.Add(plane);
            }
        }

        private void DoAIPlaneBurst(float dt)
        {
            if (_aiPlanes.Any(p => p.FiringBurst))
            {
                if (!_burstTimer.IsRunning)
                {
                    _burstTimer.Restart();
                }
            }
            else
            {
                _burstTimer.Stop();
            }

            _burstTimer.Update(dt);
        }

        private void DoAIPlaneBursts()
        {
            var firing = _aiPlanes.Where(p => p.FiringBurst).ToArray();

            if (firing.Length == 0)
                return;

            for (int i = 0; i < firing.Length; i++)
            {
                var plane = firing[i];
                plane.FireBullet(p => AddExplosion(p));
            }
        }

        private void DoAIPlaneDecoy(float dt)
        {
            if (_aiPlanes.Any(p => p.DroppingDecoy))
            {
                if (!_decoyTimer.IsRunning)
                {
                    _decoyTimer.Restart();
                }
            }
            else
            {
                _decoyTimer.Stop();
            }

            _decoyTimer.Update(dt);
        }

        private void DoAIPlaneDecoys()
        {
            var dropping = _aiPlanes.Where(p => p.DroppingDecoy).ToArray();

            if (dropping.Length == 0)
                return;

            for (int i = 0; i < dropping.Length; i++)
            {
                var plane = dropping[i];

                DropDecoy(plane);
            }
        }

        private void RenderObjects(RenderContext ctx)
        {
            DrawPlane(ctx);

            if (World.ShowMissileCloseup)
                DrawAIPlanesOverlay(ctx);
            //DrawMissileOverlays(gfx);
            //DrawMissileTargetOverlays(gfx);
        }

        private void DrawPlane(RenderContext ctx)
        {
            ctx.Gfx.PushTransform();

            var zAmt = World.ZoomScale;
            var pos = new D2DPoint(World.ViewPortSize.width * 0.5f, World.ViewPortSize.height * 0.5f);
            pos *= zAmt;

            var offset = new D2DPoint(-_playerPlane.Position.X, -_playerPlane.Position.Y);
            offset *= zAmt;

            ctx.Gfx.ScaleTransform(VIEW_SCALE, VIEW_SCALE, _playerPlane.Position);
            ctx.Gfx.TranslateTransform(offset.X, offset.Y);
            ctx.Gfx.TranslateTransform(pos.X, pos.Y);

            var viewPortRect = new D2DRect(_playerPlane.Position, new D2DSize((World.ViewPortSize.width / VIEW_SCALE), World.ViewPortSize.height / VIEW_SCALE));

            // Draw the ground.
            ctx.Gfx.FillRectangle(new D2DRect(new D2DPoint(_playerPlane.Position.X, 2000f), new D2DSize(this.Width * World.ViewPortScaleMulti, 4000f)), D2DColor.DarkGreen);

            _targets.ForEach(o => o.Render(ctx));
            _explosions.ForEach(o => o.Render(ctx));
            _missiles.ForEach(o => o.Render(ctx));
            _missileTrails.ForEach(o => o.Render(ctx));
            _playerPlane.Render(ctx);
            _bullets.ForEach(o => o.Render(ctx));
            _flames.ForEach(o => o.Render(ctx));

            //DrawNearObjs(ctx.Gfx);

            ctx.Gfx.PopTransform();
        }

        private void DrawNearObjs(D2DGraphics gfx)
        {
            foreach (var plane in _aiPlanes)
            {
                if (_playerPlane.IsObjNear(plane))
                    gfx.FillEllipse(new D2DEllipse(plane.Position, new D2DSize(5f, 5f)), D2DColor.Red);
            }

            foreach (var bullet in _bullets)
            {
                if (_playerPlane.IsObjNear(bullet))
                    gfx.FillEllipse(new D2DEllipse(bullet.Position, new D2DSize(5f, 5f)), D2DColor.Red);
            }

        }


        private void DrawHud(RenderContext ctx, D2DSize viewportsize)
        {
            DrawAltimeter(ctx.Gfx, viewportsize);
            DrawSpeedo(ctx.Gfx, viewportsize);
            DrawGMeter(ctx.Gfx, viewportsize);
            DrawThrottle(ctx.Gfx, viewportsize);

            if (!_playerPlane.IsDamaged)
            {
                DrawGuideIcon(ctx.Gfx, viewportsize);
                DrawHudMessage(ctx.Gfx, viewportsize);
                DrawTargetPointers(ctx.Gfx, viewportsize);
                DrawMissilePointers(ctx.Gfx, viewportsize);
            }
        }

        private void DrawGuideIcon(D2DGraphics gfx, D2DSize viewportsize)
        {
            const float DIST = 300f;
            var pos = new D2DPoint(viewportsize.width * 0.5f, viewportsize.height * 0.5f);

            var mouseAngle = _guideAngle;
            var mouseVec = Helpers.AngleToVectorDegrees(mouseAngle, DIST);
            gfx.DrawEllipse(new D2DEllipse(pos - mouseVec, new D2DSize(5f, 5f)), _hudColor, 2f);

            var planeAngle = _playerPlane.Rotation;
            var planeVec = Helpers.AngleToVectorDegrees(planeAngle, DIST);
            gfx.DrawCrosshair(pos + planeVec, 2f, _hudColor, 5f, 20f);
        }

        private void DrawHudMessage(D2DGraphics gfx, D2DSize viewportsize)
        {
            if (_hudMessageTimeout.IsRunning && !string.IsNullOrEmpty(_hudMessage))
            {
                var pos = new D2DPoint(viewportsize.width * 0.5f, 200f);
                var rect = new D2DRect(pos, new D2DSize(250, 50));
                gfx.FillRectangle(rect, D2DColor.Gray);
                gfx.DrawTextCenter(_hudMessage, _hudMessageColor, _defaultFontName, 40f, rect);
            }

            if (!_hudMessageTimeout.IsRunning)
                _hudMessage = string.Empty;
        }

        private void NewHudMessage(string message, D2DColor color)
        {
            _hudMessage = message;
            _hudMessageColor = color;
            _hudMessageTimeout.Restart();
        }

        private void DrawThrottle(D2DGraphics gfx, D2DSize viewportsize)
        {
            const float W = 20f;
            const float H = 50f;
            const float xPos = 80f;
            const float yPos = 80f;
            var pos = new D2DPoint(xPos, (viewportsize.height * 0.5f) + yPos);

            var rect = new D2DRect(pos, new D2DSize(W, H));

            gfx.PushTransform();

            gfx.DrawRectangle(rect, _hudColor);
            gfx.DrawTextCenter("THR", _hudColor, _defaultFontName, 15f, new D2DRect(pos + new D2DPoint(0, 40f), new D2DSize(50f, 20f)));

            var throtRect = new D2DRect(pos.X - (W * 0.5f), pos.Y - (H * 0.5f), W, (H * _playerPlane.ThrustAmount));
            gfx.RotateTransform(180f, pos);
            gfx.FillRectangle(throtRect, _hudColor);

            gfx.PopTransform();
        }

        private void DrawGMeter(D2DGraphics gfx, D2DSize viewportsize)
        {
            const float xPos = 80f;
            var pos = new D2DPoint(xPos, viewportsize.height * 0.5f);
            var rect = new D2DRect(pos, new D2DSize(50, 20));

            gfx.DrawText($"G {Math.Round(_playerPlane.GForce, 1)}", _hudColor, _defaultFontName, 15f, rect);
        }

        private void DrawAltimeter(D2DGraphics gfx, D2DSize viewportsize)
        {
            const float MIN_ALT = 3000f;
            const float W = 80f;
            const float H = 400f;
            const float HalfW = W * 0.5f;
            const float HalfH = H * 0.5f;
            const float MARKER_STEP = 100f;
            const float xPos = 200f;
            var pos = new D2DPoint(viewportsize.width - xPos, viewportsize.height * 0.5f);
            var rect = new D2DRect(pos, new D2DSize(W, H));
            var alt = _playerPlane.Altitude;
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

            for (float y = 0; y < H; y += MARKER_STEP)
            {
                if (y % MARKER_STEP == 0)
                {
                    var start = new D2DPoint(pos.X - HalfW, (pos.Y - y + HalfH - MARKER_STEP) + (alt % MARKER_STEP));
                    var end = new D2DPoint(pos.X + HalfW, (pos.Y - y + HalfH - MARKER_STEP) + (alt % MARKER_STEP));

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


        private void DrawSpeedo(D2DGraphics gfx, D2DSize viewportsize)
        {
            const float W = 80f;
            const float H = 400f;
            const float HalfW = W * 0.5f;
            const float HalfH = H * 0.5f;
            const float MARKER_STEP = 50f;//100f;
            const float xPos = 200f;
            var pos = new D2DPoint(xPos, viewportsize.height * 0.5f);
            var rect = new D2DRect(pos, new D2DSize(W, H));
            var spd = _playerPlane.Velocity.Length();
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

            var actualRect = new D2DRect(new D2DPoint(pos.X, pos.Y + HalfH + 20f), new D2DSize(60f, 20f));
            gfx.DrawTextCenter(Math.Round(spd, 0).ToString(), _hudColor, _defaultFontName, 15f, actualRect);
        }

        private void DrawTargetPointers(D2DGraphics gfx, D2DSize viewportsize)
        {
            const float MIN_DIST = 600f;
            var pos = new D2DPoint(viewportsize.width * 0.5f, viewportsize.height * 0.5f);

            var mostCentered = FindMostCentered();
            var mostCenteredPlane = mostCentered as Plane;


            for (int i = 0; i < _targets.Count; i++)
            {
                var target = _targets[i] as Plane;

                if (target == null)
                    continue;

                if (target.IsDamaged)
                    continue;

                var dist = D2DPoint.Distance(_playerPlane.Position, target.Position);
                var dir = target.Position - _playerPlane.Position;
                var angle = dir.Angle(true);
                var color = D2DColor.White;
                var vec = Helpers.AngleToVectorDegrees(angle);
                var pos1 = pos + (vec * 300f);
                var pos2 = pos1 + (vec * 50f);

                var distColor = Helpers.LerpColor(D2DColor.Red, D2DColor.Green, Helpers.Factor(dist, MIN_DIST * 6f));

                float weight = 3f;

                gfx.DrawArrowStroked(pos1, pos2, distColor, weight, D2DColor.White, 3f);

                if (_playerPlane.ClosingRate(target) > 0f)
                    gfx.DrawArrow(pos + (vec * 270f), pos + (vec * 250f), D2DColor.Green);
                else
                    gfx.DrawArrow(pos + (vec * 250f), pos + (vec * 270f), D2DColor.Green);



                if (mostCentered != null && mostCentered.ID == target.ID)
                {
                    if (_playerPlane.IsObjInFOV(mostCentered, World.SENSOR_FOV))
                    {
                        gfx.FillEllipse(new D2DEllipse(pos1, new D2DSize(6f, 6f)), D2DColor.Red);
                    }
                }

            }

            if (mostCentered != null && (mostCenteredPlane != null && !mostCenteredPlane.IsDamaged))
            {
                if (_playerPlane.IsObjInFOV(mostCentered, World.SENSOR_FOV))
                {
                    var dist = D2DPoint.Distance(_playerPlane.Position, mostCentered.Position);
                    var distPos = pos + new D2DPoint(0f, -200f);
                    var dRect = new D2DRect(distPos, new D2DSize(60, 30));
                    gfx.FillRectangle(dRect, new D2DColor(0.5f, D2DColor.Black));
                    gfx.DrawTextCenter(Math.Round(dist, 0).ToString(), _hudColor, _defaultFontName, 15f, dRect);

                }
            }
        }

        private void DrawMissilePointers(D2DGraphics gfx, D2DSize viewportsize)
        {
            const float MIN_DIST = 600f;

            bool warningMessage = false;
            var pos = new D2DPoint(viewportsize.width * 0.5f, viewportsize.height * 0.5f);

            for (int i = 0; i < _missiles.Count; i++)
            {
                var missile = _missiles[i] as GuidedMissile;

                if (missile == null)
                    continue;

                if (missile.Owner.ID == _playerPlane.ID)
                    continue;

                var dist = D2DPoint.Distance(_playerPlane.Position, missile.Position);

                if (dist < MIN_DIST / 2f)
                    continue;

                var dir = missile.Position - _playerPlane.Position;
                var angle = dir.Angle(true);
                var color = D2DColor.Red;
                var vec = Helpers.AngleToVectorDegrees(angle);
                var pos1 = pos + (vec * 200f);
                var pos2 = pos1 + (vec * 20f);
                var distFact = 1f - Helpers.Factor(dist, MIN_DIST * 10f);

                if (missile.IsDistracted)
                    color = D2DColor.Yellow;

                // Display warning if impact time is less than 10 seconds?
                const float MIN_IMPACT_TIME = 10f;
                if (MissileIsImpactThreat(_playerPlane, missile, MIN_IMPACT_TIME))
                    warningMessage = true;

                if (!missile.MissedTarget)
                    gfx.DrawArrow(pos1, pos2, color, (distFact * 30f) + 1f);
            }

            if (warningMessage)
            {
                var rect = new D2DRect(pos - new D2DPoint(0, -200), new D2DSize(120, 30));
                gfx.DrawTextCenter("WARNING", D2DColor.Red, _defaultFontName, 30f, rect);
            }
        }


        private void PauseRender()
        {
            if (!_isPaused)
            {
                _pauseRenderEvent.Reset();
                _pauseRenderEvent.Wait();
            }
        }

        private void ResumeRender()
        {
            if (_isPaused && _stopRenderEvent.Wait(0))
            {
                _pauseRenderEvent.Set();
                _isPaused = false;
            }

            if (!_stopRenderEvent.Wait(0))
                _stopRenderEvent.Set();
        }

        private void StopRender()
        {
            _stopRenderEvent.Reset();
            Thread.Sleep(32);
        }

        private void DoCollisions()
        {
            // Targets/AI Planes vs missiles and bullets.
            for (int r = 0; r < _targets.Count; r++)
            {
                var targ = _targets[r] as GameObjectPoly;
                if (targ == null)
                    continue;

                // Missiles
                for (int m = 0; m < _missiles.Count; m++)
                {
                    var missile = _missiles[m] as Missile;

                    if (missile.Owner.ID == targ.ID)
                        continue;

                    if (targ.Contains(missile, out D2DPoint pos))
                    {
                        if (targ is Plane plane)
                        {
                            if (plane.IsAI)
                            {
                                var oPlane = missile.Owner as Plane;
                                if (oPlane.IsAI)
                                    continue;

                                if (targ.ID != _playerPlane.ID && !plane.IsDamaged)
                                {
                                    _playerScore++;
                                    NewHudMessage("Splash!", D2DColor.GreenYellow);
                                }

                                plane.DoImpact(missile, pos, _flames);
                            }
                        }

                        missile.IsExpired = true;
                        AddExplosion(targ.Position);
                    }
                }

                // Bullets
                for (int b = 0; b < _bullets.Count; b++)
                {
                    var bullet = _bullets[b];

                    // Keep AI planes from shooting each other.
                    if (targ is Plane plane)
                    {
                        if (plane.IsAI && bullet.Owner.ID != _playerPlane.ID)
                            continue;
                    }


                    if (targ.Contains(bullet.Position) && bullet.Owner.ID != targ.ID)
                    {
                        if (!targ.IsExpired)
                            AddExplosion(targ.Position);

                        if (targ is Plane plane2)
                        {
                            if (targ.ID != _playerPlane.ID && !plane2.IsDamaged)
                                _playerScore++;

                            if (plane2.IsAI)
                                plane2.DoImpact(bullet, _flames);
                        }

                        bullet.IsExpired = true;
                    }
                }

                //for (int e = 0; e < _explosions.Count; e++)
                //{
                //    var explosion = _explosions[e];

                //    if (explosion.Contains(targ.Position))
                //    {
                //        targ.IsExpired = true;
                //    }
                //}
            }

            // Handle missiles hit by bullets.
            // And handle player plane hits by AI missiles.
            for (int m = 0; m < _missiles.Count; m++)
            {
                var missile = _missiles[m] as Missile;

                for (int b = 0; b < _bullets.Count; b++)
                {
                    var bullet = _bullets[b];

                    if (bullet.Owner == missile.Owner)
                        continue;

                    if (missile.Contains(bullet.Position))
                    {
                        if (!missile.IsExpired)
                            AddExplosion(missile.Position);

                        missile.IsExpired = true;
                        bullet.IsExpired = true;
                    }
                }

                if (missile.Owner.ID == _playerPlane.ID)
                    continue;

                if (_playerPlane.Contains(missile, out D2DPoint pos))
                {
                    if (!_godMode)
                        _playerPlane.DoImpact(missile, pos, _flames);

                    missile.IsExpired = true;
                    AddExplosion(_playerPlane.Position);
                }
            }


            // Handle player plane vs bullets.
            for (int b = 0; b < _bullets.Count; b++)
            {
                var bullet = _bullets[b];

                if (bullet.Owner.ID == _playerPlane.ID)
                    continue;

                if (_playerPlane.Contains(bullet.Position))
                {
                    if (!_playerPlane.IsExpired)
                        AddExplosion(_playerPlane.Position);

                    if (!_godMode)
                        _playerPlane.DoImpact(bullet, _flames);

                    bullet.IsExpired = true;
                }
            }

            HandleGroundImpacts();
            PruneExpiredObj();
        }

        private void HandleGroundImpacts()
        {
            // AI Planes.
            for (int a = 0; a < _aiPlanes.Count; a++)
            {
                var plane = _aiPlanes[a];

                if (plane.Altitude <= 0f)
                {
                    if (!plane.IsDamaged)
                        plane.SetOnFire(_flames);

                    plane.IsDamaged = true;
                    plane.DoHitGround();
                    plane.SASOn = false;
                    plane.Velocity = D2DPoint.Zero;
                    plane.Position = new D2DPoint(plane.Position.X, 0f);
                    plane.RotationSpeed = 0f;

                    var rot180 = Helpers.ClampAngle180(plane.Rotation);
                    if (rot180 > -90f && rot180 < 0f || rot180 < 90f && rot180 > 0f)
                        plane.Rotation = 0f;
                    else if (rot180 > 90f && rot180 < 180f || rot180 > -180f && rot180 < -90f)
                        plane.Rotation = 180f;
                }
                //plane.IsExpired = true;

                if (plane.IsExpired)
                    _aiPlanes.RemoveAt(a);
            }

            // Player plane.
            if (_playerPlane.Altitude <= 0f)
            {
                if (!_playerPlane.HasCrashed)
                    _playerDeaths++;

                if (!_playerPlane.IsDamaged)
                    _playerPlane.SetOnFire(_flames);

                _playerPlane.IsDamaged = true;
                _playerPlane.DoHitGround();
                _playerPlane.SASOn = false;
                _playerPlane.AutoPilotOn = false;
                _playerPlane.ThrustOn = false;
                _playerPlane.Velocity = D2DPoint.Zero;
                _playerPlane.Position = new D2DPoint(_playerPlane.Position.X, 0f);
                _playerPlane.RotationSpeed = 0f;

                var rot180 = Helpers.ClampAngle180(_playerPlane.Rotation);
                if (rot180 > -90f && rot180 < 0f || rot180 < 90f && rot180 > 0f)
                    _playerPlane.Rotation = 0f;
                else if (rot180 > 90f && rot180 < 180f || rot180 > -180f && rot180 < -90f)
                    _playerPlane.Rotation = 180f;
            }
        }

        private void PruneExpiredObj()
        {

            for (int o = 0; o < _missiles.Count; o++)
            {
                var missile = _missiles[o];

                if (missile.Altitude <= 0f)
                    missile.IsExpired = true;


                // TODO: Remove missiles fired by destoyed player
                if (missile.IsExpired)
                    _missiles.RemoveAt(o);
            }

            for (int o = 0; o < _missileTrails.Count; o++)
            {
                var trail = _missileTrails[o];

                if (trail.IsExpired)
                    _missileTrails.RemoveAt(o);
            }


            for (int o = 0; o < _targets.Count; o++)
            {
                var targ = _targets[o];

                if (targ.IsExpired)
                    _targets.RemoveAt(o);
            }

            for (int o = 0; o < _bullets.Count; o++)
            {
                var bullet = _bullets[o];

                if (bullet.IsExpired)
                    _bullets.RemoveAt(o);
            }

            for (int e = 0; e < _explosions.Count; e++)
            {
                var explosion = _explosions[e];

                if (explosion.IsExpired)
                    _explosions.RemoveAt(e);
            }

            for (int f = 0; f < _flames.Count; f++)
            {
                var flame = _flames[f];

                if (flame.IsExpired)
                    _flames.RemoveAt(f);
            }
        }

        private void KillAllAIPlanes()
        {
            foreach (var plane in _aiPlanes)
            {
                plane.SetOnFire(_flames);
                plane.IsDamaged = true;
            }
        }



        private GameObject FindMostCentered()
        {
            var minFov = float.MaxValue;
            GameObject mostCenter = null;

            for (int i = 0; i < _targets.Count; i++)
            {
                var targ = _targets[i];

                if (targ is Plane plane)
                {
                    if (plane.HasCrashed)
                        continue;
                }

                var fov = _playerPlane.FOVToObject(targ);

                if (fov < minFov)
                {
                    minFov = fov;
                    mostCenter = targ;
                }
            }

            if (mostCenter == null)
                return null;

            return mostCenter;
        }

        private void TargetCenteredWithMissile()
        {

            var mostCenter = FindMostCentered();

            if (mostCenter == null)
                return;

            //var inFov = Helpers.IsPosInFOV(_plane, closest.Position, 40f);

            if (!_playerPlane.IsObjInFOV(mostCenter, World.SENSOR_FOV))
                return;

            var missile = GetNewMissile(mostCenter, _guidanceType);

            _newMissiles.Enqueue(missile);
        }

        private void DropDecoy(Plane plane)
        {
            if (plane.IsDamaged)
                return;

            var decoy = new Decoy(plane);

            _newTargets.Enqueue(decoy);
        }

        private bool MissileIsImpactThreat(Plane plane, Missile missile, float minImpactTime)
        {
            var dist = plane.Position.DistanceTo(missile.Position);
            var navigationTime = dist / (plane.Velocity.Length() + missile.Velocity.Length());
            var closingRate = plane.ClosingRate(missile);

            // Is it going to hit soon, and has positive closing rate and is actively targeting us?
            return (navigationTime < minImpactTime && closingRate > 0f && missile.Target == plane);
        }

        private void ConsiderDefendMissile()
        {
            const float MIN_DIST = 8000f;
            for (int i = 0; i < _aiPlanes.Count; i++)
            {
                var plane = _aiPlanes[i];
                bool isTargeted = false;

                for (int j = 0; j < _missiles.Count; j++)
                {
                    var missile = _missiles[j] as GuidedMissile;
                    var dist = missile.Position.DistanceTo(plane.Position);

                    const float MIN_IMPACT_TIME = 10f; //?
                    if (MissileIsImpactThreat(plane, missile, MIN_IMPACT_TIME))
                        isTargeted = true;
                }

                plane.IsDefending = isTargeted;
            }
        }

        private void ConsiderDropDecoy()
        {
            const float MIN_DIST = 5000f;

            for (int i = 0; i < _aiPlanes.Count; i++)
            {
                var plane = _aiPlanes[i];

                for (int j = 0; j < _missiles.Count; j++)
                {
                    var missile = _missiles[j] as GuidedMissile;

                    if (missile == null)
                        continue;

                    var dist = D2DPoint.Distance(plane.Position, missile.Position);
                    if (missile.Target.ID == plane.ID && !missile.MissedTarget && dist <= MIN_DIST)
                        plane.DropDecoys();
                }
            }
        }

        private void ConsiderTargetPlayer()
        {
            const float min_time = 10f;//10f;
            const float max_time = 40f;//20f;

            if (_aiTime > 0 && _aiTime > _nextAITime)
            {
                _aiTime = 0;
                _nextAITime = _rnd.NextFloat(min_time, max_time);

                var aiPlanes = new List<Plane>();
                for (int i = 0; i < _targets.Count; i++)
                {
                    var obj = _targets[i];

                    var plane = obj as Plane;

                    if (plane != null && plane.IsAI && !plane.IsDamaged)
                        aiPlanes.Add(plane);
                }

                if (aiPlanes.Count == 0)
                    return;

                //var rndEngagePlane = aiPlanes[_rnd.Next(aiPlanes.Count)];
                //rndEngagePlane.EngagePlayer(_rnd.NextFloat(50f, 200f));


                // Find AI planes pointing at player and fire missiles from a random plane.
                var planesWithLock = new List<Plane>();
                for (int i = 0; i < aiPlanes.Count; i++)
                {
                    var plane = aiPlanes[i];

                    if (plane.IsObjInFOV(_playerPlane, World.SENSOR_FOV))
                        planesWithLock.Add(plane);
                }

                if (planesWithLock.Count > 0)
                {
                    var rndPlane = planesWithLock[_rnd.Next(planesWithLock.Count)];
                    //var missile = new GuidedMissile(rndPlane, _playerPlane, GuidanceType.BasicLOS, useControlSurfaces: true, useThrustVectoring: true);
                    var missile = new GuidedMissile(rndPlane, _playerPlane, GuidanceType.Advanced, useControlSurfaces: true, useThrustVectoring: false);

                    Debug.WriteLine("MISSILE LAUNCH!");

                    _newMissiles.Enqueue(missile);

                }
            }

            if (!_isPaused)
                _aiTime += World.DT;

        }

        private void DoDecoySuccess()
        {
            // Test for decoy success.
            const float MIN_DECOY_FOV = 10f;
            var decoys = _targets.Where(t => t is Decoy).ToList();


            for (int i = 0; i < _missiles.Count; i++)
            {
                var missile = _missiles[i] as GuidedMissile;
                var target = missile.Target as Plane;

                if (target == null)
                    continue;

                if (missile == null)
                    continue;

                GameObject maxTempObj;
                var maxTemp = 0f;
                const float MaxEngineTemp = 1800f;
                const float MaxDecoyTemp = 2000f;

                const float EngineRadius = 4f;
                const float DecoyRadius = 2f;

                var targetDist = D2DPoint.Distance(missile.Position, target.Position);
                var targetTemp = MaxEngineTemp * target.ThrustAmount * EngineRadius;
                var engineArea = 4f * (float)Math.PI * (float)Math.Pow(targetDist, 2f);
                targetTemp /= engineArea;

                maxTempObj = target;
                maxTemp = targetTemp;

                for (int k = 0; k < decoys.Count; k++)
                {
                    var decoy = decoys[k];

                    if (!missile.IsObjInFOV(decoy, MIN_DECOY_FOV))
                        continue;

                    var dist = D2DPoint.Distance(decoy.Position, missile.Position);
                    var decoyTemp = (MaxDecoyTemp * DecoyRadius) / (4f * (float)Math.PI * (float)Math.Pow(dist, 2f));

                    if (decoyTemp > maxTemp)
                    {
                        maxTemp = decoyTemp;
                        maxTempObj = decoy;
                    }

                }

                if (maxTempObj is Decoy)
                {
                    missile.DoChangeTargetChance(maxTempObj);
                }
            }
        }
        private Missile GetNewMissile(GameObject target, GuidanceType guidance)
        {
            switch (_interceptorType)
            {
                case InterceptorTypes.ControlSurface:
                    return new GuidedMissile(_playerPlane, target, guidance, useControlSurfaces: true, useThrustVectoring: false);

                case InterceptorTypes.ControlSurfaceWithThrustVectoring:
                    return new GuidedMissile(_playerPlane, target, guidance, useControlSurfaces: true, useThrustVectoring: true);

                case InterceptorTypes.DirectRotation:
                    return new GuidedMissile(_playerPlane, target, guidance, useControlSurfaces: false);

                case InterceptorTypes.KillVehicle:
                    return new EKVMissile(_playerPlane, target);

                default:
                    return new GuidedMissile(_playerPlane, target, guidance, useControlSurfaces: true);
            }
        }

        private void AddExplosion(D2DPoint pos)
        {
            var explosion = new Explosion(pos, 200f, 1.4f);

            _explosions.Add(explosion);
        }


        private void Clear()
        {
            PauseRender();
            _missiles.Clear();
            _missileTrails.Clear();
            _targets.Clear();
            _bullets.Clear();
            _explosions.Clear();
            _aiPlanes.Clear();
            _playerScore = 0;
            _playerDeaths = 0;
            _flames.Clear();
            ResumeRender();
        }


        private void DrawOverlays(RenderContext ctx)
        {
            DrawInfo(ctx.Gfx, _infoPosition);

            //var center = new D2DPoint(World.ViewPortSize.width * 0.5f, World.ViewPortSize.height * 0.5f);
            //var angVec = Helpers.AngleToVectorDegrees(_testAngle);
            //gfx.DrawLine(center, center + (angVec * 100f), D2DColor.Red);


            if (World.EnableTurbulence || World.EnableWind)
                DrawWindAndTurbulenceOverlay(ctx);


            if (_playerPlane.IsDamaged)
                ctx.Gfx.FillRectangle(World.ViewPortRect, new D2DColor(0.2f, D2DColor.Red));

            //DrawFPSGraph(gfx);
            //DrawGrid(gfx);

            //DrawRadial(ctx.Gfx, _radialPosition);
        }

        private void DrawFPSGraph(RenderContext ctx)
        {
            var pos = new D2DPoint(300, 300);
            _fpsGraph.Render(ctx.Gfx, pos, 1f);
        }


        private float _guideAngle = 0f;
        private void DrawRadial(D2DGraphics ctx, D2DPoint pos)
        {
            const float radius = 300f;
            const float step = 10f;

            float angle = 0f;

            while (angle < 360f)
            {
                var vec = Helpers.AngleToVectorDegrees(angle);
                vec = pos + (vec * radius);

                ctx.DrawLine(pos, vec, D2DColor.DarkGray, 1, D2DDashStyle.Dash);

                ctx.DrawText(angle.ToString(), D2DColor.White, _defaultFontName, 12f, new D2DRect(vec.X, vec.Y, 100f, 30f));

                angle += step;
            }

            ctx.DrawEllipse(new D2DEllipse(pos, new D2DSize(radius, radius)), D2DColor.White);


            float testDiff = 200f;
            float testFact = 0.6f;
            float angle1 = _guideAngle;
            float angle2 = _guideAngle + testDiff;

            ctx.DrawLine(pos, pos + Helpers.AngleToVectorDegrees(angle1) * (radius), D2DColor.Green);


            //        if (!_isPaused)
            //_testAngle = Helpers.ClampAngle(_testAngle + 1f);
        }


        private void DrawAIPlanesOverlay(RenderContext ctx)
        {
            if (_aiPlaneViewIdx < 0 || _aiPlaneViewIdx > _aiPlanes.Count - 1)
                return;

            var plane = _aiPlanes[_aiPlaneViewIdx];

            var scale = 5f;
            var zAmt = World.ZoomScale;
            var pos = new D2DPoint(World.ViewPortSize.width * 0.85f, World.ViewPortSize.height * 0.20f);
            pos *= zAmt;

            ctx.Gfx.PushLayer(_missileOverlayLayer, new D2DRect(pos * World.ViewPortScaleMulti, new D2DSize(1000f, 1000f)));
            ctx.Gfx.Clear(_missileOverlayColor);

            ctx.Gfx.PushTransform();

            var offset = new D2DPoint(-plane.Position.X, -plane.Position.Y);
            offset *= zAmt;

            ctx.Gfx.ScaleTransform(scale, scale, plane.Position);
            ctx.Gfx.TranslateTransform(offset.X, offset.Y);
            ctx.Gfx.TranslateTransform(pos.X, pos.Y);

            var vp = new D2DRect(plane.Position, World.ViewPortSize);
            ctx.PushViewPort(vp);

            var test = vp.Contains(plane.Position);

            _targets.ForEach(t =>
            {
                if (t is Decoy d)
                    d.Render(ctx);
            });

            _missiles.ForEach(m => m.Render(ctx));

            plane.Render(ctx);

            _flames.ForEach(f => f.Render(ctx));

            ctx.DrawText(plane.Altitude.ToString(), D2DColor.White, _defaultFontName, 10f, new D2DRect(plane.Position + new D2DPoint(20, 80), new D2DSize(100, 20)));
            ctx.DrawText(plane.Velocity.Length().ToString(), D2DColor.White, _defaultFontName, 10f, new D2DRect(plane.Position + new D2DPoint(20, 90), new D2DSize(100, 20)));

            ctx.PopViewPort();

            ctx.Gfx.PopTransform();
            ctx.Gfx.PopLayer();
        }

        private void DrawMissileTargetOverlays(RenderContext ctx)
        {
            var plrMissiles = _missiles.Where(m => m.Owner.ID == _playerPlane.ID).ToArray();
            if (plrMissiles.Length == 0)
                return;

            var scale = 5f;
            var zAmt = World.ZoomScale;
            var pos = new D2DPoint(World.ViewPortSize.width * 0.85f, World.ViewPortSize.height * 0.20f);
            pos *= zAmt;

            ctx.Gfx.PushLayer(_missileOverlayLayer, new D2DRect(pos * World.ViewPortScaleMulti, new D2DSize(1000f, 1000f)));
            ctx.Gfx.Clear(_missileOverlayColor);

            for (int m = 0; m < plrMissiles.Length; m++)
            {
                var missile = plrMissiles[m] as GuidedMissile;
                var target = missile.Target as Plane;

                if (target == null)
                    continue;

                if (missile.Owner.ID != _playerPlane.ID)
                    continue;

                ctx.Gfx.PushTransform();

                var offset = new D2DPoint(-target.Position.X, -target.Position.Y);
                offset *= zAmt;

                ctx.Gfx.ScaleTransform(scale, scale, target.Position);
                ctx.Gfx.TranslateTransform(offset.X, offset.Y);
                ctx.Gfx.TranslateTransform(pos.X, pos.Y);

                target.Render(ctx);

                //for (int t = 0; t < _targets.Count; t++)
                //    _targets[t].Render(gfx);

                //missile.Render(gfx);

                _targets.ForEach(t =>
                {
                    if (t is Decoy d)
                        d.Render(ctx);

                });

                _flames.ForEach(f => f.Render(ctx));

                var dist = D2DPoint.Distance(missile.Position, missile.Target.Position);

                //gfx.DrawText(missile.Velocity.Length().ToString(), D2DColor.White, _defaultFontName, 20f, new D2DRect(pos - new D2DPoint(0,0),new D2DSize(500,500)));
                ctx.DrawText(Math.Round(missile.Velocity.Length(), 1).ToString(), D2DColor.White, _defaultFontName, 10f, new D2DRect(missile.Position + new D2DPoint(80, 80), new D2DSize(50, 20)));
                ctx.DrawText(Math.Round(dist, 1).ToString(), D2DColor.White, _defaultFontName, 10f, new D2DRect(missile.Position - new D2DPoint(60, -80), new D2DSize(50, 20)));

                ctx.Gfx.PopTransform();
            }

            ctx.Gfx.PopLayer();
        }

        private void DrawMissileOverlays(RenderContext ctx)
        {
            var plrMissiles = _missiles.Where(m => m.Owner.ID == _playerPlane.ID).ToArray();
            if (plrMissiles.Length == 0)
                return;

            var scale = 5f;
            var zAmt = World.ZoomScale;
            var pos = new D2DPoint(World.ViewPortSize.width * 0.85f, World.ViewPortSize.height * 0.20f);
            pos *= zAmt;

            ctx.Gfx.PushLayer(_missileOverlayLayer, new D2DRect(pos * World.ViewPortScaleMulti, new D2DSize(1000f, 1000f)));
            ctx.Gfx.Clear(_missileOverlayColor);

            for (int m = 0; m < plrMissiles.Length; m++)
            {
                var missile = plrMissiles[m] as GuidedMissile;

                if (missile.Owner.ID != _playerPlane.ID)
                    continue;

                ctx.Gfx.PushTransform();

                var offset = new D2DPoint(-missile.Position.X, -missile.Position.Y);
                offset *= zAmt;

                ctx.Gfx.ScaleTransform(scale, scale, missile.Position);
                ctx.Gfx.TranslateTransform(offset.X, offset.Y);
                ctx.Gfx.TranslateTransform(pos.X, pos.Y);

                for (int t = 0; t < _targets.Count; t++)
                    _targets[t].Render(ctx);

                missile.Render(ctx);

                var dist = D2DPoint.Distance(missile.Position, missile.Target.Position);

                //gfx.DrawText(missile.Velocity.Length().ToString(), D2DColor.White, _defaultFontName, 20f, new D2DRect(pos - new D2DPoint(0,0),new D2DSize(500,500)));
                ctx.DrawText(Math.Round(missile.Velocity.Length(), 1).ToString(), D2DColor.White, _defaultFontName, 10f, new D2DRect(missile.Position + new D2DPoint(80, 80), new D2DSize(50, 20)));
                ctx.DrawText(Math.Round(dist, 1).ToString(), D2DColor.White, _defaultFontName, 10f, new D2DRect(missile.Position - new D2DPoint(60, -80), new D2DSize(50, 20)));

                ctx.Gfx.PopTransform();
            }

            ctx.Gfx.PopLayer();
        }

        private void DrawSky(RenderContext ctx)
        {
            const float barH = 20f;
            const float MAX_ALT = 50000f;

            var plrAlt = Math.Abs(_playerPlane.Position.Y);
            if (_playerPlane.Position.Y >= 0)
                plrAlt = 0f;


            var color1 = new D2DColor(0.5f, D2DColor.SkyBlue);
            var color2 = new D2DColor(0.5f, D2DColor.Black);
            var rect = new D2DRect(new D2DPoint(this.Width * 0.5f, 0), new D2DSize(this.Width, barH));
            plrAlt += this.Height / 2f;

            for (float y = 0; y < this.Height; y += barH)
            {
                var posY = (plrAlt - y);
                var color = Helpers.LerpColor(color1, color2, (posY / MAX_ALT));

                rect.Y = y;
                ctx.Gfx.FillRectangle(rect, color);
            }
        }

        private void DrawMovingBackground(RenderContext ctx)
        {
            float spacing = 75f;
            const float size = 4f;
            var d2dSz = new D2DSize(size, size);
            var color = new D2DColor(0.4f, D2DColor.Gray);

            var plrPos = _playerPlane.Position;
            plrPos /= World.ViewPortScaleMulti;
            var roundPos = new D2DPoint((plrPos.X) % spacing, (plrPos.Y) % spacing);
            roundPos *= 4f;

            var rect = new D2DRect(0, 0, this.Width, this.Height);

            int hits = 0;
            int miss = 0;

            for (float x = 0 - (spacing * 3f); x < this.Width + roundPos.X; x += spacing)
            {
                for (float y = 0 - (spacing * 3f); y < this.Height + roundPos.Y; y += spacing)
                {
                    var pos = new D2DPoint(x, y);
                    pos -= roundPos;

                    if (rect.Contains(pos))
                    {
                        ctx.Gfx.FillRectangle(new D2DRect(pos, d2dSz), color);
                        hits++;
                    }
                    else
                        miss++;
                }
            }
        }

        private void DrawWindAndTurbulenceOverlay(RenderContext ctx)
        {
            var pos = new D2DPoint(this.Width - 100f, 100f);

            ctx.FillEllipse(new D2DEllipse(pos, new D2DSize(World.AirDensity * 10f, World.AirDensity * 10f)), D2DColor.SkyBlue);

            ctx.DrawLine(pos, pos + (World.Wind * 2f), D2DColor.White, 2f);
        }

        private void DrawInfo(D2DGraphics gfx, D2DPoint pos)
        {
            string infoText = string.Empty;
            infoText += $"Paused: {_isPaused}\n\n";
            infoText += $"Guidance Type: {_guidanceType.ToString()}\n";
            //infoText += $"Missile Type: {_interceptorType.ToString()}\n";
            //infoText += $"Target Type: {_targetTypes.ToString()}\n\n";
            infoText += $"Overlay (Tracking/Aero/Missile): {(World.ShowTracking ? "On" : "Off")}/{(World.ShowAero ? "On" : "Off")}/{(World.ShowMissileCloseup ? "On" : "Off")} \n";
            infoText += $"Turbulence/Wind: {(World.EnableTurbulence ? "On" : "Off")}/{(World.EnableWind ? "On" : "Off")}\n";

            var numObj = _missiles.Count + _targets.Count + _bullets.Count + _explosions.Count + _aiPlanes.Count + _flames.Count;
            infoText += $"Num Objects: {numObj}\n";
            infoText += $"On Screen: {GraphicsExtensions.OnScreen}\n";
            infoText += $"Off Screen: {GraphicsExtensions.OffScreen}\n";


            infoText += $"FPS: {Math.Round(_renderFPS, 0)}\n";
            infoText += $"Zoom: {Math.Round(World.ZoomScale, 2)}\n";
            infoText += $"DT: {Math.Round(World.DT, 4)}\n";
            //infoText += $"Trails: {(_trailsOn ? "Trails" : _motionBlur ? "Blur" : "Off")}\n\n";
            infoText += $"AutoPilot: {(_playerPlane.AutoPilotOn ? "On" : "Off")}\n";
            infoText += $"Position: {_playerPlane?.Position}\n";
            //infoText += $"Rotation: {_playerPlane?.Rotation}\n";
            //infoText += $"Rotation: {Helpers.ClampAngle180(_playerPlane.Rotation)}\n";

            infoText += $"Score: {_playerScore}\n";
            infoText += $"Deaths: {_playerDeaths}\n";


            //infoText += $"Velocity: {_playerPlane?.Velocity.Length()}\n";
            //infoText += $"Density Alt: {World.GetDensityAltitude(_playerPlane.Position)}\n";



            if (_showHelp)
            {
                infoText += $@"
            H: Hide help

            P: Pause
            B: Motion Blur
            T: Trails
            N: Pause/One Step
            R: Spawn Target
            A: Spawn target at click pos
            M: Move ship to click pos
            C: Clear all
            I: Toggle Aero Display
            O: Toggle Missile View
            U: Toggle Guidance Tracking Dots
            S: Toggle Missile Type
            Y: Cycle Target Types
            K: Toggle Turbulence
            L: Toggle Wind
            +/-: Zoom
            Shift + (+/-): Change Delta Time
            S: Missile Type
            Shift + Mouse-Wheel or E: Guidance Type
            Left-Click: Thrust ship
            Right-Click: Fire auto cannon
            Middle-Click or Enter: Fire missile (Hold Shift to fire all types)
            Mouse-Wheel: Rotate ship";
            }
            else
            {
                infoText += "\n";
                infoText += "H: Show help";
            }

            gfx.DrawText(infoText, D2DColor.GreenYellow, _defaultFontName, 12f, pos.X, pos.Y);
        }

        private void PolyPlaneUI_KeyPress(object sender, KeyPressEventArgs e)
        {
            switch (e.KeyChar)
            {
                case 'a':
                    _playerPlane.AutoPilotOn = !_playerPlane.AutoPilotOn;
                    break;

                case 'b':
                    _motionBlur = !_motionBlur;
                    _trailsOn = false;
                    break;

                case 'c':
                    Clear();
                    break;

                case 'd':
                    DropDecoy(_playerPlane);
                    break;

                case 'e':
                    _guidanceType = Helpers.CycleEnum(_guidanceType);
                    break;

                case 'h':
                    _showHelp = !_showHelp;
                    break;

                case 'i':
                    World.ShowAero = !World.ShowAero;
                    break;

                case 'k':
                    World.EnableTurbulence = !World.EnableTurbulence;
                    KillAllAIPlanes();
                    break;

                case 'l':
                    World.EnableWind = !World.EnableWind;
                    break;

                case 'm':
                    InitPlane();
                    break;

                case 'n':
                    _isPaused = true;
                    _oneStep = true;
                    break;

                case 'o':
                    World.ShowMissileCloseup = !World.ShowMissileCloseup;
                    break;

                case 'p':

                    if (!_isPaused)
                        PauseRender();
                    else
                        ResumeRender();
                    break;

                case 'r':
                    ResetPlane();
                    break;

                case 's':
                    _slewEnable = !_slewEnable;

                    if (_slewEnable)
                        _playerPlaneSlewPos = _playerPlane.Position;

                    break;

                case 't':
                    break;

                case 'u':
                    SpawnAIPlane();
                    break;

                case 'y':
                    _targetTypes = Helpers.CycleEnum(_targetTypes);
                    break;

                case '=' or '+':
                    if (_shiftDown)
                    {
                        World.DT += DT_ADJ_AMT;
                    }
                    else
                    {
                        World.ZoomScale += 0.01f;
                        ResizeGfx(force: true);
                    }
                    break;

                case '-' or '_':

                    if (_shiftDown)
                    {
                        World.DT -= DT_ADJ_AMT;
                    }
                    else
                    {
                        World.ZoomScale -= 0.01f;
                        ResizeGfx(force: true);
                    }
                    break;

                case '[':
                    _aiPlaneViewIdx--;
                    _aiPlaneViewIdx = Math.Clamp(_aiPlaneViewIdx, 0, _aiPlanes.Count);
                    break;
                case ']':

                    if (_aiPlanes.Count == 0)
                        return;

                    _aiPlaneViewIdx = (_aiPlaneViewIdx + 1) % _aiPlanes.Count;
                    break;

            }
        }

        private void PolyPlaneUI_MouseUp(object sender, MouseEventArgs e)
        {
            //_player.FlameOn = false;


            if (e.Button == MouseButtons.Left)
            {
                _fireBurst = false;
                _playerBurstTimer.Stop();
            }
        }

        private void PolyPlaneUI_MouseDown(object sender, MouseEventArgs e)
        {
            switch (e.Button)
            {
                case MouseButtons.Left:
                    _playerBurstTimer.Start();
                    break;

                case MouseButtons.Right:
                    //_playerPlane.ToggleThrust();
                    DropDecoy(_playerPlane);
                    break;

                case MouseButtons.Middle:
                    TargetCenteredWithMissile();
                    break;

            }
        }

        private void PolyPlaneUI_MouseWheel(object? sender, MouseEventArgs e)
        {
            if (!_shiftDown)
            {
                //if (e.Delta > 0)
                //    _playerPlane.Pitch(true);
                //else
                //    _playerPlane.Pitch(false);

                //DropDecoy(_playerPlane);

                if (e.Delta > 0)
                    _playerPlane.MoveThrottle(true);
                else
                    _playerPlane.MoveThrottle(false);

            }
            else
            {
                var len = Enum.GetNames(typeof(GuidanceType)).Length;
                var cur = (int)_guidanceType;
                int next = cur;

                if (e.Delta < 0)
                    next = (next + 1) % len;
                else
                    next = (next - 1) < 0 ? len - 1 : next - 1;

                _guidanceType = (GuidanceType)next;
            }
        }

        private void PolyPlaneUI_KeyDown(object sender, KeyEventArgs e)
        {
            _shiftDown = e.Shift;
        }

        private void PolyPlaneUI_KeyUp(object sender, KeyEventArgs e)
        {
            _shiftDown = e.Shift;
        }

        private void PolyPlaneUI_MouseMove(object sender, MouseEventArgs e)
        {
            var center = new D2DPoint(World.ViewPortSize.width * 0.5f, World.ViewPortSize.height * 0.5f);
            var pos = new D2DPoint(e.X, e.Y) * World.ViewPortScaleMulti;
            var angle = pos.AngleTo(center);

            _guideAngle = angle;
            _playerPlane.SetAutoPilotAngle(angle);
        }

        private void PolyPlaneUI_FormClosing(object sender, FormClosingEventArgs e)
        {
            _renderThread?.Join(1000);
            //_renderThread.Wait(1000);


        }
    }
}