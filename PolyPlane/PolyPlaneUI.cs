using PolyPlane.GameObjects;
using PolyPlane.GameObjects.Manager;
using PolyPlane.Net;
using PolyPlane.Rendering;
using System.Diagnostics;
using unvell.D2DLib;

namespace PolyPlane
{
    public partial class PolyPlaneUI : Form
    {

        private Thread _gameThread;

        private const float DT_ADJ_AMT = 0.00025f;

        private ManualResetEventSlim _pauseRenderEvent = new ManualResetEventSlim(true);
        private ManualResetEventSlim _stopRenderEvent = new ManualResetEventSlim(true);

        private bool _isPaused = false;
        private bool _oneStep = false;
        private bool _killRender = false;
        private bool _shiftDown = false;
        private bool _showHelp = false;
        private bool _godMode = false;
        private bool _queueNextViewId = false;
        private bool _queuePrevViewId = false;
        private bool _queueResetPlane = false;
        private bool _skipRender = false;
        private long _lastRenderTime = 0;
        private float _renderFPS = 0;
        private bool _useMultiThread = true;
        private int _multiThreadNum = 4;

        private D2DPoint _playerPlaneSlewPos = D2DPoint.Zero;
        private bool _slewEnable = false;

        private readonly D2DColor _hudColor = new D2DColor(0.3f, D2DColor.GreenYellow);
        private D2DLayer _missileOverlayLayer;
        private readonly string _defaultFontName = "Consolas";

        private GameTimer _burstTimer = new GameTimer(0.25f, true);
        private GameTimer _decoyTimer = new GameTimer(0.25f, true);
        private GameTimer _playerBurstTimer = new GameTimer(0.1f, true);
        private int _aiPlaneViewID = -1;

        private Stopwatch _timer = new Stopwatch();
        private TimeSpan _renderTime = new TimeSpan();
        private TimeSpan _updateTime = new TimeSpan();
        private TimeSpan _collisionTime = new TimeSpan();
        private double _packetDelay = 0f;

        private GameObjectManager _objs = new GameObjectManager();
        private Plane _playerPlane;

        private NetPlayHost _client;
        private NetEventManager _netMan;
        private CollisionManager _collisions;
        private RenderManager _render;
        private FPSLimiter _fpsLimiter = new FPSLimiter();

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
        }

        private bool DoNetGameSetup()
        {
            bool result = false;

            using (var config = new ClientServerConfigForm())
            {
                if (config.ShowDialog() == DialogResult.OK)
                {
                    ENet.Library.Initialize();
                    World.IsNetGame = true;

                    World.IsServer = false;

                    InitPlane(config.IsAI);

                    _client = new ClientNetHost(config.Port, config.ServerIPAddress);
                    _netMan = new NetEventManager(_objs, _client, _playerPlane);
                    _collisions = new CollisionManager(_objs, _netMan);

                    _netMan.PlayerIDReceived += NetMan_PlayerIDReceived;

                    _client.Start();
                    result = true;
                    //this.Text += " - CLIENT";

                    ResumeRender();
                }
            }

            return result;
        }

        private void NetMan_PlayerIDReceived(object? sender, int e)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(() => NetMan_PlayerIDReceived(sender, e));
            }
            else
            {
                this.Text += $" - CLIENT - ID: {e}";
            }
        }

        private void PolyPlaneUI_Disposed(object? sender, EventArgs e)
        {
            _client?.SendPlayerDisconnectPacket((uint)_playerPlane.PlayerID);

            StopRender();
            _gameThread?.Join(100);

            _client?.Stop();
            _client?.Dispose();

            ENet.Library.Deinitialize();

            _render?.Dispose();

            _missileOverlayLayer?.Dispose();
            _fpsLimiter?.Dispose();
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
                _objs.AddBullet(b);

                if (World.IsNetGame)
                {
                    _client.SendNewBulletPacket(b);
                }
            };


            _playerPlane.AutoPilotOn = true;
            _playerPlane.ThrustOn = true;
            _playerPlane.Velocity = new D2DPoint(500f, 0f);

            _playerPlane.Radar = new Radar(_playerPlane, _hudColor, _objs.Missiles, _objs.Planes);

            _playerPlane.Radar.SkipFrames = World.PHYSICS_STEPS;

            _playerPlane.FireMissileCallback = (m) =>
            {
                _objs.EnqueueMissile(m);

                if (World.IsNetGame)
                {
                    _client.SendNewMissilePacket(m);
                }

            };

            if (World.IsNetGame)
                _objs.EnqueuePlane(_playerPlane);
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

            _playerPlane.Radar = new Radar(_playerPlane, _hudColor, _objs.Missiles, _objs.Planes);

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
            aiPlane.Radar = new Radar(aiPlane, _hudColor, _objs.Missiles, _objs.Planes);

            aiPlane.Radar.SkipFrames = World.PHYSICS_STEPS;

            aiPlane.FireMissileCallback = (m) =>
            {
                _objs.EnqueueMissile(m);

                if (World.IsNetGame)
                {
                    _client.SendNewMissilePacket(m);
                }

            };

            aiPlane.FireBulletCallback = b =>
            {
                _objs.AddBullet(b);

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
            _objs.EnqueuePlane(aiPlane);
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
            _render?.Dispose();
            _render = new RenderManager(this, _objs, _netMan);
        }

        private void ResizeGfx(bool force = false)
        {
            if (!force)
                if (World.ViewPortBaseSize.height == this.Size.Height && World.ViewPortBaseSize.width == this.Size.Width)
                    return;

            StopRender();

            _render.ResizeGfx(force);

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
            _objs.SyncAll();
            ProcessObjQueue();


            Plane viewPlane = GetViewPlane();
            World.ViewID = viewPlane.ID;

            _timer.Restart();

            // Update/advance objects.
            if (!_isPaused || _oneStep)
            {
                var partialDT = World.SUB_DT;

                var localObjs = _objs.GetAllLocalObjects();
                var numObj = localObjs.Count;

                for (int i = 0; i < World.PHYSICS_STEPS; i++)
                {
                    _timer.Restart();

                    _collisions.DoCollisions();

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

                var netObj = _objs.GetAllNetObjects();
                if (_useMultiThread)
                {
                    netObj.ForEachParallel(o => o.Update(World.DT, World.ViewPortSize, World.RenderScale), _multiThreadNum);
                }
                else
                {
                    netObj.ForEach(o => o.Update(World.DT, World.ViewPortSize, World.RenderScale));
                }

                World.UpdateAirDensityAndWind(World.DT);

                //DoDecoySuccess();

                _playerBurstTimer.Update(World.DT);

                DoAIPlaneBurst(World.DT);
                _decoyTimer.Update(World.DT);

                _timer.Stop();
                _updateTime += _timer.Elapsed;

                _oneStep = false;
            }

            _timer.Restart();

            if (!_skipRender)
                _render.RenderFrame(viewPlane);
            else
                _fpsLimiter.Wait(60);

            _timer.Stop();
            _renderTime = _timer.Elapsed;

            if (World.IsNetGame)
                _netMan.DoNetEvents();

            _objs.PruneExpired();

            var now = DateTime.UtcNow.Ticks;
            var fps = TimeSpan.TicksPerSecond / (float)(now - _lastRenderTime);
            _lastRenderTime = now;
            _renderFPS = fps;

            if (_slewEnable)
            {
                _playerPlane.Rotation = _playerPlane.PlayerGuideAngle;
                _playerPlane.RotationSpeed = 0f;
                _playerPlane.Position = _playerPlaneSlewPos;
                _playerPlane.Reset();
                _playerPlane.Velocity = D2DPoint.Zero;
                //_playerPlane.HasCrashed = true;
                //_godMode = true;
            }
        }


        private Plane GetViewPlane()
        {
            var idPlane = _objs.GetPlaneByPlayerID(_aiPlaneViewID);

            if (idPlane != null)
            {
                return idPlane;
            }
            else
                return _playerPlane;
        }

        private void ProcessObjQueue()
        {
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

                SendPlayerReset();

                _queueResetPlane = false;
            }
        }

        private void DoAIPlaneBurst(float dt)
        {
            if (_objs.Planes.Any(p => p.FiringBurst))
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
            var firing = _objs.Planes.Where(p => p.FiringBurst).ToArray();

            if (firing.Length == 0)
                return;

            for (int i = 0; i < firing.Length; i++)
            {
                var plane = firing[i];

                if (!plane.IsAI && plane.ID.Equals(_playerPlane.ID))
                    continue;

                plane.FireBullet(p => AddExplosion(p));
            }
        }

        private void DoAIPlaneDecoys()
        {
            if (_playerPlane.DroppingDecoy)
                DropDecoy(_playerPlane);

            var dropping = _objs.Planes.Where(p => p.DroppingDecoy).ToArray();

            if (dropping.Length == 0)
                return;

            for (int i = 0; i < dropping.Length; i++)
            {
                var plane = dropping[i];

                DropDecoy(plane);
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

        private void SendPlayerReset()
        {
            var resetPacket = new BasicPacket(PacketTypes.PlayerReset, _playerPlane.ID);
            _client.EnqueuePacket(resetPacket);
        }

        private void DoNetDecoy(Decoy decoy)
        {
            var decoyPacket = new Net.DecoyPacket(decoy);
            _client.EnqueuePacket(decoyPacket);
        }

        private void DropDecoy(Plane plane)
        {
            if (plane.IsDamaged)
                return;

            var decoy = new Decoy(plane);
            _objs.EnqueueDecoy(decoy);
            DoNetDecoy(decoy);
        }

        private void DoDecoySuccess()
        {
            // Test for decoy success.
            const float MIN_DECOY_FOV = 10f;
            var decoys = _objs.Decoys;

            bool groundScatter = false;

            for (int i = 0; i < _objs.Missiles.Count; i++)
            {
                var missile = _objs.Missiles[i] as GuidedMissile;
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

        private void AddExplosion(D2DPoint pos)
        {
            var explosion = new Explosion(pos, 200f, 1.4f);
            _objs.AddExplosion(explosion);
        }

        private string GetInfo()
        {
            var viewPlane = GetViewPlane();

            string infoText = string.Empty;
            infoText += $"Paused: {_isPaused}\n\n";


            var numObj = _objs.TotalObjects;
            infoText += $"Num Objects: {numObj}\n";
            infoText += $"On Screen: {GraphicsExtensions.OnScreen}\n";
            infoText += $"Off Screen: {GraphicsExtensions.OffScreen}\n";
            infoText += $"AI Planes: {_objs.Planes.Count(p => !p.IsDamaged && !p.HasCrashed)}\n";


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

        private int GetNextAIID()
        {
            if (_aiPlaneViewID == -1 && _objs.Planes.Count > 0)
                return _objs.Planes.First().ID.PlayerID;


            int nextId = -1;
            for (int i = 0; i < _objs.Planes.Count; i++)
            {
                var plane = _objs.Planes[i];

                if (plane.ID.PlayerID == _aiPlaneViewID && i + 1 < _objs.Planes.Count)
                {
                    nextId = _objs.Planes[i + 1].ID.PlayerID;
                }
                else if (plane.ID.PlayerID == _aiPlaneViewID && i + 1 >= _objs.Planes.Count)
                {
                    nextId = 0;
                }
            }

            return nextId;
        }

        private int GetPrevAIID()
        {
            if (_aiPlaneViewID == -1 && _objs.Planes.Count > 0)
                return _objs.Planes.Last().ID.PlayerID;

            int nextId = -1;

            for (int i = 0; i < _objs.Planes.Count; i++)
            {
                var plane = _objs.Planes[i];

                if (plane.ID.PlayerID == _aiPlaneViewID && i - 1 >= 0)
                {
                    nextId = _objs.Planes[i - 1].ID.PlayerID;
                }
                else if (plane.ID.PlayerID == _aiPlaneViewID && i - 1 <= 0)
                {
                    nextId = _objs.Planes.Last().ID.PlayerID;
                }
            }

            return nextId;
        }

        private void PolyPlaneUI_KeyPress(object sender, KeyPressEventArgs e)
        {
            switch (e.KeyChar)
            {
                case 'a':
                    //_playerPlane.AutoPilotOn = !_playerPlane.AutoPilotOn;
                    break;

                case 'b':
                    //_motionBlur = !_motionBlur;
                    //_trailsOn = false;
                    //_skipRender = !_skipRender;
                    break;

                case 'c':
                    break;

                case 'd':
                    World.InterpOn = !World.InterpOn;
                    break;

                case 'e':
                    break;

                case 'h':
                    //_showHelp = !_showHelp;

                    break;

                case 'i':
                    World.ShowAero = !World.ShowAero;
                    break;

                case 'k':
                    //World.EnableTurbulence = !World.EnableTurbulence;

                    //var viewPlane = GetViewPlane();
                    //if (viewPlane != null)
                    //    viewPlane.IsDamaged = true;

                    break;

                case 'l':
                    //World.EnableWind = !World.EnableWind;
                    break;

                case 'm':
                    //InitPlane();
                    break;

                case 'n':
                    //_isPaused = true;
                    //_oneStep = true;
                    break;

                case 'o':
                    //World.ShowMissileCloseup = !World.ShowMissileCloseup;
                    //_useMultiThread = !_useMultiThread;
                    //_showInfo = !_showInfo;
                    _render.ToggleInfo();
                    break;

                case 'p':

                //if (!_isPaused)
                //    PauseRender();
                //else
                //    ResumeRender();
                //break;

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
                    //SpawnAIPlane();
                    break;

                case 'y':
                    //_targetTypes = Helpers.CycleEnum(_targetTypes);
                    break;

                case '=' or '+':
                    if (_shiftDown)
                    {
                        //World.DT += DT_ADJ_AMT;
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
                        //World.DT -= DT_ADJ_AMT;
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

                case ' ':
                    TargetLockedWithMissile();
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

            //_guideAngle = angle;
            _playerPlane.SetAutoPilotAngle(angle);
        }

        private void PolyPlaneUI_FormClosing(object sender, FormClosingEventArgs e)
        {
            _gameThread?.Join(1000);
            //_renderThread.Wait(1000);


        }

        private void PolyPlaneUI_Shown(object sender, EventArgs e)
        {
            if (DoNetGameSetup())
            {
                InitGfx();

                if (_netMan != null)
                {
                    _netMan.ScreenFlashCallback = _render.DoScreenFlash;
                    _netMan.ScreenShakeCallback = _render.DoScreenShake;
                }

                StartGameThread();
            }
            else
            {
                this.Close();
            }
        }
    }
}