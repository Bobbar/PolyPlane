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
        private Thread _gameThread;

        private const float DT_ADJ_AMT = 0.00025f;
        private const float VIEW_SCALE = 4f;

        private ManualResetEventSlim _pauseRenderEvent = new ManualResetEventSlim(true);
        private ManualResetEventSlim _stopRenderEvent = new ManualResetEventSlim(true);

        private bool _isPaused = false;
        private bool _trailsOn = false;
        private bool _oneStep = false;
        private bool _killRender = false;
        private bool _motionBlur = false;
        private bool _shiftDown = false;
        private bool _showHelp = false;
        private bool _godMode = false;
        private bool _clearObjs = false;
        private int _playerScore = 0;
        private int _playerDeaths = 0;
        private bool _queueNextViewId = false;
        private bool _queuePrevViewId = false;
        private bool _skipRender = false;
        private long _lastRenderTime = 0;
        private float _renderFPS = 0;
        private bool _useMultiThread = true;
        private bool _showInfo = true;
        private int _multiThreadNum = 4;

        private List<GameObject> _missiles = new List<GameObject>();
        private List<SmokeTrail> _missileTrails = new List<SmokeTrail>();
        private List<GameObject> _targets = new List<GameObject>();
        private List<GameObject> _decoys = new List<GameObject>();
        private List<GameObjectPoly> _bullets = new List<GameObjectPoly>();
        private List<GameObject> _explosions = new List<GameObject>();
        private List<Plane> _aiPlanes = new List<Plane>();

        private List<GameObject> _updateObjects = new List<GameObject>();

        private ConcurrentQueue<GameObject> _newTargets = new ConcurrentQueue<GameObject>();
        private ConcurrentQueue<GameObject> _newDecoys = new ConcurrentQueue<GameObject>();
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

        private long _aiPlaneViewID = -1;

        private Stopwatch _timer = new Stopwatch();
        private TimeSpan _renderTime = new TimeSpan();
        private TimeSpan _updateTime = new TimeSpan();
        private TimeSpan _collisionTime = new TimeSpan();


        private Random _rnd => Helpers.Rnd;

        public PolyPlaneUI()
        {
            InitializeComponent();

            this.MouseWheel += PolyPlaneUI_MouseWheel;
            this.Disposed += PolyPlaneUI_Disposed;

            _burstTimer.TriggerCallback = () => DoAIPlaneBursts();
            _decoyTimer.TriggerCallback = () => DoAIPlaneDecoys();
            _playerBurstTimer.TriggerCallback = () => _playerPlane.FireBullet(p => AddExplosion(p));

            _multiThreadNum = Environment.ProcessorCount;
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

            StartGameThread();
        }

        private void InitPlane()
        {
            _playerPlane = new Plane(new D2DPoint(this.Width * 0.5f, -5000f));
            _playerPlane.FireBulletCallback = b => { _bullets.Add(b); };
            _playerPlane.AutoPilotOn = true;
            _playerPlane.ThrustOn = true;
            _playerPlane.Velocity = new D2DPoint(500f, 0f);

            _playerPlane.Radar = new Radar(_playerPlane, _hudColor, _targets, _missiles);
            _playerPlane.Radar.SkipFrames = World.PHYSICS_STEPS;

            _playerPlane.FireMissileCallback = (m) => _newMissiles.Enqueue(m);

            _newTargets.Enqueue(_playerPlane);
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

            _playerPlane.Radar = new Radar(_playerPlane, _hudColor, _targets, _missiles);
            _playerPlane.Radar.SkipFrames = World.PHYSICS_STEPS;

            _playerPlane.FireMissileCallback = (m) => _newMissiles.Enqueue(m);
        }

        private void TargetLockedWithMissile()
        {
            if (_playerPlane.Radar.HasLock && _playerPlane.Radar.LockedObj != null)
                _playerPlane.FireMissile(_playerPlane.Radar.LockedObj);
        }
        private void SpawnAIPlane()
        {
            //var pos = new D2DPoint(_rnd.NextFloat(-(World.ViewPortSize.width * 4f), World.ViewPortSize.width * 4f), _rnd.NextFloat(-(World.ViewPortSize.height * 0.5f), -15000f));
            var pos = new D2DPoint(_rnd.NextFloat(-(World.ViewPortSize.width * 4f), World.ViewPortSize.width * 4f), _rnd.NextFloat(-4000f, -17000f));

            var aiPlane = new Plane(pos, _playerPlane);
            aiPlane.Radar = new Radar(aiPlane, _hudColor, _targets, _missiles);
            aiPlane.Radar.SkipFrames = World.PHYSICS_STEPS;

            aiPlane.FireMissileCallback = (m) => _newMissiles.Enqueue(m);


            aiPlane.FireBulletCallback = b => { _bullets.Add(b); };
            aiPlane.Velocity = new D2DPoint(400f, 0f);

            _newTargets.Enqueue(aiPlane);
            _newAIPlanes.Enqueue(aiPlane);
        }

        private void StartGameThread()
        {
            _gameThread = new Thread(GameLoop);
            _gameThread.Priority = ThreadPriority.AboveNormal;
            _gameThread.Start();
            _decoyTimer.Start();
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

            _ctx = new RenderContext(_gfx, _device);

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

        private void GameLoop()
        {
            while (!this.Disposing && !_killRender)
            {
                _stopRenderEvent.Wait();

                AdvanceAndRender();

                if (!_pauseRenderEvent.Wait(0))
                {
                    _isPaused = true;
                    _pauseRenderEvent.Set();
                }
            }
        }

        private void AdvanceAndRender()
        {
            _renderTime = TimeSpan.Zero;
            _updateTime = TimeSpan.Zero;
            _collisionTime = TimeSpan.Zero;

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

            Plane viewPlane = GetViewPlane();


            _timer.Restart();

            DrawSky(_ctx, viewPlane);
            DrawMovingBackground(_ctx, viewPlane);

            _timer.Stop();
            _renderTime += _timer.Elapsed;

            _gfx.PushTransform();
            _gfx.ScaleTransform(World.ZoomScale, World.ZoomScale);

            // Update/advance objects.
            if (!_isPaused || _oneStep)
            {
                var partialDT = World.SUB_DT;

                var objs = GetAllObjects();
                var numObj = objs.Count;

                for (int i = 0; i < World.PHYSICS_STEPS; i++)
                {
                    _timer.Restart();

                    DoCollisions();

                    _timer.Stop();

                    _collisionTime += _timer.Elapsed;

                    _timer.Restart();

                    if (_useMultiThread)
                    {
                        objs.ForEachParallel(o => o.Update(partialDT, World.ViewPortSize, World.RenderScale), _multiThreadNum);
                    }
                    else
                    {
                        objs.ForEach(o => o.Update(partialDT, World.ViewPortSize, World.RenderScale));
                    }

                    _timer.Stop();
                    _updateTime += _timer.Elapsed;
                }

                _timer.Restart();

                World.UpdateAirDensityAndWind(World.DT);

                DoDecoySuccess();

                _playerBurstTimer.Update(World.DT);
                _hudMessageTimeout.Update(World.DT);
                _explosions.ForEach(o => o.Update(World.DT, World.ViewPortSize, World.RenderScale));
                _decoys.ForEach(o => o.Update(World.DT, World.ViewPortSize, World.RenderScale));

                _missileTrails.ForEach(t => t.Update(World.DT, World.ViewPortSize, World.RenderScale));

                DoAIPlaneBurst(World.DT);
                _decoyTimer.Update(World.DT);

                _timer.Stop();
                _updateTime += _timer.Elapsed;

                _oneStep = false;
            }

            _timer.Restart();

            // Render objects.
            if (!_skipRender)
                DrawPlaneAndObjects(_ctx, viewPlane);

            _gfx.PopTransform();

            DrawHud(_ctx, new D2DSize(this.Width, this.Height), viewPlane);
            DrawOverlays(_ctx);

            _timer.Stop();
            _renderTime += _timer.Elapsed;


            _gfx.EndRender();

            var now = DateTime.UtcNow.Ticks;
            var fps = TimeSpan.TicksPerSecond / (float)(now - _lastRenderTime);
            _lastRenderTime = now;
            _renderFPS = fps;

            //_fpsGraph.Update(fps);

            if (_slewEnable)
            {
                _playerPlane.Rotation = _guideAngle;
                _playerPlane.RotationSpeed = 0f;
                _playerPlane.Position = _playerPlaneSlewPos;
                _playerPlane.Reset();
                _playerPlane.Velocity = D2DPoint.Zero;
                _playerPlane.HasCrashed = true;
                _godMode = true;
            }
        }

        private Plane GetViewPlane()
        {
            var idPlane = IDToPlane(_aiPlaneViewID);

            if (idPlane != null)
            {
                return idPlane;
            }
            else
                return _playerPlane;
        }

        private List<GameObject> GetAllObjects()
        {
            _updateObjects.Clear();

            _updateObjects.AddRange(_missiles);
            _updateObjects.AddRange(_targets);
            _updateObjects.AddRange(_bullets);

            return _updateObjects;
        }


        private void DrawNearObj(D2DGraphics gfx, Plane plane)
        {
            _targets.ForEach(t =>
            {
                if (t.IsObjNear(plane))
                    gfx.FillEllipseSimple(t.Position, 5f, D2DColor.Red);

            });

            _bullets.ForEach(b =>
            {
                if (b.IsObjNear(plane))
                    gfx.FillEllipseSimple(b.Position, 5f, D2DColor.Red);

            });

            _missiles.ForEach(m =>
            {
                if (m.IsObjNear(plane))
                    gfx.FillEllipseSimple(m.Position, 5f, D2DColor.Red);

            });

            _decoys.ForEach(d =>
            {
                if (d.IsObjNear(plane))
                    gfx.FillEllipseSimple(d.Position, 5f, D2DColor.Red);

            });
        }

        private Plane IDToPlane(long id)
        {
            var plane = _aiPlanes.Where(p => p.ID == id).FirstOrDefault();

            if (plane == null)
                return _playerPlane;

            return plane;
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

            while (_newDecoys.Count > 0)
            {
                if (_newDecoys.TryDequeue(out GameObject decoy))
                    _decoys.Add(decoy);
            }

            if (_queueNextViewId)
            {
                _aiPlaneViewID = GetNextAIID();
                _queueNextViewId = false;
            }

            if (_queuePrevViewId)
            {
                _aiPlaneViewID = GetPrevAIID();
                _queuePrevViewId = false;
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

        private void DoAIPlaneDecoys()
        {
            if (_playerPlane.DroppingDecoy)
                DropDecoy(_playerPlane);

            var dropping = _aiPlanes.Where(p => p.DroppingDecoy).ToArray();

            if (dropping.Length == 0)
                return;

            for (int i = 0; i < dropping.Length; i++)
            {
                var plane = dropping[i];

                DropDecoy(plane);
            }
        }

        private void DrawPlaneAndObjects(RenderContext ctx, Plane plane)
        {
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
            ctx.PushViewPort(viewPortRect);

            // Draw the ground.
            ctx.Gfx.FillRectangle(new D2DRect(new D2DPoint(plane.Position.X, 2000f), new D2DSize(this.Width * World.ViewPortScaleMulti, 4000f)), D2DColor.DarkGreen);

            //_targets.ForEach(o => o.Render(ctx));

            _targets.ForEach(o =>
            {
                o.Render(ctx);

                if (o is Plane tplane && tplane != plane)
                {
                    ctx.Gfx.DrawEllipse(new D2DEllipse(tplane.Position, new D2DSize(80f, 80f)), _hudColor, 2f);
                }
            });

            _missiles.ForEach(o => o.Render(ctx));
            _missileTrails.ForEach(o => o.Render(ctx));
            _decoys.ForEach(o => o.Render(ctx));

            plane.Render(ctx);

            _bullets.ForEach(o => o.Render(ctx));
            _explosions.ForEach(o => o.Render(ctx));

            //DrawNearObj(_ctx.Gfx, plane);

            ctx.PopViewPort();
            ctx.Gfx.PopTransform();
        }

        private void DrawHud(RenderContext ctx, D2DSize viewportsize, Plane viewPlane)
        {
            DrawAltimeter(ctx.Gfx, viewportsize, viewPlane);
            DrawSpeedo(ctx.Gfx, viewportsize, viewPlane);
            DrawGMeter(ctx.Gfx, viewportsize, viewPlane);
            DrawThrottle(ctx.Gfx, viewportsize, viewPlane);
            DrawStats(ctx.Gfx, viewportsize, viewPlane);

            if (!viewPlane.IsDamaged)
            {
                if (viewPlane.IsAI == false)
                {
                    DrawGuideIcon(ctx.Gfx, viewportsize);
                }

                DrawHudMessage(ctx.Gfx, viewportsize);
                DrawTargetPointers(ctx.Gfx, viewportsize, viewPlane);
                DrawMissilePointers(ctx.Gfx, viewportsize, viewPlane);
            }

            DrawRadar(ctx, viewportsize, viewPlane);

        }

        private void DrawGuideIcon(D2DGraphics gfx, D2DSize viewportsize)
        {
            const float DIST = 300f;
            var pos = new D2DPoint(viewportsize.width * 0.5f, viewportsize.height * 0.5f);

            var mouseAngle = _guideAngle;
            var mouseVec = Helpers.AngleToVectorDegrees(mouseAngle, DIST);
            gfx.DrawEllipse(new D2DEllipse(pos + mouseVec, new D2DSize(5f, 5f)), _hudColor, 2f);

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

        private void DrawThrottle(D2DGraphics gfx, D2DSize viewportsize, Plane plane)
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

            var throtRect = new D2DRect(pos.X - (W * 0.5f), pos.Y - (H * 0.5f), W, (H * plane.ThrustAmount));
            gfx.RotateTransform(180f, pos);
            gfx.FillRectangle(throtRect, _hudColor);

            gfx.PopTransform();
        }

        private void DrawStats(D2DGraphics gfx, D2DSize viewportsize, Plane plane)
        {
            const float W = 20f;
            const float H = 50f;
            const float xPos = 80f;
            const float yPos = 110f;
            var pos = new D2DPoint(xPos, (viewportsize.height * 0.5f) + yPos);

            var rect = new D2DRect(pos, new D2DSize(W, H));

            gfx.PushTransform();

            gfx.DrawTextCenter($"{plane.Hits}/{Plane.MAX_HITS}", _hudColor, _defaultFontName, 15f, new D2DRect(pos + new D2DPoint(0, 40f), new D2DSize(50f, 20f)));
            gfx.DrawTextCenter($"{plane.NumMissiles}", _hudColor, _defaultFontName, 15f, new D2DRect(pos + new D2DPoint(0, 70f), new D2DSize(50f, 20f)));

            gfx.PopTransform();
        }

        private void DrawGMeter(D2DGraphics gfx, D2DSize viewportsize, Plane plane)
        {
            const float xPos = 80f;
            var pos = new D2DPoint(xPos, viewportsize.height * 0.5f);
            var rect = new D2DRect(pos, new D2DSize(50, 20));

            gfx.DrawText($"G {Math.Round(plane.GForce, 1)}", _hudColor, _defaultFontName, 15f, rect);
        }

        private void DrawAltimeter(D2DGraphics gfx, D2DSize viewportsize, Plane plane)
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


        private void DrawSpeedo(D2DGraphics gfx, D2DSize viewportsize, Plane plane)
        {
            const float W = 80f;
            const float H = 400f;
            const float HalfW = W * 0.5f;
            const float HalfH = H * 0.5f;
            const float MARKER_STEP = 50f;//100f;
            const float xPos = 200f;
            var pos = new D2DPoint(xPos, viewportsize.height * 0.5f);
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

            var actualRect = new D2DRect(new D2DPoint(pos.X, pos.Y + HalfH + 20f), new D2DSize(60f, 20f));
            gfx.DrawTextCenter(Math.Round(spd, 0).ToString(), _hudColor, _defaultFontName, 15f, actualRect);
        }

        private void DrawTargetPointers(D2DGraphics gfx, D2DSize viewportsize, Plane plane)
        {
            const float MIN_DIST = 600f;
            const float MAX_DIST = 6000f;
            var pos = new D2DPoint(viewportsize.width * 0.5f, viewportsize.height * 0.5f);

            for (int i = 0; i < _targets.Count; i++)
            {
                var target = _targets[i] as Plane;

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

                if (plane.ClosingRate(target) > 0f)
                    gfx.DrawArrow(pos + (vec * 270f), pos + (vec * 250f), _hudColor, 2f);
                else
                    gfx.DrawArrow(pos + (vec * 250f), pos + (vec * 270f), _hudColor, 2f);
            }

            if (plane.Radar.HasLock)
            {
                var lockPos = pos + new D2DPoint(0f, -200f);
                var lRect = new D2DRect(lockPos, new D2DSize(120, 30));
                gfx.DrawTextCenter("LOCKED", _hudColor, _defaultFontName, 25f, lRect);

            }
        }

        private void DrawRadar(RenderContext ctx, D2DSize viewportsize, Plane plane)
        {
            var pos = new D2DPoint(viewportsize.width * 0.8f, viewportsize.height * 0.8f);
            plane.Radar.Position = pos;
            plane.Radar.Render(ctx);
        }

        private void DrawMissilePointers(D2DGraphics gfx, D2DSize viewportsize, Plane plane)
        {
            const float MIN_DIST = 3000f;
            const float MAX_DIST = 20000f;

            bool warningMessage = false;
            var pos = new D2DPoint(viewportsize.width * 0.5f, viewportsize.height * 0.5f);

            for (int i = 0; i < _missiles.Count; i++)
            {
                var missile = _missiles[i] as GuidedMissile;

                if (missile == null)
                    continue;

                if (missile.Owner.ID == plane.ID)
                    continue;

                if (missile.Target.ID != plane.ID)
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

                if (targ is Decoy)
                    continue;

                // Missiles
                for (int m = 0; m < _missiles.Count; m++)
                {
                    var missile = _missiles[m] as Missile;

                    if (missile.Owner.ID == targ.ID)
                        continue;

                    if (targ.CollidesWith(missile, out D2DPoint pos))
                    {
                        if (targ is Plane plane)
                        {
                            if (plane.IsAI)
                            {
                                var oPlane = missile.Owner as Plane;

                                if (!oPlane.IsAI && targ.ID != _playerPlane.ID && !plane.IsDamaged)
                                {
                                    _playerScore++;
                                    NewHudMessage("Splash!", D2DColor.GreenYellow);
                                    Log.Msg($"Dist Traveled: {missile.DistTraveled}");
                                }

                                Log.Msg("AI plane hit AI plane with missile.");
                            }

                            if (plane.IsAI == true)
                                plane.DoImpact(missile, pos);
                            else if (plane.IsAI == false && !_godMode)
                                plane.DoImpact(missile, pos);
                        }

                        missile.IsExpired = true;
                        AddExplosion(pos);
                    }
                }

                // Bullets
                for (int b = 0; b < _bullets.Count; b++)
                {
                    var bullet = _bullets[b];

                    if (bullet.Owner.ID == targ.ID)
                        continue;

                    if (targ.CollidesWith(bullet, out D2DPoint pos) && bullet.Owner.ID != targ.ID)
                    {
                        if (!targ.IsExpired)
                            AddExplosion(pos);

                        if (targ is Plane plane2)
                        {
                            if (!plane2.IsAI && targ.ID != _playerPlane.ID && !plane2.IsDamaged)
                                _playerScore++;

                            if (plane2.IsAI)
                                plane2.DoImpact(bullet, pos);

                            if (plane2 == _playerPlane && !_godMode)
                                plane2.DoImpact(bullet, pos);
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

                    if (missile.CollidesWith(bullet, out D2DPoint posb))
                    {
                        if (!missile.IsExpired)
                            AddExplosion(posb);

                        missile.IsExpired = true;
                        bullet.IsExpired = true;
                    }
                }

                if (missile.Owner.ID == _playerPlane.ID)
                    continue;

                if (_playerPlane.CollidesWith(missile, out D2DPoint pos))
                {
                    if (!_godMode)
                        _playerPlane.DoImpact(missile, pos);

                    Log.Msg($"Dist Traveled: {missile.DistTraveled}");

                    missile.IsExpired = true;
                    AddExplosion(_playerPlane.Position);
                }
            }


            //// Handle player plane vs bullets.
            //for (int b = 0; b < _bullets.Count; b++)
            //{
            //    var bullet = _bullets[b];

            //    if (bullet.Owner.ID == _playerPlane.ID)
            //        continue;

            //    if (_playerPlane.Contains(bullet, out D2DPoint pos))
            //    {
            //        if (!_playerPlane.IsExpired)
            //            AddExplosion(_playerPlane.Position);

            //        if (!_godMode)
            //            _playerPlane.DoImpact(bullet, pos);

            //        bullet.IsExpired = true;
            //    }
            //}

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
                        plane.SetOnFire();


                    if (!plane.HasCrashed)
                    {
                        var pointingRight = Helpers.IsPointingRight(plane.Rotation);
                        if (pointingRight)
                            plane.Rotation = 0f;
                        else
                            plane.Rotation = 180f;
                    }

                    plane.IsDamaged = true;
                    plane.DoHitGround();
                    plane.SASOn = false;
                    //plane.Velocity = D2DPoint.Zero;

                    plane.Velocity *= new D2DPoint(0.998f, 0f);
                    plane.Position = new D2DPoint(plane.Position.X, 0f);
                    plane.RotationSpeed = 0f;
                }

                if (plane.IsExpired)
                    _aiPlanes.RemoveAt(a);
            }

            // Player plane.
            if (_playerPlane.Altitude <= 0f)
            {
                if (!_playerPlane.HasCrashed)
                    _playerDeaths++;

                if (!_playerPlane.IsDamaged)
                    _playerPlane.SetOnFire();

                if (!_playerPlane.HasCrashed)
                {
                    var pointingRight = Helpers.IsPointingRight(_playerPlane.Rotation);
                    if (pointingRight)
                        _playerPlane.Rotation = 0f;
                    else
                        _playerPlane.Rotation = 180f;
                }

                _playerPlane.IsDamaged = true;
                _playerPlane.DoHitGround();
                _playerPlane.SASOn = false;
                _playerPlane.AutoPilotOn = false;
                _playerPlane.ThrustOn = false;
                _playerPlane.Position = new D2DPoint(_playerPlane.Position.X, 0f);
                _playerPlane.RotationSpeed = 0f;
                _playerPlane.Velocity *= new D2DPoint(0.998f, 0f);
            }
        }

        private void PruneExpiredObj()
        {
            if (_clearObjs)
            {
                _clearObjs = false;
                Clear();
            }

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

            for (int d = 0; d < _decoys.Count; d++)
            {
                var decoy = _decoys[d];

                if (decoy.IsExpired || decoy.Owner.IsExpired)
                    _decoys.RemoveAt(d);
            }
        }

        private void DropDecoy(Plane plane)
        {
            if (plane.IsDamaged)
                return;

            var decoy = new Decoy(plane);
            _newDecoys.Enqueue(decoy);
        }

        private bool MissileIsImpactThreat(Plane plane, Missile missile, float minImpactTime)
        {
            var navigationTime = Helpers.ImpactTime(plane, missile);
            var closingRate = plane.ClosingRate(missile);

            // Is it going to hit soon, and has positive closing rate and is actively targeting us?
            return (navigationTime < minImpactTime && closingRate > 0f && missile.Target == plane);
        }

        private void DoDecoySuccess()
        {
            // Test for decoy success.
            const float MIN_DECOY_FOV = 10f;
            var decoys = _decoys;

            bool groundScatter = false;

            for (int i = 0; i < _missiles.Count; i++)
            {
                var missile = _missiles[i] as GuidedMissile;
                var target = missile.Target as Plane;

                if (target == null)
                    continue;

                if (missile == null)
                    continue;

                // Decoys dont work if target is being painted.?
                //if (missile.Owner.IsObjInFOV(target, World.SENSOR_FOV * 0.25f))
                //    continue;

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

                    //if (missile.Owner.IsObjInFOV(target, World.SENSOR_FOV * 0.25f) && )
                    //    continue;

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

        private void QueueClear()
        {
            _clearObjs = true;
        }

        private void Clear()
        {
            _missiles.Clear();
            _missileTrails.Clear();
            _targets.Clear();
            _bullets.Clear();
            _explosions.Clear();
            _aiPlanes.Clear();
            _playerScore = 0;
            _playerDeaths = 0;
            _decoys.Clear();

            _newTargets.Enqueue(_playerPlane);
        }


        private void DrawOverlays(RenderContext ctx)
        {
            if (_showInfo)
                DrawInfo(ctx.Gfx, _infoPosition);

            //var center = new D2DPoint(World.ViewPortSize.width * 0.5f, World.ViewPortSize.height * 0.5f);
            //var angVec = Helpers.AngleToVectorDegrees(_testAngle);
            //gfx.DrawLine(center, center + (angVec * 100f), D2DColor.Red);


            if (World.EnableTurbulence || World.EnableWind)
                DrawWindAndTurbulenceOverlay(ctx);


            if (_playerPlane.IsDamaged)
                ctx.Gfx.FillRectangle(World.ViewPortRect, new D2DColor(0.2f, D2DColor.Red));

            //DrawFPSGraph(ctx);
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


        //private void DrawAIPlanesOverlay(RenderContext ctx)
        //{
        //    if (_aiPlaneViewIdx < 0 || _aiPlaneViewIdx > _aiPlanes.Count - 1)
        //        return;

        //    var plane = _aiPlanes[_aiPlaneViewIdx];

        //    var scale = 5f;
        //    var zAmt = World.ZoomScale;
        //    var pos = new D2DPoint(World.ViewPortSize.width * 0.85f, World.ViewPortSize.height * 0.20f);
        //    pos *= zAmt;

        //    ctx.Gfx.PushLayer(_missileOverlayLayer, new D2DRect(pos * World.ViewPortScaleMulti, new D2DSize(3000f, 3000f)));
        //    ctx.Gfx.Clear(_missileOverlayColor);

        //    ctx.Gfx.PushTransform();

        //    var offset = new D2DPoint(-plane.Position.X, -plane.Position.Y);
        //    offset *= zAmt;

        //    ctx.Gfx.ScaleTransform(scale, scale, plane.Position);
        //    ctx.Gfx.TranslateTransform(offset.X, offset.Y);
        //    ctx.Gfx.TranslateTransform(pos.X, pos.Y);

        //    var vp = new D2DRect(plane.Position, World.ViewPortSize);
        //    ctx.PushViewPort(vp);

        //    var test = vp.Contains(plane.Position);

        //    _targets.ForEach(t =>
        //    {
        //        if (t is Decoy d)
        //            d.Render(ctx);
        //    });

        //    _missiles.ForEach(m => m.Render(ctx));

        //    plane.Render(ctx);

        //    _flames.ForEach(f => f.Render(ctx));

        //    ctx.DrawText(plane.Altitude.ToString(), D2DColor.White, _defaultFontName, 10f, new D2DRect(plane.Position + new D2DPoint(20, 80), new D2DSize(100, 20)));
        //    ctx.DrawText(plane.Velocity.Length().ToString(), D2DColor.White, _defaultFontName, 10f, new D2DRect(plane.Position + new D2DPoint(20, 90), new D2DSize(100, 20)));

        //    ctx.PopViewPort();

        //    ctx.Gfx.PopTransform();
        //    ctx.Gfx.PopLayer();
        //}

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


                var vp = new D2DRect(missile.Position, World.ViewPortSize);
                ctx.PushViewPort(vp);

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

                ctx.PopViewPort();
                ctx.Gfx.PopTransform();
            }

            ctx.Gfx.PopLayer();
        }

        private void DrawSky(RenderContext ctx, Plane viewPlane)
        {
            const float barH = 20f;
            const float MAX_ALT = 50000f;

            var plrAlt = Math.Abs(viewPlane.Position.Y);
            if (viewPlane.Position.Y >= 0)
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

        private void DrawMovingBackground(RenderContext ctx, Plane viewPlane)
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
            var viewPlane = GetViewPlane();

            string infoText = string.Empty;
            infoText += $"Paused: {_isPaused}\n\n";


            var numObj = _missiles.Count + _targets.Count + _bullets.Count + _explosions.Count + _aiPlanes.Count;
            infoText += $"Num Objects: {numObj}\n";
            infoText += $"On Screen: {GraphicsExtensions.OnScreen}\n";
            infoText += $"Off Screen: {GraphicsExtensions.OffScreen}\n";
            infoText += $"AI Planes: {_aiPlanes.Count(p => !p.IsDamaged && !p.HasCrashed)}\n";


            infoText += $"FPS: {Math.Round(_renderFPS, 0)}\n";
            infoText += $"Update ms: {_updateTime.TotalMilliseconds}\n";
            infoText += $"Render ms: {_renderTime.TotalMilliseconds}\n";
            infoText += $"Collision ms: {_collisionTime.TotalMilliseconds}\n";

            infoText += $"Zoom: {Math.Round(World.ZoomScale, 2)}\n";
            infoText += $"DT: {Math.Round(World.DT, 4)}\n";
            infoText += $"AutoPilot: {(_playerPlane.AutoPilotOn ? "On" : "Off")}\n";
            infoText += $"Position: {_playerPlane?.Position}\n";
            infoText += $"Kills: {viewPlane.Kills}\n";
            infoText += $"Bullets (Fired/Hit): ({viewPlane.BulletsFired} / {viewPlane.BulletsHit}) \n";
            infoText += $"Missiles (Fired/Hit): ({viewPlane.MissilesFired} / {viewPlane.MissilesHit}) \n";
            infoText += $"Headshots: {viewPlane.Headshots}\n";

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

        private long GetNextAIID()
        {
            if (_aiPlaneViewID == -1 && _aiPlanes.Count > 0)
                return _aiPlanes.First().ID;


            long nextId = -1;
            for (int i = 0; i < _aiPlanes.Count; i++)
            {
                var plane = _aiPlanes[i];

                if (plane.ID == _aiPlaneViewID && i + 1 < _aiPlanes.Count)
                {
                    nextId = _aiPlanes[i + 1].ID;
                }
                else if (plane.ID == _aiPlaneViewID && i + 1 >= _aiPlanes.Count)
                {
                    nextId = 0;
                }
            }

            return nextId;
        }

        private long GetPrevAIID()
        {
            if (_aiPlaneViewID == -1 && _aiPlanes.Count > 0)
                return _aiPlanes.Last().ID;

            long nextId = -1;

            for (int i = 0; i < _aiPlanes.Count; i++)
            {
                var plane = _aiPlanes[i];

                if (plane.ID == _aiPlaneViewID && i - 1 >= 0)
                {
                    nextId = _aiPlanes[i - 1].ID;
                }
                else if (plane.ID == _aiPlaneViewID && i - 1 <= 0)
                {
                    nextId = _aiPlanes.Last().ID;
                }
            }

            return nextId;
        }

        private void PolyPlaneUI_KeyPress(object sender, KeyPressEventArgs e)
        {
            switch (e.KeyChar)
            {
                case 'a':
                    _playerPlane.AutoPilotOn = !_playerPlane.AutoPilotOn;
                    break;

                case 'b':
                    //_motionBlur = !_motionBlur;
                    //_trailsOn = false;
                    _skipRender = !_skipRender;
                    break;

                case 'c':
                    QueueClear();
                    break;

                case 'd':

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
                    //World.ShowMissileCloseup = !World.ShowMissileCloseup;
                    //_useMultiThread = !_useMultiThread;
                    _showInfo = !_showInfo;
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
                    {
                        _playerPlaneSlewPos = _playerPlane.Position;
                    }
                    else
                    {
                        _playerPlane.HasCrashed = false;
                        _godMode = false;
                    }

                    break;

                case 't':
                    break;

                case 'u':
                    SpawnAIPlane();
                    break;

                case 'y':
                    //_targetTypes = Helpers.CycleEnum(_targetTypes);
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
                    _queuePrevViewId = true;

                    break;
                case ']':

                    _queueNextViewId = true;
                    break;

            }
        }

        private void PolyPlaneUI_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _playerBurstTimer.Stop();
            }

            if (e.Button == MouseButtons.Right)
                _playerPlane.DroppingDecoy = false;
        }

        private void PolyPlaneUI_MouseDown(object sender, MouseEventArgs e)
        {
            switch (e.Button)
            {
                case MouseButtons.Left:
                    _playerBurstTimer.Start();
                    break;

                case MouseButtons.Right:
                    _playerPlane.DroppingDecoy = true;
                    break;

                case MouseButtons.Middle:
                    TargetLockedWithMissile();
                    break;

            }
        }

        private void PolyPlaneUI_MouseWheel(object? sender, MouseEventArgs e)
        {
            if (!_shiftDown)
            {
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
            var angle = center.AngleTo(pos);

            _guideAngle = angle;
            _playerPlane.SetAutoPilotAngle(angle);
        }

        private void PolyPlaneUI_FormClosing(object sender, FormClosingEventArgs e)
        {
            _gameThread?.Join(1000);
            //_renderThread.Wait(1000);


        }
    }
}