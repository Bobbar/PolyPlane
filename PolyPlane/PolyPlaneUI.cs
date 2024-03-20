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
        private bool _queueResetPlane = false;
        private bool _skipRender = false;
        private long _lastRenderTime = 0;
        private float _renderFPS = 0;
        private bool _useMultiThread = true;
        private bool _showInfo = true;
        private int _multiThreadNum = 4;
        private bool _netIDIsSet = false;

        private List<GameObject> _missiles = new List<GameObject>();
        private List<SmokeTrail> _missileTrails = new List<SmokeTrail>();
        private List<GameObject> _decoys = new List<GameObject>();
        private List<GameObjectPoly> _bullets = new List<GameObjectPoly>();
        private List<GameObject> _explosions = new List<GameObject>();
        private List<Plane> _planes = new List<Plane>();
        private List<GameObject> _updateObjects = new List<GameObject>();
        private List<GameObject> _expiredObjects = new List<GameObject>();

        private ConcurrentQueue<GameObject> _newDecoys = new ConcurrentQueue<GameObject>();
        private ConcurrentQueue<GameObject> _newMissiles = new ConcurrentQueue<GameObject>();
        private ConcurrentQueue<Plane> _newPlanes = new ConcurrentQueue<Plane>();

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
        private TimeSpan _netTime = new TimeSpan();
        private double _packetDelay = 0f;
        private long _frame = 0;

        private SmoothDouble _packetDelayAvg = new SmoothDouble(100);

        private Net.Client _client;

        private Random _rnd => Helpers.Rnd;

        public PolyPlaneUI()
        {
            InitializeComponent();

            this.MouseWheel += PolyPlaneUI_MouseWheel;
            this.Disposed += PolyPlaneUI_Disposed;

            _burstTimer.TriggerCallback = () => DoAIPlaneBursts();
            _decoyTimer.TriggerCallback = () => DoAIPlaneDecoys();
            _playerBurstTimer.TriggerCallback = () => _playerPlane.FireBullet(p => AddExplosion(p));


            _multiThreadNum = Environment.ProcessorCount - 2;


            InitGfx();


            DoNetGameSetup();

            StartGameThread();
        }

        private void PolyPlaneUI_Disposed(object? sender, EventArgs e)
        {
            StopRender();
            _gameThread.Join(100);

            _client?.Stop();
            _client?.Dispose();

            ENet.Library.Deinitialize();

            _device?.Dispose();
            _missileOverlayLayer?.Dispose();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            //InitGfx();

            //InitPlane();

            //DoNetGameSetup();

            //StartGameThread();

            //PauseRender();
        }

        private void InitPlane(bool asAI = false)
        {
            if (asAI)
            {
                _playerPlane = GetAIPlane();

            }
            else
            {
                _playerPlane = new Plane(new D2DPoint(this.Width * 0.5f, -5000f));
            }

            _playerPlane.PlayerID = World.GetNextPlayerId();

            _playerPlane.FireBulletCallback = b =>
            {
                _bullets.Add(b);

                if (World.IsNetGame)
                {
                    _client.SendNewBulletPacket(b);
                }
            };


            _playerPlane.AutoPilotOn = true;
            _playerPlane.ThrustOn = true;
            _playerPlane.Velocity = new D2DPoint(500f, 0f);

            _playerPlane.Radar = new Radar(_playerPlane, _hudColor, _missiles, _planes);

            _playerPlane.Radar.SkipFrames = World.PHYSICS_STEPS;

            _playerPlane.FireMissileCallback = (m) =>
            {
                _newMissiles.Enqueue(m);

                if (World.IsNetGame)
                {
                    _client.SendNewMissilePacket(m);
                }

            };

            if (World.IsNetGame)
                _newPlanes.Enqueue(_playerPlane);
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

            _playerPlane.Radar = new Radar(_playerPlane, _hudColor, _missiles, _planes);

            _playerPlane.Radar.SkipFrames = World.PHYSICS_STEPS;
        }

        private void TargetLockedWithMissile()
        {
            if (_playerPlane.Radar.HasLock && _playerPlane.Radar.LockedObj != null)
                _playerPlane.FireMissile(_playerPlane.Radar.LockedObj);
        }

        private Plane GetAIPlane()
        {
            var pos = new D2DPoint(_rnd.NextFloat(-(World.ViewPortSize.width * 4f), World.ViewPortSize.width * 4f), _rnd.NextFloat(-4000f, -17000f));

            var aiPlane = new Plane(pos, isAI: true);
            aiPlane.PlayerID = World.GetNextPlayerId();
            aiPlane.Radar = new Radar(aiPlane, _hudColor, _missiles, _planes);

            aiPlane.Radar.SkipFrames = World.PHYSICS_STEPS;

            aiPlane.FireMissileCallback = (m) =>
            {
                _newMissiles.Enqueue(m);

                if (World.IsNetGame)
                {
                    _client.SendNewMissilePacket(m);
                }

            };



            aiPlane.FireBulletCallback = b =>
            {
                _bullets.Add(b);

                if (World.IsNetGame)
                {
                    _client.SendNewBulletPacket(b);
                }
            };

            aiPlane.Velocity = new D2DPoint(400f, 0f);

            return aiPlane;
        }

        private void SpawnAIPlane()
        {
            var aiPlane = GetAIPlane();

            _newPlanes.Enqueue(aiPlane);
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

        private void DoNetGameSetup()
        {
            using (var config = new ClientServerConfigForm())
            {
                if (config.ShowDialog() == DialogResult.OK)
                {
                    ENet.Library.Initialize();
                    World.IsNetGame = true;

                    World.IsServer = false;
                    _client = new Net.Client(config.Port, config.IPAddress);
                    _client.Start();

                    this.Text += " - CLIENT";

                    InitPlane(config.IsAI);
                }
            }

            ResumeRender();
        }

        private void ServerUI_Disposed(object? sender, EventArgs e)
        {
            this.Dispose();
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

            if (World.IsNetGame)
                DoNetEvents();

            var viewPortRect = new D2DRect(_playerPlane.Position, new D2DSize((World.ViewPortSize.width / VIEW_SCALE), World.ViewPortSize.height / VIEW_SCALE));
            _ctx.Viewport = viewPortRect;

            if (_trailsOn || _motionBlur)
                _gfx.BeginRender();
            else
                _gfx.BeginRender(_clearColor);

            if (_motionBlur)
                _gfx.FillRectangle(World.ViewPortRect, _blurColor);

            Plane viewPlane = GetViewPlane();

            World.ViewID = viewPlane.ID;

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

                var localObjs = GetAllObjects(localOnly: true);
                var numObj = localObjs.Count;

                for (int i = 0; i < World.PHYSICS_STEPS; i++)
                {
                    _timer.Restart();

                    DoCollisions();

                    _timer.Stop();

                    _collisionTime += _timer.Elapsed;

                    _timer.Restart();

                    if (_useMultiThread)
                    {
                        localObjs.ForEachParallel(o => o.Update(partialDT, World.ViewPortSize, World.RenderScale), _multiThreadNum);
                    }
                    else
                    {
                        localObjs.ForEach(o => o.Update(partialDT, World.ViewPortSize, World.RenderScale));
                    }

                    _timer.Stop();
                    _updateTime += _timer.Elapsed;
                }

                _timer.Restart();

                var netObj = GetAllNetObjects();
                if (_useMultiThread)
                {
                    netObj.ForEachParallel(o => o.Update(World.DT, World.ViewPortSize, World.RenderScale), _multiThreadNum);
                }
                else
                {
                    netObj.ForEach(o => o.Update(World.DT, World.ViewPortSize, World.RenderScale));
                }

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

            PruneExpiredObj();

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
                //_godMode = true;
            }
        }


        private GameObject GetObjectById(GameID id)
        {
            var allObjs = GetAllObjects();
            foreach (var obj in allObjs)
            {
                if (obj.ID.Equals(id))
                    return obj;
            }

            return null;
        }

        private Plane GetNetPlane(GameID id, bool netOnly = true)
        {

            foreach (var plane in _planes)
            {
                if (netOnly && !plane.IsNetObject)
                    continue;

                if (plane.ID.Equals(id))
                    return plane as Plane;
            }

            return null;
        }

        private GuidedMissile GetNetMissile(GameID id)
        {
            foreach (var missile in _missiles)
            {
                if (missile.ID.Equals(id))
                    return missile as GuidedMissile;
            }

            return null;
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

        private List<GameObject> GetAllNetObjects()
        {
            _updateObjects.Clear();

            _updateObjects.AddRange(_missiles.Where(m => m.IsNetObject));
            _updateObjects.AddRange(_bullets.Where(b => b.IsNetObject));
            _updateObjects.AddRange(_planes.Where(p => p.IsNetObject));

            return _updateObjects;
        }

        private List<GameObject> GetAllObjects(bool localOnly = false)
        {
            _updateObjects.Clear();

            if (localOnly)
            {
                _updateObjects.AddRange(_missiles.Where(m => !m.IsNetObject));
                _updateObjects.AddRange(_bullets.Where(b => !b.IsNetObject));
                _updateObjects.AddRange(_planes.Where(p => !p.IsNetObject));

            }
            else
            {
                _updateObjects.AddRange(_missiles);
                _updateObjects.AddRange(_bullets);
                _updateObjects.AddRange(_planes);

            }

            return _updateObjects;
        }


        private void DrawNearObj(D2DGraphics gfx, Plane plane)
        {
            //_targets.ForEach(t =>
            //{
            //    if (t.IsObjNear(plane))
            //        gfx.FillEllipseSimple(t.Position, 5f, D2DColor.Red);

            //});

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
            var plane = _planes.Where(p => p.ID.PlayerID == id).FirstOrDefault();

            if (plane == null)
                return _playerPlane;

            return plane as Plane;
        }

        private Plane TryIDToPlane(GameID id)
        {
            var plane = _planes.Where(p => p.ID.Equals(id)).FirstOrDefault();

            return plane as Plane;
        }

       
        private void ProcessObjQueue()
        {
            //while (_newTargets.Count > 0)
            //{
            //    if (_newTargets.TryDequeue(out GameObject obj))
            //        _targets.Add(obj);
            //}

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

            bool newPlanes = _newPlanes.Count > 0;

            while (_newPlanes.Count > 0)
            {
                if (_newPlanes.TryDequeue(out Plane plane))
                    _planes.Add(plane);
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

            if (_queueResetPlane)
            {
                ResetPlane();
                _queueResetPlane = false;
            }
        }

        private void DoAIPlaneBurst(float dt)
        {
            if (_planes.Any(p => p.FiringBurst))
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
            var firing = _planes.Where(p => p.FiringBurst).ToArray();

            if (firing.Length == 0)
                return;

            for (int i = 0; i < firing.Length; i++)
            {
                var plane = firing[i];

                if (plane.ID.Equals(_playerPlane.ID))
                    continue;

                plane.FireBullet(p => AddExplosion(p));
            }
        }

        private void DoAIPlaneDecoys()
        {
            if (_playerPlane.DroppingDecoy)
                DropDecoy(_playerPlane);

            var dropping = _planes.Where(p => p.DroppingDecoy).ToArray();

            if (dropping.Length == 0)
                return;

            for (int i = 0; i < dropping.Length; i++)
            {
                var plane = dropping[i];

                DropDecoy(plane);
            }
        }

        private void NewHudMessage(string message, D2DColor color)
        {
            _hudMessage = message;
            _hudMessageColor = color;
            _hudMessageTimeout.Restart();
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
            if (World.IsNetGame && !World.IsServer)
            {
                HandleGroundImpacts();
                return;
            }

            // Targets/AI Planes vs missiles and bullets.
            for (int r = 0; r < _planes.Count; r++)
            {
                var targ = _planes[r] as Plane;

                if (targ == null)
                    continue;

                // Missiles
                for (int m = 0; m < _missiles.Count; m++)
                {
                    var missile = _missiles[m] as Missile;

                    if (missile.Owner.ID.Equals(targ.ID))
                        continue;

                    if (targ.CollidesWith(missile, out D2DPoint pos))
                    {
                        if (targ is Plane plane)
                        {
                            if (plane.IsAI)
                            {
                                var oPlane = missile.Owner as Plane;

                                if (!oPlane.IsAI && !targ.ID.Equals(_playerPlane.ID) && !plane.IsDamaged)
                                {
                                    _playerScore++;
                                    NewHudMessage("Splash!", D2DColor.GreenYellow);
                                    Log.Msg($"Dist Traveled: {missile.DistTraveled}");
                                }

                                Log.Msg("AI plane hit AI plane with missile.");
                            }


                            if (missile.IsExpired)
                                continue;

                            var impactResult = plane.GetImpactResult(missile, pos);
                            SendNetImpact(missile, plane, impactResult);

                        }

                        missile.IsExpired = true;
                        AddExplosion(pos);
                    }
                }

                // Bullets
                for (int b = 0; b < _bullets.Count; b++)
                {
                    var bullet = _bullets[b];

                    if (bullet.Owner.ID.Equals(targ.ID))
                        continue;

                    if (targ.CollidesWith(bullet, out D2DPoint pos) && !bullet.Owner.ID.Equals(targ.ID))
                    {
                        if (!targ.IsExpired)
                            AddExplosion(pos);

                        if (targ is Plane plane2)
                        {
                            if (!plane2.IsAI && !targ.ID.Equals(_playerPlane.ID) && !plane2.IsDamaged)
                                _playerScore++;

                            var impactResult = plane2.GetImpactResult(bullet, pos);
                            SendNetImpact(bullet, plane2, impactResult);


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

                if (missile.Owner.ID.Equals(_playerPlane.ID))
                    continue;

                if (_playerPlane.CollidesWith(missile, out D2DPoint pos))
                {
                    if (!_godMode)
                    {
                        var impactResult = _playerPlane.GetImpactResult(missile, pos);
                        SendNetImpact(missile, _playerPlane, impactResult);
                    }

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
            //PruneExpiredObj();
        }

        private void HandleGroundImpacts()
        {
            // AI Planes.
            for (int a = 0; a < _planes.Count; a++)
            {
                var plane = _planes[a];

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
                {
                    _expiredObjects.Add(missile);
                    _missiles.RemoveAt(o);
                }
            }

            for (int o = 0; o < _missileTrails.Count; o++)
            {
                var trail = _missileTrails[o];

                if (trail.IsExpired)
                    _missileTrails.RemoveAt(o);
            }

            for (int o = 0; o < _planes.Count; o++)
            {
                var plane = _planes[o];

                if (plane.IsExpired)
                {
                    _expiredObjects.Add(plane);
                    _planes.RemoveAt(o);
                }
            }


            //for (int o = 0; o < _targets.Count; o++)
            //{
            //    var targ = _targets[o];

            //    if (targ.IsExpired)
            //        _targets.RemoveAt(o);
            //}

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
            DoNetDecoy(decoy);
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
            //_targets.Clear();
            _bullets.Clear();
            _explosions.Clear();
            _planes.Clear();
            _playerScore = 0;
            _playerDeaths = 0;
            _decoys.Clear();

            _newPlanes.Enqueue(_playerPlane);
            //_newTargets.Enqueue(_playerPlane);
        }

        private string GetInfo()
        {
            var viewPlane = GetViewPlane();

            string infoText = string.Empty;
            infoText += $"Paused: {_isPaused}\n\n";


            var numObj = _missiles.Count + _bullets.Count + _explosions.Count + _planes.Count;
            infoText += $"Num Objects: {numObj}\n";
            infoText += $"On Screen: {GraphicsExtensions.OnScreen}\n";
            infoText += $"Off Screen: {GraphicsExtensions.OffScreen}\n";
            infoText += $"AI Planes: {_planes.Count(p => !p.IsDamaged && !p.HasCrashed)}\n";


            infoText += $"FPS: {Math.Round(_renderFPS, 0)}\n";
            infoText += $"Update ms: {_updateTime.TotalMilliseconds}\n";
            infoText += $"Render ms: {_renderTime.TotalMilliseconds}\n";
            infoText += $"Collision ms: {_collisionTime.TotalMilliseconds}\n";
            infoText += $"Packet Delay: {_packetDelay}\n";

            infoText += $"Zoom: {Math.Round(World.ZoomScale, 2)}\n";
            infoText += $"DT: {Math.Round(World.DT, 4)}\n";
            infoText += $"AutoPilot: {(viewPlane.AutoPilotOn ? "On" : "Off")}\n";
            infoText += $"Position: {viewPlane?.Position}\n";
            infoText += $"Kills: {viewPlane.Kills}\n";
            infoText += $"Bullets (Fired/Hit): ({viewPlane.BulletsFired} / {viewPlane.BulletsHit}) \n";
            infoText += $"Missiles (Fired/Hit): ({viewPlane.MissilesFired} / {viewPlane.MissilesHit}) \n";
            infoText += $"Headshots: {viewPlane.Headshots}\n";
            infoText += $"Interp: {World.InterpOn.ToString()}\n";

            return infoText;
        }

        private void DrawInfo(D2DGraphics gfx, D2DPoint pos)
        {
            var infoText = GetInfo();

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
            if (_aiPlaneViewID == -1 && _planes.Count > 0)
                return _planes.First().ID.PlayerID;


            long nextId = -1;
            for (int i = 0; i < _planes.Count; i++)
            {
                var plane = _planes[i];

                if (plane.ID.PlayerID == _aiPlaneViewID && i + 1 < _planes.Count)
                {
                    nextId = _planes[i + 1].ID.PlayerID;
                }
                else if (plane.ID.PlayerID == _aiPlaneViewID && i + 1 >= _planes.Count)
                {
                    nextId = 0;
                }
            }

            return nextId;
        }

        private long GetPrevAIID()
        {
            if (_aiPlaneViewID == -1 && _planes.Count > 0)
                return _planes.Last().ID.PlayerID;

            long nextId = -1;

            for (int i = 0; i < _planes.Count; i++)
            {
                var plane = _planes[i];

                if (plane.ID.PlayerID == _aiPlaneViewID && i - 1 >= 0)
                {
                    nextId = _planes[i - 1].ID.PlayerID;
                }
                else if (plane.ID.PlayerID == _aiPlaneViewID && i - 1 <= 0)
                {
                    nextId = _planes.Last().ID.PlayerID;
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
                    World.InterpOn = !World.InterpOn;
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

                    var viewPlane = GetViewPlane();
                    if (viewPlane != null)
                        viewPlane.IsDamaged = true;

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
                    //ResetPlane();
                    _queueResetPlane = true;
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
                _playerPlane.FiringBurst = false;

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
                    _playerPlane.FiringBurst = true;
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