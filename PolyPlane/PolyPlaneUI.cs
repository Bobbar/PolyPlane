using PolyPlane.AI_Behavior;
using PolyPlane.GameObjects;
using PolyPlane.GameObjects.Manager;
using PolyPlane.Helpers;
using PolyPlane.Net;
using PolyPlane.Net.NetHost;
using PolyPlane.Rendering;
using System.Diagnostics;
using unvell.D2DLib;

namespace PolyPlane
{
    public partial class PolyPlaneUI : Form
    {
        private Thread _gameThread;
        private ManualResetEventSlim _renderExitEvent = new ManualResetEventSlim(false);

        private bool _isFullScreen = false;
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

            _multiThreadNum = Environment.ProcessorCount - 2;
        }

        /// <summary>
        /// Toggle phony (but effective) fullscreen mode.
        /// </summary>
        private void ToggleFullscreen()
        {
            if (!_isFullScreen)
            {
                this.WindowState = FormWindowState.Normal;
                this.FormBorderStyle = FormBorderStyle.None;
                this.WindowState = FormWindowState.Maximized;

                _isFullScreen = true;
            }
            else
            {
                this.FormBorderStyle = FormBorderStyle.Sizable;
                this.WindowState = FormWindowState.Maximized;
                _isFullScreen = false;
            }
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
            {
                string spawnDirText = "Left";
                var guideAngle = GetPlayerGuidanceAngle();

                if (Utilities.IsPointingRight(guideAngle))
                    spawnDirText = "Right";

                _render.NewHudMessage($"Press 'R' or left-click to respawn.\n\n Direction: {spawnDirText}", D2DColor.GreenYellow);
            }

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

                        //InitPlane(config.PlaneColor, config.IsAI, config.PlayerName);

                        _playerPlane = GetNewPlane(config.PlaneColor, config.IsAI, config.PlayerName);

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
                        ResumeGame();

                        World.ViewPlaneID = _playerPlane.ID;


                        result = true;
                        break;

                    case DialogResult.Cancel:
                        // Solo game.
                        World.IsNetGame = false;
                        World.IsServer = false;

                        _collisions = new CollisionManager();
                        _collisions.ImpactEvent += HandleNewImpact;

                        //InitPlane(config.PlaneColor, config.IsAI, config.PlayerName);
                        _playerPlane = GetNewPlane(config.PlaneColor, config.IsAI, config.PlayerName);

                        InitGfx();
                        StartGameThread();
                        ResumeGame();

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
            if (this.Disposing || this.IsDisposed || _render == null || _killRender == true) return;

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
                            if (e.Target is FighterPlane targetPlane && !targetPlane.IsDisabled)
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

        private void PolyPlaneUI_FormClosing(object sender, FormClosingEventArgs e)
        {
            _collisions.ImpactEvent -= HandleNewImpact;
            _client?.SendPlayerDisconnectPacket((uint)_playerPlane.PlayerID);

            StopRender();
        }

        private void PolyPlaneUI_Disposed(object? sender, EventArgs e)
        {
            _client?.Stop();
            _client?.Dispose();
            _render?.Dispose();
            _fpsLimiter?.Dispose();
        }


        private FighterPlane GetNewPlane(D2DColor planeColor, bool isAI = false, string playerName = "Player")
        {
            var pos = Utilities.FindSafeSpawnPoint(_objs);
            var plane = new FighterPlane(pos, planeColor, isAI, false);

            plane.PlayerName = playerName;

            if (isAI && World.ShowAITags)
                plane.PlayerName += $" [{Utilities.GetPersonalityTag(plane.Personality)}]";

            plane.PlayerID = World.GetNextPlayerId();
            plane.AutoPilotOn = true;
            plane.ThrustOn = true;
            plane.Velocity = new D2DPoint(500f, 0f);

            plane.FireBulletCallback = b =>
            {
                _objs.EnqueueBullet(b);

                if (b.Owner.ID.Equals(World.ViewPlaneID))
                    _render.DoScreenShake(2f);

                if (World.IsNetGame)
                    _client.SendNewBulletPacket(b);
            };

            plane.FireMissileCallback = (m) =>
            {
                _objs.EnqueueMissile(m);

                if (World.IsNetGame)
                    _client.SendNewMissilePacket(m);

            };

            plane.DropDecoyCallback = DropDecoy;

            _objs.EnqueuePlane(plane);

            return plane;
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
            //plane.SASOn = true;
            plane.IsDisabled = false;
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

            // Set player plane rotation to current guidance angle.
            // Allow players to choose to spawn traveling left or right.
            var guideAngle = GetPlayerGuidanceAngle();

            if (Utilities.IsPointingRight(guideAngle))
                _playerPlane.Rotation = 0f;
            else
                _playerPlane.Rotation = 180f;

            _playerPlane.Velocity = Utilities.AngleToVectorDegrees(_playerPlane.Rotation, 500f);

            _playerPlane.RotationSpeed = 0f;
            //_playerPlane.SASOn = true;
            _playerPlane.IsDisabled = false;
            _playerPlane.FixPlane();

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

        private void SpawnAIPlane()
        {
            var aiPlane = GetNewPlane(D2DColor.Randomly(), isAI: true, playerName: "(BOT) " + Utilities.GetRandomName());
            _objs.EnqueuePlane(aiPlane);
        }

        private void StartGameThread()
        {
            _killRender = false;
            _renderExitEvent.Reset();
            _gameThread = new Thread(GameLoop);
            _gameThread.Priority = ThreadPriority.AboveNormal;
            _gameThread.Start();
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
                AdvanceAndRender();
            }

            _renderExitEvent.Set();
        }

        private void AdvanceAndRender()
        {
            if (_killRender)
                return;

            _updateTime = TimeSpan.Zero;
            _collisionTime = TimeSpan.Zero;

            GraphicsExtensions.OnScreen = 0;
            GraphicsExtensions.OffScreen = 0;

            _render?.ResizeGfx();

            ProcessQueuedEvents();

            var viewPlane = World.GetViewPlane();

            // Update/advance objects.
            if (!World.IsPaused || _oneStep)
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

                _objs.Update();

                _timer.Stop();
                _updateTime += _timer.Elapsed;

                _oneStep = false;
            }

            _render.CollisionTime = _collisionTime;
            _render.UpdateTime = _updateTime;

            if (!_skipRender && !_killRender && this.WindowState != FormWindowState.Minimized)
                _render.RenderFrame(viewPlane);
            else
                _fpsLimiter.Wait(60);

            if (World.IsNetGame)
                _netMan.DoNetEvents();

            DoMouseButtons();

            if (_slewEnable)
            {
                _playerPlane.Rotation = _playerPlane.PlayerGuideAngle;
                _playerPlane.RotationSpeed = 0f;
                _playerPlane.Position = _playerPlaneSlewPos;
                _playerPlane.Velocity = D2DPoint.Zero;
            }

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
                _playerPlane.FiringBurst = false;
                _playerPlane.DroppingDecoy = false;
                return;
            }

            // Don't allow inputs if mouse left the window.
            if (!this.DesktopBounds.Contains(Control.MousePosition))
            {
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
                _playerPlane.FiringBurst = true;
            }
            else
            {
                _playerPlane.FiringBurst = false;
            }

            if (buttons.HasFlag(MouseButtons.Right))
                _playerPlane.DroppingDecoy = true;
            else
                _playerPlane.DroppingDecoy = false;

            var guideAngle = GetPlayerGuidanceAngle();
            _playerPlane.SetAutoPilotAngle(guideAngle);
        }

        private float GetPlayerGuidanceAngle()
        {
            // Poll current mouse position and apply offsets and scaling.
            var mousePos = Control.MousePosition.ToD2DPoint();
            var windowPos = this.DesktopLocation.ToD2DPoint();
            var offsetPos = mousePos - windowPos;
            var scaledPos = offsetPos * World.ViewPortScaleMulti;

            // Get the scaled center point of the viewport.
            var center = new D2DPoint(World.ViewPortSize.width * 0.5f, World.ViewPortSize.height * 0.5f);
            center /= World.DEFAULT_DPI / (float)this.DeviceDpi; // Scale for DPI.

            // Compute angle from center and update guidance.
            var angle = center.AngleTo(scaledPos);

            return angle;
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

        private void DropDecoy(Decoy decoy)
        {
            _objs.EnqueueDecoy(decoy);

            if (World.IsNetGame)
                _netMan.SendNewDecoy(decoy);
        }

        private void PauseGame()
        {
            World.IsPaused = true;
        }

        private void ResumeGame()
        {
            World.IsPaused = false;
        }

        private void StopRender()
        {
            _killRender = true;

            _renderExitEvent.Wait(1000);
        }

        private void SendPlayerReset()
        {
            _netMan.SendPlaneReset(_playerPlane);
        }

        private void NextViewPlane()
        {
            if ((_playerPlane.IsDisabled || _playerPlane.HasCrashed || _playerPlane.IsAI))
                _queueNextViewId = true;
        }

        private void PrevViewPlane()
        {
            if ((_playerPlane.IsDisabled || _playerPlane.HasCrashed || _playerPlane.IsAI))
                _queuePrevViewId = true;
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
                    World.ShowLeadIndicators = !World.ShowLeadIndicators;
                    break;

                case 'm':
                    break;

                case 'n':
                    if (World.IsNetGame)
                        break;

                    World.IsPaused = true;
                    _oneStep = true;
                    break;

                case 'o':
                    _render.ToggleInfo();
                    break;

                case 'p':

                    if (World.IsNetGame)
                        break;

                    if (!World.IsPaused)
                        PauseGame();
                    else
                        ResumeGame();

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
                    PrevViewPlane();
                    break;

                case ']':
                    NextViewPlane();
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
                case MouseButtons.Left:
                    if (_playerPlane.HasCrashed)
                        _queueResetPlane = true;
                    break;

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

            if (e.KeyData.HasFlag(Keys.Enter) && e.KeyData.HasFlag(Keys.Alt))
                ToggleFullscreen();

            if (e.KeyData == Keys.Escape && _isFullScreen)
                ToggleFullscreen();
        }

        private void PolyPlaneUI_KeyUp(object sender, KeyEventArgs e)
        {
            _shiftDown = e.Shift;
            _ctrlDown = e.Control;
        }

        private void PolyPlaneUI_MouseWheel(object? sender, MouseEventArgs e)
        {
            if (e.Delta > 0)
            {
                if (!_ctrlDown)
                    _render?.DoMouseWheelUp();
                else
                    PrevViewPlane();
            }
            else
            {
                if (!_ctrlDown)
                    _render?.DoMouseWheelDown();
                else
                    NextViewPlane();
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