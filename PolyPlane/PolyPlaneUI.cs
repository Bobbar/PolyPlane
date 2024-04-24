using PolyPlane.AI_Behavior;
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
        private bool _canRespawn = false;

        private D2DPoint _playerPlaneSlewPos = D2DPoint.Zero;
        private bool _slewEnable = false;

        private readonly D2DColor _hudColor = new D2DColor(0.3f, D2DColor.GreenYellow);
        private D2DLayer _missileOverlayLayer;
        private readonly string _defaultFontName = "Consolas";

        private GameTimer _burstTimer = new GameTimer(0.25f, true);
        private GameTimer _decoyTimer = new GameTimer(0.25f, true);
        private GameTimer _playerBurstTimer = new GameTimer(0.15f, true);
        private GameTimer _playerResetTimer = new GameTimer(15f);
        private int _aiPlaneViewID = -1;

        private Stopwatch _timer = new Stopwatch();
        private TimeSpan _renderTime = new TimeSpan();
        private TimeSpan _updateTime = new TimeSpan();
        private TimeSpan _collisionTime = new TimeSpan();
        private double _packetDelay = 0f;

        private GameObjectManager _objs = new GameObjectManager();
        private FighterPlane _playerPlane;

        private NetPlayHost _client;
        private NetEventManager _netMan;
        private CollisionManager _collisions;
        private RenderManager _render;
        private FPSLimiter _fpsLimiter = new FPSLimiter();
        private bool _hasFocus = true;
        private Random _rnd => Helpers.Rnd;

        public PolyPlaneUI()
        {
            InitializeComponent();

            this.GotFocus += PolyPlaneUI_GotFocus;
            this.LostFocus += PolyPlaneUI_LostFocus;
            this.MouseWheel += PolyPlaneUI_MouseWheel;
            this.Disposed += PolyPlaneUI_Disposed;

            _burstTimer.TriggerCallback = () => DoAIPlaneBursts();
            _decoyTimer.TriggerCallback = () => DoAIPlaneDecoys();

            _playerBurstTimer.TriggerCallback = () =>
            {
                _playerPlane.FireBullet(p => _objs.AddBulletExplosion(p));

                if (!_playerPlane.IsDamaged && _playerPlane.NumBullets > 0)
                    _render.DoScreenShake(2f);
            };

            _playerResetTimer.TriggerCallback = () =>
            {
                EnableRespawn();
            };

            _multiThreadNum = Environment.ProcessorCount - 2;
        }

        private void NetMan_PlayerRespawned(object? sender, FighterPlane e)
        {
            _render?.AddNewEventMessage($"'{e.PlayerName}' has respawned.", EventType.Net);
        }

        private void NetMan_PlayerKicked(object? sender, int e)
        {
            var playerPlane = _objs.GetPlaneByPlayerID(e);

            if (playerPlane != null)
            {
                _render?.AddNewEventMessage($"'{playerPlane.PlayerName}' has been kicked.", EventType.Net);


                if (playerPlane.ID.Equals(_playerPlane.ID))
                    _render?.NewHudMessage("You have been kicked from the server!", D2DColor.Blue);
            }
        }

        private void NetMan_PlayerDisconnected(object? sender, int e)
        {
            var playerPlane = _objs.GetPlaneByPlayerID(e);

            if (playerPlane != null)
            {
                _render?.AddNewEventMessage($"'{playerPlane.PlayerName}' has left.", EventType.Net);
            }
        }

        private void Client_PeerTimeoutEvent(object? sender, ENet.Peer e)
        {
            _render.NewHudMessage("Timed out!?", D2DColor.Yellow);
        }

        private void PolyPlaneUI_LostFocus(object? sender, EventArgs e)
        {
            _hasFocus = false;
        }

        private void PolyPlaneUI_GotFocus(object? sender, EventArgs e)
        {
            _hasFocus = true;
        }

        private void EnableRespawn()
        {
            _render.NewHudMessage("Press R to respawn.", D2DColor.Green);
            _canRespawn = true;
        }

        private bool DoNetGameSetup()
        {
            bool result = false;

            using (var config = new ClientServerConfigForm())
            {
                switch (config.ShowDialog(this))
                {
                    case DialogResult.OK:
                        // Net game.
                        World.IsNetGame = true;
                        World.IsServer = false;

                        InitPlane(config.IsAI, config.PlayerName);
                        _playerPlane.PlaneColor = config.PlaneColor;

                        _client = new ClientNetHost(config.Port, config.ServerIPAddress);
                        _netMan = new NetEventManager(_objs, _client, _playerPlane);
                        _collisions = new CollisionManager(_objs, _netMan);

                        _netMan.ImpactEvent += HandleNewImpact;
                        _netMan.PlayerIDReceived += NetMan_PlayerIDReceived;
                        _netMan.PlayerDisconnected += NetMan_PlayerDisconnected;
                        _netMan.PlayerKicked += NetMan_PlayerKicked;
                        _netMan.PlayerRespawned += NetMan_PlayerRespawned;
                        _client.PeerTimeoutEvent += Client_PeerTimeoutEvent;
                        _client.PeerDisconnectedEvent += Client_PeerDisconnectedEvent;


                        _client.Start();

                        InitGfx();
                        StartGameThread();
                        ResumeRender();

                        result = true;
                        break;

                    case DialogResult.Cancel:
                        // Solo game.
                        World.IsNetGame = false;
                        World.IsServer = false;

                        _collisions = new CollisionManager(_objs);
                        _collisions.ImpactEvent += HandleNewImpact;

                        InitPlane(false, config.PlayerName);
                        _playerPlane.PlaneColor = config.PlaneColor;

                        InitGfx();
                        StartGameThread();
                        ResumeRender();

                        result = true;

                        break;

                    case DialogResult.Abort:
                        // Aborted.
                        result = false;
                        break;
                }
            }

            return result;
        }

      

        /// <summary>
        /// Return to server/game config screen.
        /// </summary>
        private void ResetGame()
        {
            World.IsNetGame = false;
            World.IsServer = false;

            _killRender = true;

            Task.Delay(100).Wait();

            _objs.Clear();

            _netMan.ImpactEvent -= HandleNewImpact;
            _netMan.PlayerIDReceived -= NetMan_PlayerIDReceived;
            _netMan.PlayerDisconnected -= NetMan_PlayerDisconnected;
            _netMan.PlayerKicked -= NetMan_PlayerKicked;

            _client.PeerTimeoutEvent -= Client_PeerTimeoutEvent;
            _client.PeerDisconnectedEvent -= Client_PeerDisconnectedEvent;

            _client?.Stop();
            _client?.Dispose();

            DoNetGameSetup();
        }

        private void Client_PeerDisconnectedEvent(object? sender, ENet.Peer e)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(() => Client_PeerDisconnectedEvent(sender, e));
            }
            else
            {
                ResetGame();
            }
        }

        private void HandleNewImpact(object? sender, ImpactEvent e)
        {
            if (this.Disposing || this.IsDisposed) return;

            try
            {
                if (this.InvokeRequired)
                    this.Invoke(() => HandleNewImpact(sender, e));
                else
                {
                    var viewPlane = GetViewPlane();

                    if (viewPlane != null)
                    {
                        if (e.Target.ID.Equals(viewPlane.ID))
                        {
                            _render.DoScreenFlash(D2DColor.Red);
                            _render.DoScreenShake();
                        }
                        else if (e.DoesDamage && e.Impactor.Owner.ID.Equals(viewPlane.ID))
                        {
                            _render.DoScreenFlash(D2DColor.Green);
                        }
                    }
                }
            }
            catch
            {
                // Catch object disposed exceptions.
            }
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
            _killRender = true;
            _gameThread?.Join(100);

            _client?.Stop();
            _client?.Dispose();

            _render?.Dispose();

            _missileOverlayLayer?.Dispose();
            _fpsLimiter?.Dispose();
        }

        private void InitPlane(bool asAI = false, string playerName = "Player")
        {
            if (asAI)
            {
                _playerPlane = GetAIPlane();

            }
            else
            {
                _playerPlane = new FighterPlane(new D2DPoint(Helpers.Rnd.NextFloat(World.PlaneSpawnRange.X, World.PlaneSpawnRange.Y), -5000f));
            }

            _playerPlane.PlayerName = playerName;
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

            _playerPlane.Radar.SkipFrames = World.PHYSICS_SUB_STEPS;

            _playerPlane.FireMissileCallback = (m) =>
            {
                _objs.EnqueueMissile(m);

                if (World.IsNetGame)
                {
                    _client.SendNewMissilePacket(m);
                }

            };

            _objs.EnqueuePlane(_playerPlane);
        }

        private void ResetPlane()
        {
            if (World.IsNetGame && !_canRespawn)
                return;

            if (World.IsNetGame)
                SendPlayerReset();

            _playerPlane.AutoPilotOn = true;
            _playerPlane.ThrustOn = true;
            _playerPlane.Position = new D2DPoint(Helpers.Rnd.NextFloat(World.PlaneSpawnRange.X, World.PlaneSpawnRange.Y), -5000f);
            _playerPlane.Velocity = new D2DPoint(500f, 0f);
            _playerPlane.RotationSpeed = 0f;
            _playerPlane.Rotation = 0f;
            _playerPlane.SASOn = true;
            _playerPlane.IsDamaged = false;
            _playerPlane.Reset();
            _playerPlane.FixPlane();

            _playerPlane.Radar = new Radar(_playerPlane, _hudColor, _objs.Missiles, _objs.Planes);
            _playerPlane.Radar.SkipFrames = World.PHYSICS_SUB_STEPS;

            _playerResetTimer.Stop();
            _canRespawn = false;
            _render.ClearHudMessage();
        }

        private void TargetLockedWithMissile()
        {
            if (_playerPlane.Radar.HasLock && _playerPlane.Radar.LockedObj != null)
                _playerPlane.FireMissile(_playerPlane.Radar.LockedObj);
        }

        private FighterPlane GetAIPlane()
        {
            var pos = new D2DPoint(_rnd.NextFloat(World.PlaneSpawnRange.X, World.PlaneSpawnRange.Y), _rnd.NextFloat(-4000f, -17000f));

            var aiPlane = new FighterPlane(pos, Helpers.RandomEnum<AIPersonality>());
            aiPlane.PlayerID = World.GetNextPlayerId();
            aiPlane.Radar = new Radar(aiPlane, _hudColor, _objs.Missiles, _objs.Planes);
            aiPlane.PlayerName = "(BOT) " + Helpers.GetRandomName();
            aiPlane.Radar.SkipFrames = World.PHYSICS_SUB_STEPS;

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
            _killRender = false;
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

            FighterPlane viewPlane = GetViewPlane();
            World.ViewID = viewPlane.ID;

            _timer.Restart();

            // Update/advance objects.
            if (!_isPaused || _oneStep)
            {
                var partialDT = World.SUB_DT;

                var localObjs = _objs.GetAllLocalObjects();
                var numObj = localObjs.Count;

                for (int i = 0; i < World.PHYSICS_SUB_STEPS; i++)
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

                // TODO: Where to handle decoy success? Client or server?
                //if (!World.IsNetGame)
                _collisions.DoDecoySuccess();

                _playerBurstTimer.Update(World.DT);
                _playerResetTimer.Update(World.DT);

                DoAIPlaneBurst(World.DT);
                _decoyTimer.Update(World.DT);

                _timer.Stop();
                _updateTime += _timer.Elapsed;

                _oneStep = false;
            }

            _render.CollisionTime = _collisionTime;
            _render.UpdateTime = _updateTime;

            _timer.Restart();

            if (!_skipRender)
                _render.RenderFrame(viewPlane);
            else
                _fpsLimiter.Wait(60);

            _timer.Stop();
            _renderTime += _timer.Elapsed;


            if (World.IsNetGame)
                _netMan.DoNetEvents();

            _objs.PruneExpired();

            DoMouseButtons();

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

            if (_playerPlane.IsDamaged && !_playerResetTimer.IsRunning)
                _playerResetTimer.Restart(ignoreCooldown: true);

            if (_playerPlane.HasCrashed)
            {
                EnableRespawn();

                if (_playerPlane.IsAI)
                    ResetPlane();
            }

            // Flight straight and level while player is typeing.
            if (_netMan != null && _netMan.ChatInterface.ChatIsActive)
            {
                var toRight = Helpers.IsPointingRight(_playerPlane.Rotation);

                if (toRight)
                    _playerPlane.SetAutoPilotAngle(0f);
                else
                    _playerPlane.SetAutoPilotAngle(180f);
            }
        }

        private void DoMouseButtons()
        {
            if (_playerPlane.IsAI)
                return;

            if (!_hasFocus)
            {
                _playerBurstTimer.Stop();
                _playerPlane.FiringBurst = false;
                _playerPlane.DroppingDecoy = false;
                return;
            }

            var buttons = Control.MouseButtons;

            if (buttons.HasFlag(MouseButtons.Left))
            {
                _playerBurstTimer.Start();
                _playerPlane.FiringBurst = true;
            }
            else
            {
                _playerBurstTimer.Stop();
                _playerPlane.FiringBurst = false;
            }

            if (buttons.HasFlag(MouseButtons.Right))
                _playerPlane.DroppingDecoy = true;
            else
                _playerPlane.DroppingDecoy = false;

        }

        private FighterPlane GetViewPlane()
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

                if (plane.IsNetObject)
                    continue;

                plane.FireBullet(p => AddExplosion(p));
            }
        }

        private void DoAIPlaneDecoys()
        {
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
        }

        private void SendPlayerReset()
        {
            _netMan.SendPlaneReset(_playerPlane);
        }

        private void DoNetDecoy(Decoy decoy)
        {
            _netMan.SendNewDecoy(decoy);
        }

        private void DropDecoy(FighterPlane plane)
        {
            if (plane.NumDecoys <= 0)
            {
                plane.NumDecoys = 0;
                return;
            }

            if (plane.IsDamaged)
                return;

            var decoy = new Decoy(plane);
            _objs.EnqueueDecoy(decoy);

            if (World.IsNetGame)
                DoNetDecoy(decoy);

            plane.NumDecoys--;
        }

        private void AddExplosion(D2DPoint pos)
        {
            var explosion = new Explosion(pos, 200f, 1.4f);
            _objs.AddExplosion(explosion);
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
            if (_netMan != null)
            {
                _netMan.ChatInterface.NewKeyPress(e.KeyChar);

                if (_netMan.ChatInterface.ChatIsActive)
                    return;
            }

            switch (e.KeyChar)
            {
                case 'a':
                    //_playerPlane.AutoPilotOn = !_playerPlane.AutoPilotOn;
                    break;

                case 'b':
                    //_motionBlur = !_motionBlur;
                    //_trailsOn = false;
                    _skipRender = !_skipRender;
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

                    if (World.IsNetGame)
                        break;

                    if (!_isPaused)
                        PauseRender();
                    else
                        ResumeRender();

                    break;

                case 'r':
                    _queueResetPlane = true;
                    break;

                case 's':

                    if (World.IsNetGame)
                        break;

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
                    if (!World.IsNetGame)
                        SpawnAIPlane();
                    break;

                case 'y':
                    //_targetTypes = Helpers.CycleEnum(_targetTypes);
                    break;

                case '=' or '+':
                    if (_shiftDown)
                    {
                        //World.DT += DT_ADJ_AMT;
                        _render.HudScale += 0.01f;
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
                        _render.HudScale -= 0.01f;

                    }
                    else
                    {
                        World.ZoomScale -= 0.01f;
                        ResizeGfx(force: true);
                    }
                    break;

                case '[':

                    if ((_playerPlane.IsDamaged || _playerPlane.HasCrashed))
                        _queuePrevViewId = true;

                    break;
                case ']':
                    if ((_playerPlane.IsDamaged || _playerPlane.HasCrashed))
                        _queueNextViewId = true;
                    break;

                case (char)8: //Backspace
                    _aiPlaneViewID = _playerPlane.PlayerID;
                    break;
                case ' ':
                    TargetLockedWithMissile();
                    break;

            }
        }

        private void PolyPlaneUI_MouseUp(object sender, MouseEventArgs e)
        {
        }

        private void PolyPlaneUI_MouseDown(object sender, MouseEventArgs e)
        {
            switch (e.Button)
            {
                case MouseButtons.Middle:
                    TargetLockedWithMissile();
                    break;

            }
        }

        private void PolyPlaneUI_MouseWheel(object? sender, MouseEventArgs e)
        {
            //if (!_shiftDown)
            //{
            //    if (e.Delta > 0)
            //        _playerPlane.MoveThrottle(true);
            //    else
            //        _playerPlane.MoveThrottle(false);
            //}
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

            _playerPlane.SetAutoPilotAngle(angle);
        }

        private void PolyPlaneUI_Shown(object sender, EventArgs e)
        {
            if (!DoNetGameSetup())
            {
                this.Close();
            }

        }
    }
}