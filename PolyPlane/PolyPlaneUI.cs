using PolyPlane.GameObjects;
using PolyPlane.GameObjects.Managers;
using PolyPlane.Helpers;
using PolyPlane.Net;
using PolyPlane.Net.NetHost;
using PolyPlane.Rendering;
using System.Collections.Concurrent;
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

        private bool _skipRender = false;
        private bool _canRespawn = false;
        private bool _slewEnable = false;
        private bool _hasFocus = true;
        private bool _isHoldingAlt = false;
        private bool _rightMouseDown = false;
        private bool _inStartup = false;
        private float _holdAltitude = 0f;
        private string _title;

        private D2DPoint _playerPlaneSlewPos = D2DPoint.Zero;
        private D2DPoint _mousePosition = D2DPoint.Zero;
        private D2DPoint _mouseDownPosition = D2DPoint.Zero;
        private D2DPoint _prevViewObjectPosition = D2DPoint.Zero;

        private GameObjectManager _objs = World.ObjectManager;
        private FighterPlane _playerPlane;
        private DummyObject? _freeCamObject = null;

        private NetPlayHost _client;
        private NetEventManager _netMan;
        private CollisionManager _collisions;
        private Renderer _render;
        private GLRenderer _glRender;

        private FPSLimiter _fpsLimiter = new FPSLimiter();
        private SelectObjectUI? _selectObjectUI = null;
        private ConcurrentQueue<Action> _actionQueue = new ConcurrentQueue<Action>();
        private bool _hasLaunchOptions = false;

        public PolyPlaneUI()
        {
            InitializeComponent();

            _title = this.Text;

            //this.GotFocus += PolyPlaneUI_GotFocus;
            //this.LostFocus += PolyPlaneUI_LostFocus;
            this.Disposed += PolyPlaneUI_Disposed;
            //this.MouseWheel += PolyPlaneUI_MouseWheel;



            for (int i = 0; i < World.TimeOfDayPallet.Length; i++)
            {
                var color = World.TimeOfDayPallet[i];
                var skColor = color.ToSKColor();

                Debug.WriteLine($"new SKColor({skColor.Red}, {skColor.Green}, {skColor.Blue}, {skColor.Alpha}),");

            }

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

        private void EnqueueAction(Action action)
        {
            _actionQueue.Enqueue(action);
        }

        private void EnqueueResetPlane()
        {
            var resetAction = new Action(() =>
            {
                if (World.ViewObject.Equals(_playerPlane))
                    ResetPlayerPlane();
            });

            EnqueueAction(resetAction);
        }

        private void EnqueueEnableFreeCam()
        {
            var enableFreeCamAction = new Action(() =>
            {
                var currentObj = World.GetViewObject();
                _freeCamObject = new DummyObject(currentObj.Position);
                World.ViewObject = _freeCamObject;
            });

            EnqueueAction(enableFreeCamAction);
        }

        private void EnqueueDisableFreeCam()
        {
            var disableFreeCamAction = new Action(() =>
            {
                if (!_playerPlane.IsAI)
                    World.ViewObject = _playerPlane;
            });

            EnqueueAction(disableFreeCamAction);
        }

        private void AddRespawnMessage(FighterPlane plane)
        {
            //_render?.AddNewEventMessage($"'{e.PlayerName}' has respawned.", EventType.Net);
        }


        private void NetMan_PlayerRespawned(object? sender, FighterPlane plane)
        {
            if (plane.Equals(_playerPlane))
                _render.ClearHudMessage();

            AddRespawnMessage(plane);
        }

        private void NetMan_PlayerKicked(object? sender, int e)
        {
            var playerPlane = _objs.GetPlaneByPlayerID(e);

            if (playerPlane != null)
            {
                //_render?.AddNewEventMessage($"'{playerPlane.PlayerName}' has been kicked.", EventType.Net);


                //if (playerPlane.Equals(_playerPlane))
                //    _render?.NewHudMessage("You have been kicked from the server!", D2DColor.Blue);
            }
        }

        private void NetMan_PlayerDisconnected(object? sender, int e)
        {
            var playerPlane = _objs.GetPlaneByPlayerID(e);

            if (playerPlane != null)
            {
                //_render?.AddNewEventMessage($"'{playerPlane.PlayerName}' has left.", EventType.Net);
            }
        }

        private void NetMan_PlayerEventMessage(object? sender, string e)
        {
            //_render?.AddNewEventMessage(e, EventType.Net);
        }

        private void NetMan_PlayerJoined(object? sender, int e)
        {
            var playerPlane = _objs.GetPlaneByPlayerID(e);
            if (playerPlane != null)
            {
                var joinMsg = $"'{playerPlane.PlayerName}' has joined.";
                //_render?.AddNewEventMessage(joinMsg, EventType.Net);
            }
        }

        private void Objs_PlayerKilledEvent(object? sender, EventMessage e)
        {
            _client?.EnqueuePacket(new PlayerEventPacket(e.Message));
        }

        private void Client_PeerTimeoutEvent(object? sender, ENet.Peer e)
        {
            //_render.NewHudMessage("Timed out!?", D2DColor.Yellow);
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
            if (World.ViewObject.Equals(_playerPlane))
            {
                string spawnDirText = "Left";
                var guideAngle = GetPlayerGuidanceAngle();

                if (Utilities.IsPointingRight(guideAngle))
                    spawnDirText = "Right";

                //_render.NewHudMessage($"Press 'R' or left-click to respawn.\n\n Direction: {spawnDirText}", D2DColor.GreenYellow);
            }

            _canRespawn = true;
        }

        private bool DoNetGameSetup()
        {
            _inStartup = true;

            bool result = false;

            using (var config = new ClientServerConfigForm())
            {
                var dialogResult = config.ShowDialog(this);
                switch (dialogResult)
                {
                    case DialogResult.OK:
                        // Net game.

                        DoNetGameStart(config.Port, config.ServerIPAddress, config.PlaneColor, config.IsAI, config.PlayerName);

                        result = true;
                        break;

                    case DialogResult.Cancel:
                        // Solo game.

                        DoLocalGameStart(config.PlaneColor, config.IsAI, config.PlayerName);

                        result = true;

                        break;

                    case DialogResult.Abort:
                        // Aborted.
                        result = false;
                        break;
                }
            }

            _inStartup = false;

            return result;
        }

        private void DoNetGameStart(ushort port, string ip, D2DColor planeColor, bool isAI = false, string playerName = "Player")
        {
            World.IsNetGame = true;
            World.IsServer = false;

            if (isAI)
                playerName = "*(BOT) " + Utilities.GetRandomName();

            _playerPlane = GetNewPlane(planeColor, isAI, playerName);
            World.ViewObject = _playerPlane;
            _objs.AddPlane(_playerPlane);

            _client = new ClientNetHost(port, ip);
            _netMan = new NetEventManager(_client, _playerPlane);

            var collisions = new CollisionManager(_netMan);
            World.ObjectManager.SetCollisionManager(collisions);

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

            _inStartup = false;

            InitRenderer(_netMan);

            _client.Start();

            StartGameThread();
            ResumeGame();

            _render?.ClearHudMessage();

        }

        private void DoLocalGameStart(D2DColor planeColor, bool isAI = false, string playerName = "Player")
        {
            World.IsNetGame = false;
            World.IsServer = false;

            var collisions = new CollisionManager();
            World.ObjectManager.SetCollisionManager(collisions);

            if (isAI)
                playerName = "(BOT) " + Utilities.GetRandomName();

            _playerPlane = GetNewPlane(planeColor, isAI, playerName);
            World.ViewObject = _playerPlane;
            _objs.AddPlane(_playerPlane);

            InitRenderer(null);
            StartGameThread();
            ResumeGame();

            _render?.ClearHudMessage();
        }


        /// <summary>
        /// Return to server/game config screen.
        /// </summary>
        private void ResetGame()
        {
            World.IsNetGame = false;
            World.IsServer = false;
            World.FreeCameraMode = false;
            World.ServerTimeOffset = 0;

            _killRender = true;

            _gameThread.Join();

            _objs.Clear();

            if (_netMan != null)
            {
                _netMan.ImpactEvent -= HandleNewImpact;
                _netMan.PlayerIDReceived -= NetMan_PlayerIDReceived;
                _netMan.PlayerDisconnected -= NetMan_PlayerDisconnected;
                _netMan.PlayerKicked -= NetMan_PlayerKicked;
                _netMan = null;
            }


            if (_client != null)
            {
                _client?.Dispose();

                _client.PeerTimeoutEvent -= Client_PeerTimeoutEvent;
                _client.PeerDisconnectedEvent -= Client_PeerDisconnectedEvent;

                _client = null;
            }

            if (!_inStartup)
            {
                if (!DoNetGameSetup())
                    this.Close();
            }
        }

        private void Client_PeerDisconnectedEvent(object? sender, ENet.Peer e)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(() => Client_PeerDisconnectedEvent(sender, e));
            }
            else
            {
                _netMan?.SendPlayerDisconnectPacket(e.ID);
                ResetGame();
            }
        }

        private void HandleNewImpact(object? sender, ImpactEvent e)
        {
            if (this.Disposing || this.IsDisposed || _render == null || _killRender == true) return;

            HandleImpactFeedback(e);
        }

        private void HandleImpactFeedback(ImpactEvent impact)
        {
            var viewPlane = World.GetViewObject();

            if (viewPlane != null)
            {
                if (impact.Target.Equals(viewPlane))
                {
                    _glRender?.DoScreenFlash(D2DColor.Red);
                    _glRender?.DoScreenShake();
                }
                else if (impact.Attacker != null && impact.Attacker.Equals(viewPlane))
                {
                    if (impact.Target is FighterPlane && impact.DidDamage)
                        _glRender?.DoScreenFlash(D2DColor.Green);
                }
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
                this.Text = $"{_title} - CLIENT - ID: {e}";
            }
        }

        private void PolyPlaneUI_FormClosing(object sender, FormClosingEventArgs e)
        {
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
            var pos = Utilities.FindSafeSpawnPoint();

            FighterPlane plane;

            if (isAI)
                plane = new FighterPlane(pos, planeColor, Utilities.GetRandomPersonalities(2));
            else
                plane = new FighterPlane(pos, planeColor);

            plane.PlayerName = playerName;

            if (isAI && World.ShowAITags)
                plane.PlayerName += $" [{Utilities.GetPersonalityTag(plane.Personality)}]";

            plane.ThrustOn = true;
            plane.Velocity = new D2DPoint(World.PlaneSpawnVelo, 0f);

            plane.FireBulletCallback = b =>
            {
                _objs.EnqueueBullet(b);

                if (b.Owner.Equals(World.ViewObject))
                    _glRender?.DoScreenShake(2f);

                if (World.IsNetGame)
                    _netMan.SendNewBulletPacket(b);
            };

            plane.FireMissileCallback = (m) =>
            {
                _objs.EnqueueMissile(m);

                if (World.IsNetGame)
                    _netMan.SendNewMissilePacket(m);

            };

            plane.PlayerHitCallback = HandleImpactFeedback;
            plane.DropDecoyCallback = DropDecoy;

            return plane;
        }

        private void ResetAIPlane(FighterPlane plane)
        {
            if (plane.Equals(_playerPlane))
            {
                ResetPlayerPlane();
                return;
            }

            AddRespawnMessage(plane);
            plane.RespawnPlane(Utilities.FindSafeSpawnPoint());
        }

        private void ResetPlayerPlane()
        {
            if (World.IsNetGame && !_canRespawn)
                return;

            if (World.IsNetGame)
            {
                SendPlayerReset();

                // Prevent duplicate reset packets being sent.
                _playerPlane.RespawnQueued = true;
            }
            else
            {
                _playerPlane.RespawnPlane(Utilities.FindSafeSpawnPoint());
                AddRespawnMessage(_playerPlane);

            }

            _canRespawn = false;
            //_render.ClearHudMessage();

            if (!_playerPlane.IsAI)
                World.ViewObject = _playerPlane;
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

        private void InitRenderer(NetEventManager netMan)
        {
            //_render?.Dispose();
            //_render = new Renderer(RenderTarget, _netMan);

            _glRender?.Dispose();

            _glRender = new GLRenderer(RenderTarget, _netMan);
            var control = _glRender.InitGLControl(RenderTarget);

            control.KeyPress += PolyPlaneUI_KeyPress;
            control.MouseDown += RenderTarget_MouseDown;
            control.KeyDown += PolyPlaneUI_KeyDown;
            control.KeyUp += PolyPlaneUI_KeyUp;
            control.MouseMove += RenderTarget_MouseMove;
            control.MouseWheel += PolyPlaneUI_MouseWheel;
            control.LostFocus += PolyPlaneUI_LostFocus;
            control.GotFocus += PolyPlaneUI_GotFocus;
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
            if (_oneStep)
                World.IsPaused = false;

            //_render.InitGfx();

            World.Update();

            var dt = World.CurrentDT;

            if (_killRender)
                return;

            // Process any queued actions.
            ProcessQueuedActions();

            // Poll mouse buttons and position.
            DoMouseButtons();

            // Get the current view object.
            var viewObject = GetViewObjectOrCamera(World.GetViewObject());

            // Process net events during net games.
            if (World.IsNetGame)
                _netMan.HandleNetEvents(dt);

            // Update/advance all objects.
            if (!World.IsPaused || _oneStep)
            {
                _objs.Update(dt);

                if (_oneStep)
                    World.IsPaused = true;

                _oneStep = false;

                // Do G-Force screen shake effect.
                if (viewObject is FighterPlane plane)
                {
                    //if (plane.GForce > World.SCREEN_SHAKE_G)
                    //    _render.DoScreenShake(plane.GForce / 4f);
                }
            }

            //_render.CollisionTime = _collisionTime;
            //_render.UpdateTime = _updateTime;

            if (World.IsNetGame)
                _netMan.HandleNetEvents(dt);

            //if (!_skipRender && !_killRender && this.WindowState != FormWindowState.Minimized)
            //    _render.RenderFrame(viewObject, dt);
            //else
            //    _fpsLimiter.Wait(World.TARGET_FPS);

            if (!_skipRender && !_killRender && this.WindowState != FormWindowState.Minimized)
                _glRender?.RenderFrame(viewObject, dt);
            else
                _fpsLimiter.Wait(World.TARGET_FPS);

            //try
            //{
            //    this.Invoke(() => _glRender.RenderFrame(viewObject, dt));

            //}
            //catch
            //{

            //}
            //_glRender.RenderFrame(viewObject, dt);

            if (_slewEnable)
            {
                _playerPlane.SetPosition(_playerPlaneSlewPos, _playerPlane.PlayerGuideAngle);
                _playerPlane.RotationSpeed = 0f;
                _playerPlane.Velocity = D2DPoint.Zero;
            }

            if (_playerPlane.HasCrashed)
                EnableRespawn();

            // Hold altitude while spectating. 
            if (viewObject.Equals(_playerPlane) == false)
                _isHoldingAlt = true;

            // Hold altitude while viewing/typing in net chat.
            if ((_netMan != null && _netMan.ChatInterface.ChatIsActive))
                _isHoldingAlt = true;

            // Make player plane maintain its current altitude.
            if (_isHoldingAlt)
            {
                if (_holdAltitude == 0f)
                    _holdAltitude = _playerPlane.Altitude;

                var altHoldAngle = Utilities.MaintainAltitudeAngle(_playerPlane, _holdAltitude);
                _playerPlane.SetGuidanceAngle(altHoldAngle);
            }
            else
            {
                _holdAltitude = 0f;
            }

            HandleAIPlaneRespawn();
        }

        /// <summary>
        /// Returns the specified object if it is not null and not expired.
        /// 
        /// Otherwise enable free camera and return a dummy object at the last known position to keep the view active.
        /// </summary>
        /// <param name="viewObject"></param>
        /// <returns></returns>
        private GameObject GetViewObjectOrCamera(GameObject viewObject)
        {
            GameObject viewObj = null;

            if (viewObject != null)
            {
                _prevViewObjectPosition = viewObject.Position;

                if (viewObject.IsExpired)
                {
                    World.FreeCameraMode = true;
                    _freeCamObject = new DummyObject(_prevViewObjectPosition);
                    World.ViewObject = _freeCamObject;
                    viewObj = _freeCamObject;
                }
                else
                {
                    viewObj = viewObject;
                }
            }
            else
            {
                World.FreeCameraMode = true;
                _freeCamObject = new DummyObject(_prevViewObjectPosition);
                World.ViewObject = _freeCamObject;
                viewObj = _freeCamObject;
            }

            return viewObj;
        }

        private void HandleAIPlaneRespawn()
        {
            if (!World.RespawnAIPlanes)
                return;

            for (int i = 0; i < _objs.Planes.Count; i++)
            {
                var plane = _objs.Planes[i];
                if (plane.IsAI && plane.HasCrashed && plane.AIRespawnReady)
                {
                    if (!plane.RespawnQueued)
                        ResetAIPlane(plane);
                }
            }
        }

        private void DoMouseButtons()
        {
            var isPaused = World.IsPaused;
            var mouseInViewport = this.DesktopBounds.Contains(Control.MousePosition) && _hasFocus;

            // Don't allow inputs if mouse left the window.
            if (!mouseInViewport && !_playerPlane.IsAI)
            {
                _playerPlane.FiringBurst = false;
                _playerPlane.DroppingDecoy = false;
                _isHoldingAlt = true; // Hold the plane at the current altitude if mouse leaves the window.
                _rightMouseDown = false;
                return;
            }
            else
            {
                _isHoldingAlt = false;
            }

            var buttons = Control.MouseButtons;

            if (!World.FreeCameraMode && !_playerPlane.IsAI)
            {
                if (!isPaused && (buttons & MouseButtons.Left) == MouseButtons.Left)
                {
                    _playerPlane.FiringBurst = true;
                }
                else
                {
                    _playerPlane.FiringBurst = false;
                }
            }

            if (mouseInViewport && (buttons & MouseButtons.Right) == MouseButtons.Right)
            {
                if (!isPaused && !World.FreeCameraMode && !_playerPlane.IsAI)
                    _playerPlane.DroppingDecoy = true;

                _rightMouseDown = true;
            }
            else
            {
                if (!World.FreeCameraMode && !_playerPlane.IsAI)
                    _playerPlane.DroppingDecoy = false;

                _rightMouseDown = false;
            }

            if (!World.FreeCameraMode)
            {
                var guideAngle = GetPlayerGuidanceAngle();
                _playerPlane.SetGuidanceAngle(guideAngle);
            }
            else
            {
                if (_rightMouseDown)
                    DoFreeCamMovement();
            }
        }

        private float GetPlayerGuidanceAngle()
        {
            // Scale the mouse position for the current view scale.
            var mousePos = _mousePosition;
            var scaledPos = mousePos * World.ViewPortScaleMulti;

            // Get the scaled center point of the viewport.
            var center = new D2DPoint(World.ViewPortSize.width * 0.5f, World.ViewPortSize.height * 0.5f);
            center /= World.DEFAULT_DPI / (float)this.DeviceDpi; // Scale for DPI.

            // Compute angle from center.
            var angle = scaledPos.AngleTo(center);

            return angle;
        }

        private void DoFreeCamMovement()
        {
            if (_freeCamObject == null)
                return;

            var mousePos = _mousePosition;

            var center = _mouseDownPosition;
            center /= World.DEFAULT_DPI / (float)this.DeviceDpi; // Scale for DPI.

            var dist = mousePos.DistanceTo(center);
            var angle = mousePos.AngleTo(center);

            if (dist > 0f)
            {
                _freeCamObject.Position += Utilities.AngleToVectorDegrees(angle, dist * (0.5f * Math.Clamp(1f - Utilities.Factor(World.ZoomScale, 0.5f), 0.1f, 1f)));
            }

            if (_freeCamObject.Position.Y >= 0f)
                _freeCamObject.Position = new D2DPoint(_freeCamObject.Position.X, 0f);
        }

        private void ProcessQueuedActions()
        {
            while (_actionQueue.Count > 0)
            {
                if (_actionQueue.TryDequeue(out var action))
                    action();
            }
        }

        private void DropDecoy(Decoy decoy)
        {
            _objs.EnqueueDecoy(decoy);

            if (World.IsNetGame)
                _netMan.SendNewDecoyPacket(decoy);
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
            _netMan.ClientSendPlaneReset(_playerPlane);
        }

        private void NextViewPlane()
        {
            if ((_playerPlane.IsDisabled || _playerPlane.HasCrashed || _playerPlane.IsAI))
            {
                World.FreeCameraMode = false;
                EnqueueAction(World.NextViewPlane);
            }
        }

        private void PrevViewPlane()
        {
            if ((_playerPlane.IsDisabled || _playerPlane.HasCrashed || _playerPlane.IsAI))
            {
                World.FreeCameraMode = false;
                EnqueueAction(World.PrevViewPlane);
            }
        }

        private void ShowSelectObjectUI()
        {
            if (_selectObjectUI == null || (_selectObjectUI != null && _selectObjectUI.IsDisposed))
                _selectObjectUI = new SelectObjectUI(_objs);

            _selectObjectUI.WindowState = FormWindowState.Normal;
            _selectObjectUI.Show();
            _selectObjectUI.BringToFront();
            _selectObjectUI.UpdateObjectList();
        }

        private void PolyPlaneUI_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (_netMan != null)
            {
                _netMan.ChatInterface.NewKeyPress(e.KeyChar);

                if (_netMan.ChatInterface.ChatIsActive)
                    return;
            }

            var key = char.ToLower(e.KeyChar);

            switch (key)
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
                    {
                        var clearPlanesAction = new Action(() =>
                        {
                            _objs.Planes.ForEach(p =>
                            {
                                if (!p.Equals(_playerPlane))
                                    p.IsExpired = true;
                            });
                        });

                        EnqueueAction(clearPlanesAction);
                    }
                    break;

                case 'd':
                    break;

                case 'e':
                    break;

                case 'f':

                    World.FreeCameraMode = !World.FreeCameraMode;

                    if (World.FreeCameraMode)
                        EnqueueEnableFreeCam();
                    else
                        EnqueueDisableFreeCam();

                    break;

                case 'g':

                    if (World.IsNetGame)
                        break;

                    World.GunsOnly = !World.GunsOnly;
                    break;

                case 'h':
                    _glRender?.ToggleHelp();
                    break;

                case 'i':
                    World.ShowAero = !World.ShowAero;
                    break;

                case 'k':
                    World.MissileRegen = !World.MissileRegen;
                    break;

                case 'l':
                    World.ShowLeadIndicators = !World.ShowLeadIndicators;
                    break;

                case 'm':
                    World.ShowMissilesOnRadar = !World.ShowMissilesOnRadar;
                    break;

                case 'n':
                    if (World.IsNetGame)
                        break;

                    World.IsPaused = true;
                    _oneStep = true;
                    break;

                case 'o':
                    _glRender?.ToggleInfo();
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

                    EnqueueResetPlane();

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
                        EnqueueAction(SpawnAIPlane);

                    break;

                case 'v':
                    ShowSelectObjectUI();
                    break;

                case 'y':
                    break;

                case '=' or '+':
                    if (_shiftDown)
                    {
                        //_render.HudScale += 0.01f;
                    }
                    else
                    {
                        //_render?.ZoomIn();
                    }
                    break;

                case '-' or '_':

                    if (_shiftDown)
                    {
                        //_render.HudScale -= 0.01f;
                    }
                    else
                    {
                        //_render?.ZoomOut();
                    }
                    break;

                case '[':
                    PrevViewPlane();
                    break;

                case ']':
                    NextViewPlane();
                    break;

                case (char)8: //Backspace
                    World.ViewObject = _playerPlane;
                    World.FreeCameraMode = false;
                    EnqueueDisableFreeCam();

                    break;

                case (char)9: //Tab
                    _glRender?.ToggleScore();
                    break;

                case ' ':
                    TargetLockedWithMissile();
                    break;

            }
        }

        private void RenderTarget_MouseDown(object sender, MouseEventArgs e)
        {
            switch (e.Button)
            {
                case MouseButtons.Left:
                    if (_playerPlane.HasCrashed)
                        EnqueueResetPlane();
                    break;

                case MouseButtons.Middle:
                    TargetLockedWithMissile();
                    break;

            }

            _mouseDownPosition = e.Location.ToD2DPoint();
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
                            World.TargetDT += 0.002f;

                        break;

                    case Keys.OemMinus:

                        if (!World.IsNetGame)
                            World.TargetDT -= 0.002f;

                        break;
                }
            }

            //if (e.KeyData.HasFlag(Keys.F2))
            //    _render?.ToggleHUD();

            if (e.KeyData.HasFlag(Keys.Enter) && e.KeyData.HasFlag(Keys.Alt))
                ToggleFullscreen();

            if (e.KeyData == Keys.Escape && _isFullScreen)
            {
                if (!World.IsNetGame || (World.IsNetGame && !_netMan.ChatInterface.ChatIsActive))
                    ToggleFullscreen();
            }
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
                    //_render?.DoMouseWheelUp();
                    _glRender?.ZoomIn();
                else
                    PrevViewPlane();
            }
            else
            {
                if (!_ctrlDown)
                    //_render?.DoMouseWheelDown();
                    _glRender?.ZoomOut();

                else
                    NextViewPlane();
            }
        }

        private void PolyPlaneUI_Shown(object sender, EventArgs e)
        {
            if (_hasLaunchOptions)
            {
                var o = World.LaunchOptions;
                DoNetGameStart(o.Port, o.IPAddress, D2DColor.Randomly(), o.IsAI, o.PlayerName);

                if (o.DisableRender)
                {
                    _skipRender = true;
                    this.WindowState = FormWindowState.Minimized;

                }

                return;
            }

            if (!DoNetGameSetup())
            {
                this.Close();
            }
        }

        private void RenderTarget_MouseMove(object sender, MouseEventArgs e)
        {
            _mousePosition = e.Location.ToD2DPoint();
        }
    }
}