using ENet;
using PolyPlane.GameObjects;
using PolyPlane.Net;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
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
        private Net.Server _server;
        private ServerUI _serverUI;
        private WaitableTimer _waitTimer = new WaitableTimer();
        private Stopwatch _fpsTimer = new Stopwatch();

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

            InitPlane();

            DoNetGameSetup();

            StartGameThread();
        }

        private void PolyPlaneUI_Disposed(object? sender, EventArgs e)
        {
            StopRender();
            _gameThread.Join(100);

            _serverUI?.Dispose();
            _client?.Stop();
            _server?.Stop();

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

        private void InitPlane()
        {
            _playerPlane = new Plane(new D2DPoint(this.Width * 0.5f, -5000f));
            _playerPlane.PlayerID = World.GetNextPlayerId();
            //_playerPlane.FireBulletCallback = b => { _bullets.Add(b); };

            _playerPlane.FireBulletCallback = b =>
            {
                _bullets.Add(b);

                if (World.IsNetGame)
                {
                    if (World.IsServer)
                    {
                        _server.SendNewBulletPacket(b);

                    }
                    else
                    {
                        _client.SendNewBulletPacket(b);
                    }
                }
            };


            _playerPlane.AutoPilotOn = true;
            _playerPlane.ThrustOn = true;
            _playerPlane.Velocity = new D2DPoint(500f, 0f);

            //_playerPlane.Radar = new Radar(_playerPlane, _hudColor, _targets, _missiles);
            //_playerPlane.Radar = new Radar(_playerPlane, _hudColor, _targets, _missiles, _netPlanes);
            _playerPlane.Radar = new Radar(_playerPlane, _hudColor, _missiles, _planes);

            _playerPlane.Radar.SkipFrames = World.PHYSICS_STEPS;

            //_playerPlane.FireMissileCallback = (m) => _newMissiles.Enqueue(m);
            _playerPlane.FireMissileCallback = (m) =>
            {
                _newMissiles.Enqueue(m);

                if (World.IsNetGame)
                {
                    if (World.IsServer)
                    {
                        _server.SendNewMissilePacket(m);

                    }
                    else
                    {
                        _client.SendNewMissilePacket(m);
                    }
                }

            };


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

        private void SpawnAIPlane()
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
                    if (World.IsServer)
                    {
                        _server.SendNewMissilePacket(m);

                    }
                    else
                    {
                        _client.SendNewMissilePacket(m);
                    }
                }

            };



            aiPlane.FireBulletCallback = b =>
            {
                _bullets.Add(b);

                if (World.IsNetGame)
                {
                    if (World.IsServer)
                    {
                        _server.SendNewBulletPacket(b);
                    }
                    else
                    {
                        _client.SendNewBulletPacket(b);
                    }
                }
            };




            aiPlane.Velocity = new D2DPoint(400f, 0f);

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
                    if (config.IsServer)
                    {
                        World.IsServer = true;
                        _server = new Net.Server(config.Port, config.IPAddress);
                        _server.Start();

                        this.Text += " - SERVER";

                        _serverUI = new ServerUI();
                        _serverUI.Show();
                        _serverUI.Disposed += ServerUI_Disposed;
                        this.Hide();
                        this.WindowState = FormWindowState.Minimized;
                    }
                    else
                    {
                        World.IsServer = false;
                        _client = new Net.Client(config.Port, config.IPAddress);
                        _client.Start();

                        this.Text += " - CLIENT";

                    }
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

                if (World.IsNetGame && World.IsServer)
                {
                    AdvanceServer();
                }
                else
                {
                    AdvanceAndRender();

                }

                if (!_pauseRenderEvent.Wait(0))
                {
                    _isPaused = true;
                    _pauseRenderEvent.Set();
                }
            }
        }


        private void AdvanceServer()
        {
            _updateTime = TimeSpan.Zero;
            _collisionTime = TimeSpan.Zero;

            ProcessObjQueue();

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


            DoNetEvents();

            DoCollisions();
            PruneExpiredObj();

            var fpsNow = DateTime.UtcNow.Ticks;
            var fps = TimeSpan.TicksPerSecond / (float)(fpsNow - _lastRenderTime);
            _lastRenderTime = fpsNow;
            _renderFPS = fps;


            _serverUI.InfoText = GetInfo();

            if (_serverUI.PauseRequested)
            {
                if (!_isPaused)
                    _isPaused = true;
                else
                    _isPaused = false;

                _serverUI.PauseRequested = false;
            }

            if (_serverUI.SpawnIAPlane)
            {
                SpawnAIPlane();
                _serverUI.SpawnIAPlane = false;
            }

            FPSLimiter(60);
        }

        private void FPSLimiter(int targetFPS)
        {
            long ticksPerSecond = TimeSpan.TicksPerSecond;
            long targetFrameTime = ticksPerSecond / targetFPS;
            long waitTime = 0;

            if (_fpsTimer.IsRunning)
            {
                long elapTime = _fpsTimer.Elapsed.Ticks;

                if (elapTime < targetFrameTime)
                {
                    // # High accuracy, low CPU usage. #
                    waitTime = (long)(targetFrameTime - elapTime);
                    if (waitTime > 0)
                    {
                        _waitTimer.Wait(waitTime, false);
                    }

                    // # Most accurate, highest CPU usage. #
                    //while (_fpsTimer.Elapsed.Ticks < targetFrameTime && !_loopTask.IsCompleted)
                    //{
                    //	Thread.SpinWait(10000);
                    //}
                    //elapTime = _fpsTimer.Elapsed.Ticks;

                    // # Less accurate, less CPU usage. #
                    //waitTime = (long)(targetFrameTime - elapTime);
                    //if (waitTime > 0)
                    //{
                    //	Thread.Sleep(new TimeSpan(waitTime));
                    //}
                }

                _fpsTimer.Restart();
            }
            else
            {
                _fpsTimer.Start();
                return;
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

            if (viewPlane == null)
                Debugger.Break();

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

        private void DoNetEvents()
        {
            _frame++;

            double now = 0;
            double totalPacketTime = 0;
            int numPackets = 0;

            // Send plane & missile updates every other frame.
            if (_frame % 2 == 0)
            {
                SendPlaneUpdates();
                SendMissileUpdates();
            }

            SendExpiredObjects();

            if (World.IsServer)
            {
                now = _server.CurrentTime;

                while (_server.PacketReceiveQueue.Count > 0)
                {
                    if (_server.PacketReceiveQueue.TryDequeue(out Net.NetPacket packet))
                    {
                        totalPacketTime += now - packet.FrameTime;
                        numPackets++;

                        HandleNetPacket(packet);
                    }
                }
            }
            else
            {
                now = _client.CurrentTime;

                while (_client.PacketReceiveQueue.Count > 0)
                {
                    if (_client.PacketReceiveQueue.TryDequeue(out NetPacket packet))
                    {
                        totalPacketTime += now - packet.FrameTime;
                        numPackets++;

                        HandleNetPacket(packet);
                    }
                }
            }

            if (totalPacketTime > 0f && numPackets > 0)
            {
                var avgDelay = (totalPacketTime / (float)numPackets);
                _packetDelay = _packetDelayAvg.Add(avgDelay);
            }
        }

        private void SendExpiredObjects()
        {
            var expiredObjPacket = new Net.BasicListPacket();
            _expiredObjects.ForEach(o => expiredObjPacket.Packets.Add(new BasicPacket(PacketTypes.ExpiredObjects, o.ID)));

            if (expiredObjPacket.Packets.Count == 0)
                return;

            if (World.IsServer)
                _server.EnqueuePacket(expiredObjPacket);
            else
                _client.EnqueuePacket(expiredObjPacket);

            _expiredObjects.Clear();
        }

        private void SendPlaneUpdates()
        {
            var newPlanesPacket = new Net.PlaneListPacket();

            if (World.IsServer)
            {
                foreach (var plane in _planes)
                {
                    var planePacket = new Net.PlanePacket(plane);
                    newPlanesPacket.Planes.Add(planePacket);
                }

                _server.EnqueuePacket(newPlanesPacket);
            }
            else
            {
                var planePacket = new Net.PlanePacket(_playerPlane);
                newPlanesPacket.Planes.Add(planePacket);

                _client.EnqueuePacket(newPlanesPacket);
            }

        }

        private void SendMissileUpdates()
        {
            var newMissilesPacket = new Net.MissileListPacket();

            if (World.IsServer)
            {
                _missiles.ForEach(m => newMissilesPacket.Missiles.Add(new MissilePacket(m as GuidedMissile)));

                if (newMissilesPacket.Missiles.Count > 0)
                    _server.EnqueuePacket(newMissilesPacket);
            }
            else
            {
                var missiles = _missiles.Where(m => m.PlayerID == _playerPlane.PlayerID).ToList();
                missiles.ForEach(m => newMissilesPacket.Missiles.Add(new MissilePacket(m as GuidedMissile)));

                if (newMissilesPacket.Missiles.Count > 0)
                    _client.EnqueuePacket(newMissilesPacket);
            }
        }

        private void DoNetPlaneUpdates(PlaneListPacket listPacket)
        {
            if (!World.IsServer && !_netIDIsSet)
                return;

            foreach (var planeUpdPacket in listPacket.Planes)
            {

                var netPlane = GetNetPlane(planeUpdPacket.ID);

                if (netPlane != null)
                {
                    var newPos = planeUpdPacket.Position.ToD2DPoint();

                    planeUpdPacket.SyncObj(netPlane);
                    netPlane.ThrustAmount = planeUpdPacket.ThrustAmt;
                    netPlane.Deflection = planeUpdPacket.Deflection;

                    netPlane.NetUpdate(World.DT, World.ViewPortSize, World.RenderScale, newPos, planeUpdPacket.Velocity.ToD2DPoint(), planeUpdPacket.Rotation, planeUpdPacket.FrameTime);
                }
            }
        }

        private void DoNetMissileUpdates(MissileListPacket listPacket)
        {
            foreach (var missileUpdate in listPacket.Missiles)
            {
                var netMissile = GetNetMissile(missileUpdate.ID);

                if (netMissile != null)
                {
                    var netMissileOwner = GetNetPlane(netMissile.Owner.ID, false);

                    if (netMissileOwner != null && netMissileOwner.IsNetObject)
                    {
                        missileUpdate.SyncObj(netMissile);

                        netMissile.NetUpdate(World.DT, World.ViewPortSize, World.RenderScale, missileUpdate.Position.ToD2DPoint(), missileUpdate.Velocity.ToD2DPoint(), missileUpdate.Rotation, missileUpdate.FrameTime);
                    }
                }
            }
        }

        private void DoNewBullet(BulletPacket bulletPacket)
        {
            var bullet = new Bullet(bulletPacket.Position.ToD2DPoint(), bulletPacket.Velocity.ToD2DPoint(), bulletPacket.Rotation);
            bullet.ID = bulletPacket.ID;
            bulletPacket.SyncObj(bullet);
            var owner = GetNetPlane(bulletPacket.OwnerID);
            bullet.Owner = owner;

            var contains = _bullets.Any(b => b.ID.Equals(bullet.ID));

            if (!contains)
                _bullets.Add(bullet);
        }

        private void DoNewMissile(MissilePacket missilePacket)
        {
            var missileOwner = GetNetPlane(missilePacket.OwnerID);

            if (missileOwner != null)
            {
                if (missileOwner.ID.Equals(_playerPlane.ID))
                    return;

                var missileTarget = GetNetPlane(missilePacket.TargetID, false);

                var missile = new GuidedMissile(missileOwner, missilePacket.Position.ToD2DPoint(), missilePacket.Velocity.ToD2DPoint(), missilePacket.Rotation);
                missile.IsNetObject = true;
                missile.ID = missilePacket.ID;
                missilePacket.SyncObj(missile);
                missile.Target = missileTarget;
                _newMissiles.Enqueue(missile);
            }

        }

        private void DoNewDecoy(DecoyPacket decoyPacket)
        {
            var decoyOwner = GetNetPlane(decoyPacket.OwnerID);

            if (decoyOwner != null)
            {
                var decoy = new Decoy(decoyOwner);
                decoy.ID = decoyPacket.ID;
                decoyPacket.SyncObj(decoy);

                bool containsDecoy = _decoys.Any(d => d.ID.Equals(decoy.ID));

                if (!containsDecoy)
                {
                    _decoys.Add(decoy);

                    if (World.IsServer)
                        _server.EnqueuePacket(decoyPacket);
                }
            }
        }

        private void SendNetImpact(GameObject impactor, GameObject target, PlaneImpactResult result)
        {
            if (!World.IsNetGame)
                return;

            var impactPacket = new Net.ImpactPacket(target, impactor.ID, result.ImpactPoint, result.DoesDamage, result.WasHeadshot, result.Type == ImpactType.Missile);

            if (World.IsServer)
            {
                _server.EnqueuePacket(impactPacket);
                DoNetImpact(impactPacket);
            }
            else
                _client.EnqueuePacket(impactPacket);

        }

        private void DoNetImpact(ImpactPacket packet)
        {
            if (packet != null)
            {
                GameObject impactor = null;
                var impactorMissile = _missiles.Where(m => m.ID.Equals(packet.ImpactorID)).FirstOrDefault();
                var impactorBullet = _bullets.Where(b => b.ID.Equals(packet.ImpactorID)).FirstOrDefault();

                if (impactorMissile != null)
                    impactor = impactorMissile;

                if (impactorMissile == null && impactorBullet != null)
                    impactor = impactorBullet;

                if (impactor == null)
                    return;

                impactor.IsExpired = true;

                var target = _planes.Where(p => p.ID.Equals(packet.ID)).FirstOrDefault() as Plane;

                if (target != null)
                {
                    // Move the plane to the server position, do the impact, then move it back.
                    // This is to make sure the impacts/bullet holes show up in the correct place.
                    var curRot = target.Rotation;
                    var curVelo = target.Velocity;
                    var curPos = target.Position;

                    target.Rotation = packet.Rotation;
                    target.Velocity = packet.Velocity.ToD2DPoint();
                    target.Position = packet.Position.ToD2DPoint();

                    var impactPoint = packet.ImpactPoint.ToD2DPoint();
                    target.DoNetImpact(impactor, impactPoint, packet.DoesDamage, packet.WasHeadshot, packet.WasMissile);

                    target.Rotation = curRot;
                    target.Velocity = curVelo;
                    target.Position = curPos;

                    AddExplosion(impactPoint);
                }
            }
        }



        private void DoNetDecoy(Decoy decoy)
        {
            if (!World.IsNetGame)
                return;

            var decoyPacket = new Net.DecoyPacket(decoy);

            if (World.IsServer)
                _server.EnqueuePacket(decoyPacket);
            else
                _client.EnqueuePacket(decoyPacket);
        }

        private void ServerSendOtherPlanes()
        {
            var otherPlanesPackets = new List<Net.PlanePacket>();
            foreach (var plane in _planes)
                otherPlanesPackets.Add(new Net.PlanePacket(plane as Plane));
            var listPacket = new Net.PlaneListPacket(otherPlanesPackets);
            listPacket.Type = PacketTypes.GetOtherPlanes;

            _server.EnqueuePacket(listPacket);
            //_server.SyncOtherPlanes(listPacket);
        }

        private void HandleNetPacket(NetPacket packet)
        {
            switch (packet.Type)
            {
                case PacketTypes.PlaneUpdate:

                    if (!World.IsServer && !_netIDIsSet)
                        return;

                    var updPacket = packet as PlaneListPacket;
                    DoNetPlaneUpdates(updPacket);

                    break;
                case PacketTypes.MissileUpdate:

                    var missilePacket = packet as MissileListPacket;
                    DoNetMissileUpdates(missilePacket);

                    break;
                case PacketTypes.Impact:

                    var impactPacket = packet as ImpactPacket;
                    DoNetImpact(impactPacket);

                    break;
                case PacketTypes.NewPlayer:

                    if (World.IsServer)
                    {
                        var planePacket = packet as PlanePacket;

                        if (planePacket != null)
                        {
                            var newPlane = new Plane(planePacket.Position.ToD2DPoint(), planePacket.PlaneColor);
                            newPlane.ID = planePacket.ID;
                            planePacket.SyncObj(newPlane);
                            newPlane.IsNetObject = true;
                            newPlane.Radar = new Radar(newPlane, _hudColor, _missiles, _planes);
                            _planes.Add(newPlane);
                        }

                        ServerSendOtherPlanes();
                    }

                    break;
                case PacketTypes.NewBullet:

                    var bulletPacket = packet as BulletPacket;
                    DoNewBullet(bulletPacket);

                    break;
                case PacketTypes.NewMissile:

                    var newMissilePacket = packet as MissilePacket;
                    DoNewMissile(newMissilePacket);

                    break;
                case PacketTypes.NewDecoy:

                    var newDecoyPacket = packet as DecoyPacket;
                    DoNewDecoy(newDecoyPacket);

                    break;
                case PacketTypes.SetID:

                    if (!World.IsServer)
                    {
                        _netIDIsSet = true;

                        _playerPlane.PlayerID = packet.ID.PlayerID;

                        _client.SendNewPlanePacket(_playerPlane);
                    }

                    break;
                case PacketTypes.GetNextID:
                    // Nuttin...
                    break;
                case PacketTypes.ChatMessage:
                    // Nuttin...
                    break;
                case PacketTypes.GetOtherPlanes:

                    if (World.IsServer)
                    {
                        ServerSendOtherPlanes();
                    }
                    else
                    {
                        var listPacket = packet as Net.PlaneListPacket;

                        foreach (var plane in listPacket.Planes)
                        {
                            var existing = TryIDToPlane(plane.ID);

                            if (existing == null)
                            {
                                var newPlane = new Plane(plane.Position.ToD2DPoint(), plane.PlaneColor);
                                newPlane.ID = plane.ID;
                                newPlane.IsNetObject = true;
                                newPlane.Radar = new Radar(newPlane, _hudColor, _missiles, _planes);
                                _planes.Add(newPlane);
                            }
                        }
                    }

                    break;

                case PacketTypes.ExpiredObjects:
                    var expiredPacket = packet as Net.BasicListPacket;

                    foreach (var p in expiredPacket.Packets)
                    {
                        var obj = GetObjectById(p.ID);

                        if (obj != null)
                            obj.IsExpired = true;
                    }

                    break;
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


            //foreach (var plane in _planes)
            //{
            //    if (plane.ID.Equals(id)) 
            //        return plane as Plane;
            //}


            //foreach (var plane in _netPlanes)
            //{
            //    if (plane.ID.Equals(id))
            //        return plane as Plane;
            //}

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


        //private Plane GetNetPlane(long id)
        //{
        //    foreach (var plane in _netPlanes)
        //    {
        //        if (plane.ID == id)
        //            return plane as Plane;
        //    }

        //    return null;
        //}

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


            if (World.IsServer)
            {
                var plrIdx = _updateObjects.IndexOf(_playerPlane);
                if (plrIdx != -1)
                    _updateObjects.RemoveAt(plrIdx);
            }


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

            if (World.IsServer)
            {
                var plrIdx = _updateObjects.IndexOf(_playerPlane);
                _updateObjects.RemoveAt(plrIdx);
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

        //private Plane TryIDToPlane(long id)
        //{
        //    var plane = _aiPlanes.Where(p => p.ID == id).FirstOrDefault();

        //    return plane;
        //}

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

            if (World.IsNetGame && World.IsServer && newPlanes)
                ServerSendOtherPlanes();

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

            _decoys.ForEach(o => o.Render(ctx));
            _missiles.ForEach(o => o.Render(ctx));
            _missileTrails.ForEach(o => o.Render(ctx));

            _planes.ForEach(o =>
            {
                if (o is Plane tplane && !tplane.ID.Equals(plane.ID))
                {
                    o.Render(ctx);
                    ctx.Gfx.DrawEllipse(new D2DEllipse(tplane.Position, new D2DSize(80f, 80f)), _hudColor, 2f);
                }
            });

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
                DrawPlanePointers(ctx.Gfx, viewportsize, viewPlane);
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

        private void DrawPlanePointers(D2DGraphics gfx, D2DSize viewportsize, Plane plane)
        {
            const float MIN_DIST = 600f;
            const float MAX_DIST = 6000f;
            var pos = new D2DPoint(viewportsize.width * 0.5f, viewportsize.height * 0.5f);

            for (int i = 0; i < _planes.Count; i++)
            {
                var target = _planes[i];

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

                if (missile.Owner.ID.Equals(plane.ID))
                    continue;

                if (!missile.Target.ID.Equals(plane.ID))
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
            if (World.IsNetGame && !World.IsServer)
            {
                HandleGroundImpacts();
                return;
            }

            // Targets/AI Planes vs missiles and bullets.
            for (int r = 0; r < _planes.Count; r++)
            {
                var targ = _planes[r] as GameObjectPoly;

                if (targ == null)
                    continue;

                if (targ is Decoy)
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

        //private void DrawMissileTargetOverlays(RenderContext ctx)
        //{
        //    var plrMissiles = _missiles.Where(m => m.Owner.ID.Equals(_playerPlane.ID)).ToArray();
        //    if (plrMissiles.Length == 0)
        //        return;

        //    var scale = 5f;
        //    var zAmt = World.ZoomScale;
        //    var pos = new D2DPoint(World.ViewPortSize.width * 0.85f, World.ViewPortSize.height * 0.20f);
        //    pos *= zAmt;

        //    ctx.Gfx.PushLayer(_missileOverlayLayer, new D2DRect(pos * World.ViewPortScaleMulti, new D2DSize(1000f, 1000f)));
        //    ctx.Gfx.Clear(_missileOverlayColor);

        //    for (int m = 0; m < plrMissiles.Length; m++)
        //    {
        //        var missile = plrMissiles[m] as GuidedMissile;
        //        var target = missile.Target as Plane;

        //        if (target == null)
        //            continue;

        //        if (!missile.Owner.ID.Equals(_playerPlane.ID))
        //            continue;

        //        ctx.Gfx.PushTransform();

        //        var offset = new D2DPoint(-target.Position.X, -target.Position.Y);
        //        offset *= zAmt;

        //        ctx.Gfx.ScaleTransform(scale, scale, target.Position);
        //        ctx.Gfx.TranslateTransform(offset.X, offset.Y);
        //        ctx.Gfx.TranslateTransform(pos.X, pos.Y);

        //        target.Render(ctx);

        //        //for (int t = 0; t < _targets.Count; t++)
        //        //    _targets[t].Render(gfx);

        //        //missile.Render(gfx);

        //        _targets.ForEach(t =>
        //        {
        //            if (t is Decoy d)
        //                d.Render(ctx);

        //        });

        //        var dist = D2DPoint.Distance(missile.Position, missile.Target.Position);

        //        //gfx.DrawText(missile.Velocity.Length().ToString(), D2DColor.White, _defaultFontName, 20f, new D2DRect(pos - new D2DPoint(0,0),new D2DSize(500,500)));
        //        ctx.DrawText(Math.Round(missile.Velocity.Length(), 1).ToString(), D2DColor.White, _defaultFontName, 10f, new D2DRect(missile.Position + new D2DPoint(80, 80), new D2DSize(50, 20)));
        //        ctx.DrawText(Math.Round(dist, 1).ToString(), D2DColor.White, _defaultFontName, 10f, new D2DRect(missile.Position - new D2DPoint(60, -80), new D2DSize(50, 20)));

        //        ctx.Gfx.PopTransform();
        //    }

        //    ctx.Gfx.PopLayer();
        //}

        //private void DrawMissileOverlays(RenderContext ctx)
        //{
        //    var plrMissiles = _missiles.Where(m => m.Owner.ID.Equals(_playerPlane.ID)).ToArray();
        //    if (plrMissiles.Length == 0)
        //        return;

        //    var scale = 5f;
        //    var zAmt = World.ZoomScale;
        //    var pos = new D2DPoint(World.ViewPortSize.width * 0.85f, World.ViewPortSize.height * 0.20f);
        //    pos *= zAmt;

        //    ctx.Gfx.PushLayer(_missileOverlayLayer, new D2DRect(pos * World.ViewPortScaleMulti, new D2DSize(1000f, 1000f)));
        //    ctx.Gfx.Clear(_missileOverlayColor);

        //    for (int m = 0; m < plrMissiles.Length; m++)
        //    {
        //        var missile = plrMissiles[m] as GuidedMissile;

        //        if (!missile.Owner.ID.Equals(_playerPlane.ID))
        //            continue;


        //        var vp = new D2DRect(missile.Position, World.ViewPortSize);
        //        ctx.PushViewPort(vp);

        //        ctx.Gfx.PushTransform();

        //        var offset = new D2DPoint(-missile.Position.X, -missile.Position.Y);
        //        offset *= zAmt;

        //        ctx.Gfx.ScaleTransform(scale, scale, missile.Position);
        //        ctx.Gfx.TranslateTransform(offset.X, offset.Y);
        //        ctx.Gfx.TranslateTransform(pos.X, pos.Y);

        //        for (int t = 0; t < _targets.Count; t++)
        //            _targets[t].Render(ctx);

        //        missile.Render(ctx);

        //        var dist = D2DPoint.Distance(missile.Position, missile.Target.Position);

        //        //gfx.DrawText(missile.Velocity.Length().ToString(), D2DColor.White, _defaultFontName, 20f, new D2DRect(pos - new D2DPoint(0,0),new D2DSize(500,500)));
        //        ctx.DrawText(Math.Round(missile.Velocity.Length(), 1).ToString(), D2DColor.White, _defaultFontName, 10f, new D2DRect(missile.Position + new D2DPoint(80, 80), new D2DSize(50, 20)));
        //        ctx.DrawText(Math.Round(dist, 1).ToString(), D2DColor.White, _defaultFontName, 10f, new D2DRect(missile.Position - new D2DPoint(60, -80), new D2DSize(50, 20)));

        //        ctx.PopViewPort();
        //        ctx.Gfx.PopTransform();
        //    }

        //    ctx.Gfx.PopLayer();
        //}

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
            infoText += $"Sent B/s: {(World.IsServer ? _server.BytesSentPerSecond : 0)}\n";
            infoText += $"Rec B/s: {(World.IsServer ? _server.BytesReceivedPerSecond : 0)}\n";


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