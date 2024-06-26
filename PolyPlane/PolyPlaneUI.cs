using PolyPlane.AI_Behavior;
using PolyPlane.GameObjects;
using PolyPlane.GameObjects.Manager;
using PolyPlane.Helpers;
using PolyPlane.Net;
using PolyPlane.Net.NetHost;
using PolyPlane.Rendering;
using System.Diagnostics;
using System.Drawing;
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
        private bool _ctrlDown = false;
        private bool _queueNextViewId = false;
        private bool _queuePrevViewId = false;
        private bool _queueResetPlane = false;
        private bool _queueSpawnPlane = false;
        private bool _queueClearPlanes = false;
        private bool _skipRender = false;
        private bool _canRespawn = false;
        private bool _slewEnable = false;
        private bool _hasFocus = true;
        private bool _isHoldingAlt = false;
        private float _holdAltitude = 0f;

        private int _multiThreadNum = 4;

        private D2DPoint _playerPlaneSlewPos = D2DPoint.Zero;

        private GameTimer _burstTimer = new GameTimer(0.25f, true);
        private GameTimer _decoyTimer = new GameTimer(0.25f, true);
        private GameTimer _playerBurstTimer = new GameTimer(0.25f, true);
        private GameTimer _playerResetTimer = new GameTimer(15f);

        private Stopwatch _timer = new Stopwatch();
        private TimeSpan _updateTime = new TimeSpan();
        private TimeSpan _collisionTime = new TimeSpan();

        private GameObjectManager _objs = World.ObjectManager;
        private FighterPlane _playerPlane;

        private NetPlayHost _client;
        private NetEventManager _netMan;
        private CollisionManager _collisions;
        private RenderManager _render;
        private FPSLimiter _fpsLimiter = new FPSLimiter();

        public PolyPlaneUI()
        {
            InitializeComponent();

            this.GotFocus += PolyPlaneUI_GotFocus;
            this.LostFocus += PolyPlaneUI_LostFocus;
            this.Disposed += PolyPlaneUI_Disposed;
            this.MouseWheel += PolyPlaneUI_MouseWheel;

            _burstTimer.TriggerCallback = () => DoAIPlaneBursts();
            _decoyTimer.TriggerCallback = () => DoAIPlaneDecoys();

            _playerBurstTimer.StartCallback = () =>
            {
                _playerPlane.FireBullet();

                if (!_playerPlane.IsDisabled && _playerPlane.NumBullets > 0)
                    _render.DoScreenShake(2f);
            };

            _playerBurstTimer.TriggerCallback = () =>
            {
                _playerPlane.FireBullet();

                if (!_playerPlane.IsDisabled && _playerPlane.NumBullets > 0)
                    _render.DoScreenShake(2f);
            };

            //_playerResetTimer.TriggerCallback = () =>
            //{
            //    EnableRespawn();
            //};

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


                if (playerPlane.Equals(_playerPlane))
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

        private void NetMan_PlayerEventMessage(object? sender, string e)
        {
            _render?.AddNewEventMessage(e, EventType.Net);
        }

        private void NetMan_PlayerJoined(object? sender, int e)
        {
            var playerPlane = _objs.GetPlaneByPlayerID(e);
            if (playerPlane != null)
            {
                var joinMsg = $"'{playerPlane.PlayerName}' has joined.";
                _render?.AddNewEventMessage(joinMsg, EventType.Net);
            }
        }

        private void Objs_PlayerKilledEvent(object? sender, EventMessage e)
        {
            _client?.EnqueuePacket(new PlayerEventPacket(e.Message));
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
            if (World.ViewPlaneID.Equals(_playerPlane.ID))
                _render.NewHudMessage("Press 'R' to respawn.", D2DColor.Green);

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
                        _netMan = new NetEventManager(_client, _playerPlane);
                        _collisions = new CollisionManager(_netMan);

                        _netMan.ImpactEvent += HandleNewImpact;
                        _netMan.PlayerIDReceived += NetMan_PlayerIDReceived;
                        _netMan.PlayerDisconnected += NetMan_PlayerDisconnected;
                        _netMan.PlayerKicked += NetMan_PlayerKicked;
                        _netMan.PlayerRespawned += NetMan_PlayerRespawned;
                        _netMan.PlayerEventMessage += NetMan_PlayerEventMessage;
                        _netMan.PlayerJoined += NetMan_PlayerJoined;

                        _objs.PlayerKilledEvent += Objs_PlayerKilledEvent;

                        _client.PeerTimeoutEvent += Client_PeerTimeoutEvent;
                        _client.PeerDisconnectedEvent += Client_PeerDisconnectedEvent;


                        _client.Start();

                        InitGfx();
                        StartGameThread();
                        ResumeRender();

                        World.ViewPlaneID = _playerPlane.ID;


                        result = true;
                        break;

                    case DialogResult.Cancel:
                        // Solo game.
                        World.IsNetGame = false;
                        World.IsServer = false;

                        _collisions = new CollisionManager();
                        _collisions.ImpactEvent += HandleNewImpact;

                        InitPlane(config.IsAI, config.PlayerName);

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
            _client = null;

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
            if (this.Disposing || this.IsDisposed || _render == null) return;

            try
            {
                if (this.InvokeRequired && !this.Disposing)
                    this.Invoke(() => HandleNewImpact(sender, e));
                else
                {
                    var viewPlane = World.GetViewPlane();

                    if (viewPlane != null)
                    {
                        if (e.Target.Equals(viewPlane))
                        {
                            _render.DoScreenFlash(D2DColor.Red);
                            _render.DoScreenShake();
                        }
                        else if (e.DoesDamage && e.Impactor.Owner.Equals(viewPlane))
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
            _collisions.ImpactEvent -= HandleNewImpact;
            _client?.SendPlayerDisconnectPacket((uint)_playerPlane.PlayerID);

            StopRender();
            _killRender = true;
            _client?.Stop();
            _client?.Dispose();
            _render?.Dispose();
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
                _playerPlane = new FighterPlane(Utilities.FindSafeSpawnPoint(_objs));
            }

            _playerPlane.PlayerName = playerName;
            _playerPlane.PlayerID = World.GetNextPlayerId();

            _playerPlane.FireBulletCallback = b =>
            {
                _objs.AddBullet(b);

                if (World.IsNetGame)
                    _client.SendNewBulletPacket(b);
            };

            _playerPlane.AutoPilotOn = true;
            _playerPlane.ThrustOn = true;
            _playerPlane.Velocity = new D2DPoint(500f, 0f);

            _playerPlane.FireMissileCallback = (m) =>
            {
                _objs.EnqueueMissile(m);

                if (World.IsNetGame)
                    _client.SendNewMissilePacket(m);

            };

            _objs.EnqueuePlane(_playerPlane);
        }

        private void ResetAIPlane(FighterPlane plane)
        {
            plane.AutoPilotOn = true;
            plane.ThrustOn = true;
            plane.Position = Utilities.FindSafeSpawnPoint(_objs, plane);
            plane.Velocity = new D2DPoint(500f, 0f);
            plane.SyncFixtures();
            plane.RotationSpeed = 0f;
            plane.Rotation = 0f;
            plane.SASOn = true;
            plane.IsDisabled = false;
            plane.Reset();
            plane.FixPlane();
        }

        private void ResetPlane()
        {
            if (World.IsNetGame && !_canRespawn)
                return;

            if (World.IsNetGame)
                SendPlayerReset();

            _playerPlane.AutoPilotOn = true;
            _playerPlane.ThrustOn = true;
            _playerPlane.Position = Utilities.FindSafeSpawnPoint(_objs, _playerPlane);
            _playerPlane.Velocity = new D2DPoint(500f, 0f);
            _playerPlane.RotationSpeed = 0f;
            _playerPlane.Rotation = 0f;
            _playerPlane.SASOn = true;
            _playerPlane.IsDisabled = false;
            _playerPlane.Reset();
            _playerPlane.FixPlane();

            _playerResetTimer.Stop();
            _canRespawn = false;
            _render.ClearHudMessage();

            if (!_playerPlane.IsAI)
                World.ViewPlaneID = _playerPlane.ID;
        }

        private void TargetLockedWithMissile()
        {
            if (_playerPlane.Radar.HasLock && _playerPlane.Radar.LockedObj != null)
                _playerPlane.FireMissile(_playerPlane.Radar.LockedObj);
        }

        private FighterPlane GetAIPlane()
        {
            var pos = Utilities.FindSafeSpawnPoint(_objs);

            var aiPlane = new FighterPlane(pos, Utilities.RandomEnum<AIPersonality>());
            aiPlane.PlayerID = World.GetNextPlayerId();
            aiPlane.PlayerName = "(BOT) " + Utilities.GetRandomName();

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
            _render = new RenderManager(this, _netMan);
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
            _updateTime = TimeSpan.Zero;
            _collisionTime = TimeSpan.Zero;

            GraphicsExtensions.OnScreen = 0;
            GraphicsExtensions.OffScreen = 0;

            _render?.ResizeGfx();

            _timer.Restart();
            _objs.SyncAll();
            ProcessQueuedEvents();
            _timer.Stop();
            _updateTime += _timer.Elapsed;

            var viewPlane = World.GetViewPlane();

            _timer.Restart();

            // Update/advance objects.
            if (!_isPaused || _oneStep)
            {
                _timer.Restart();
                var allObjs = _objs.GetAllObjects();
                allObjs.ForEachParallel(o => o.Update(World.DT, World.RenderScale), _multiThreadNum);
                _timer.Stop();
                _updateTime += _timer.Elapsed;

                _timer.Restart();
                _collisions.DoCollisions();
                _timer.Stop();
                _collisionTime += _timer.Elapsed;

                _timer.Restart();

                World.UpdateAirDensityAndWind(World.DT);

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


            if (!_skipRender && this.WindowState != FormWindowState.Minimized)
                _render.RenderFrame(viewPlane);
            else
                _fpsLimiter.Wait(60);

            if (World.IsNetGame)
                _netMan.DoNetEvents();

            _objs.PruneExpired();

            DoMouseButtons();

            if (_slewEnable)
            {
                _playerPlane.Rotation = _playerPlane.PlayerGuideAngle;
                _playerPlane.RotationSpeed = 0f;
                _playerPlane.Position = _playerPlaneSlewPos;
                _playerPlane.Reset();
                _playerPlane.Velocity = D2DPoint.Zero;
            }

            if (_playerPlane.IsDisabled && !_playerResetTimer.IsRunning)
                _playerResetTimer.Restart(ignoreCooldown: true);

            if (_playerPlane.HasCrashed)
            {
                EnableRespawn();

                if (_playerPlane.IsAI && _playerPlane.AIRespawnReady)
                    ResetPlane();
            }

            // Hold current altitude while player is typeing.
            if ((_netMan != null && _netMan.ChatInterface.ChatIsActive) || _isHoldingAlt)
            {
                if (_holdAltitude == 0f)
                    _holdAltitude = _playerPlane.Altitude;

                var altHoldAngle = Utilities.MaintainAltitudeAngle(_playerPlane, _holdAltitude);
                _playerPlane.SetAutoPilotAngle(altHoldAngle);
            }
            else
            {
                _holdAltitude = 0f;
            }

            HandleAIPlaneRespawn();
        }

        private void HandleAIPlaneRespawn()
        {
            if (!World.RespawnAIPlanes)
                return;

            foreach (var plane in _objs.Planes)
            {
                if (plane.IsAI && plane.HasCrashed && plane.AIRespawnReady)
                {
                    ResetAIPlane(plane);
                }
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

            // Don't allow inputs if mouse left the window.
            if (!this.DesktopBounds.Contains(Control.MousePosition))
            {
                _playerBurstTimer.Stop();
                _playerBurstTimer.Reset();
                _playerPlane.FiringBurst = false;
                _playerPlane.DroppingDecoy = false;
                _isHoldingAlt = true; // Hold the plane at the current altitude if mouse leaves the window.
                return;
            }
            else
            {
                _isHoldingAlt = false;
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
                _playerBurstTimer.Reset();
                _playerPlane.FiringBurst = false;
            }

            if (buttons.HasFlag(MouseButtons.Right))
                _playerPlane.DroppingDecoy = true;
            else
                _playerPlane.DroppingDecoy = false;
        }

        private void ProcessQueuedEvents()
        {
            if (_queueNextViewId)
            {
                World.NextViewPlane();
                _queueNextViewId = false;
            }

            if (_queuePrevViewId)
            {
                World.PrevViewPlane();
                _queuePrevViewId = false;
            }

            if (_queueResetPlane)
            {
                ResetPlane();
                _queueResetPlane = false;
            }

            if (_queueClearPlanes)
            {
                _objs.Planes.ForEach(p =>
                {
                    if (!p.Equals(_playerPlane))
                        p.IsExpired = true;
                });

                _queueClearPlanes = false;
            }

            if (_queueSpawnPlane)
            {
                SpawnAIPlane();
                _queueSpawnPlane = false;
            }
        }

        private void DoAIPlaneBurst(float dt)
        {
            if (_objs.Planes.Any(p => p.FiringBurst && p.IsAI))
            {
                _burstTimer.Start();
            }
            else
            {
                _burstTimer.Stop();
                _burstTimer.Reset();
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

                if (!plane.IsAI && plane.Equals(_playerPlane))
                    continue;

                if (plane.IsNetObject)
                    continue;

                plane.FireBullet();
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

        private void DropDecoy(FighterPlane plane)
        {
            if (plane.NumDecoys <= 0)
            {
                plane.NumDecoys = 0;
                return;
            }

            if (plane.IsDisabled)
                return;

            var decoy = new Decoy(plane);
            _objs.EnqueueDecoy(decoy);

            if (World.IsNetGame)
                _netMan.SendNewDecoy(decoy);

            plane.NumDecoys--;
            plane.DecoysDropped++;
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
                    if (World.IsNetGame)
                        break;

                    _isHoldingAlt = !_isHoldingAlt;
                    break;

                case 'b':
                    _skipRender = !_skipRender;
                    break;

                case 'c':
                    if (!World.IsNetGame)
                        _queueClearPlanes = true;
                    break;

                case 'd':
                    World.InterpOn = !World.InterpOn;
                    break;

                case 'e':
                    break;

                case 'g':

                    if (World.IsNetGame)
                        break;

                    World.GunsOnly = !World.GunsOnly;
                    break;

                case 'h':
                    _render.ToggleHelp();
                    break;

                case 'i':
                    World.ShowAero = !World.ShowAero;
                    break;

                case 'k':
                    break;

                case 'l':
                    break;

                case 'm':
                    break;

                case 'n':
                    if (World.IsNetGame)
                        break;

                    _isPaused = true;
                    _oneStep = true;
                    break;

                case 'o':
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
                    }

                    break;

                case 't':
                    break;

                case 'u':
                    if (!World.IsNetGame)
                        _queueSpawnPlane = true;
                    break;

                case 'y':
                    break;

                case '=' or '+':
                    if (_shiftDown)
                    {
                        _render.HudScale += 0.01f;
                    }
                    else
                    {
                        _render?.ZoomIn();
                    }
                    break;

                case '-' or '_':

                    if (_shiftDown)
                    {
                        _render.HudScale -= 0.01f;
                    }
                    else
                    {
                        _render?.ZoomOut();
                    }
                    break;

                case '[':

                    if ((_playerPlane.IsDisabled || _playerPlane.HasCrashed || _playerPlane.IsAI))
                        _queuePrevViewId = true;
                    break;

                case ']':
                    if ((_playerPlane.IsDisabled || _playerPlane.HasCrashed || _playerPlane.IsAI))
                        _queueNextViewId = true;
                    break;

                case (char)8: //Backspace
                    World.ViewPlaneID = _playerPlane.ID;
                    break;

                case (char)9: //Tab
                    _render?.ToggleScore();
                    break;

                case ' ':
                    TargetLockedWithMissile();
                    break;

            }
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

        private void PolyPlaneUI_KeyDown(object sender, KeyEventArgs e)
        {
            _shiftDown = e.Shift;
            _ctrlDown = e.Control;

            if (_ctrlDown)
            {
                switch (e.KeyCode)
                {
                    case Keys.Oemplus:

                        if (!World.IsNetGame)
                            World.DT += 0.002f;

                        break;

                    case Keys.OemMinus:

                        if (!World.IsNetGame)
                            World.DT -= 0.002f;

                        break;
                }
            }
        }

        private void PolyPlaneUI_KeyUp(object sender, KeyEventArgs e)
        {
            _shiftDown = e.Shift;
            _ctrlDown = e.Control;
        }

        private void PolyPlaneUI_MouseMove(object sender, MouseEventArgs e)
        {
            var center = new D2DPoint(World.ViewPortSize.width * 0.5f, World.ViewPortSize.height * 0.5f);
            center /= World.DEFAULT_DPI / (float)this.DeviceDpi; // Scale for DPI.
            var pos = new D2DPoint(e.X, e.Y) * World.ViewPortScaleMulti;
            var angle = center.AngleTo(pos);

            _playerPlane.SetAutoPilotAngle(angle);
        }

        private void PolyPlaneUI_MouseWheel(object? sender, MouseEventArgs e)
        {
            if (e.Delta > 0)
            {
                _render?.DoMouseWheelUp();
            }
            else
            {
                _render?.DoMouseWheelDown();
            }
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