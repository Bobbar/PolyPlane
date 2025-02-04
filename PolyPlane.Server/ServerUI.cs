using PolyPlane.AI_Behavior;
using PolyPlane.GameObjects;
using PolyPlane.GameObjects.Manager;
using PolyPlane.GameObjects.Tools;
using PolyPlane.Helpers;
using PolyPlane.Net;
using PolyPlane.Net.Discovery;
using PolyPlane.Net.NetHost;
using PolyPlane.Rendering;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;


namespace PolyPlane.Server
{
    public partial class ServerUI : Form
    {
        private bool _stopRender = false;
        private bool _killThread = false;

        private AIPersonality _aiPersonality = AIPersonality.Normal;
        private Thread _gameThread;

        private GameTimer _discoveryTimer = new GameTimer(2f, true);

        private ManualResetEventSlim _pauseRenderEvent = new ManualResetEventSlim(true);

        private Stopwatch _bwTimer = new Stopwatch();
        private Stopwatch _timer = new Stopwatch();
        private TimeSpan _renderTime = new TimeSpan();
        private SmoothDouble _updateTimeSmooth = new SmoothDouble(10);
        private SmoothDouble _collisionTimeSmooth = new SmoothDouble(10);
        private SmoothDouble _netTimeSmooth = new SmoothDouble(10);
        private SmoothDouble _fpsSmooth = new SmoothDouble(10);
        private SmoothDouble _sentBytesSmooth = new SmoothDouble(50);
        private SmoothDouble _recBytesSmooth = new SmoothDouble(50);
        private SmoothDouble _sentPacketsSmooth = new SmoothDouble(50);
        private SmoothDouble _recPacketsSmooth = new SmoothDouble(50);

        private double _renderFPS = 0;
        private double _lastFrameTime = 0;
        private uint _lastRecBytes = 0;
        private uint _lastSentBytes = 0;
        private uint _lastRecPackets = 0;
        private uint _lastSentPackets = 0;

        private string _address;
        private string _serverName;
        private int _port;
        private GameObjectManager _objs = World.ObjectManager;
        private NetEventManager _netMan;
        private DiscoveryServer _discovery;
        private CollisionManager _collisions;
        private NetPlayHost _server;
        private Renderer _render = null;
        private FPSLimiter _fpsLimiter = new FPSLimiter();

        private ConcurrentQueue<Action> _actionQueue = new ConcurrentQueue<Action>();
        private BindingList<NetPlayer> _currentPlayers = new BindingList<NetPlayer>();

        private Form _viewPort = null;

        private System.Windows.Forms.Timer _updateTimer = new System.Windows.Forms.Timer();

        public ServerUI()
        {
            InitializeComponent();

            var localIP = Utilities.GetLocalIP();

#if DEBUG
            localIP = "127.0.0.1";
            EnableDiscoveryCheckBox.Checked = false;
#endif

            if (localIP != null)
            {
                _address = localIP;
                AddressTextBox.Text = localIP;
            }

            this.Disposed += ServerUI_Disposed; ;

            _updateTimer.Tick += UpdateTimer_Tick;
            _updateTimer.Interval = 32;

            // Periodically broadcast discovery packets.
            _discoveryTimer.TriggerCallback = () => _discovery?.BroadcastServerInfo(new DiscoveryPacket(_address, _serverName, _port));

            AITypeComboBox.Items.Clear();
            AITypeComboBox.DataSource = Enum.GetValues<AIPersonality>();

            _currentPlayers.RaiseListChangedEvents = true;

            PlayersListBox.DataBindings.Clear();
            PlayersListBox.DataSource = _currentPlayers;

            ChatMessageTextBox.MaxLength = ChatInterface.MAX_CHARS;

            TimeOfDaySlider.Maximum = (int)World.MAX_TIMEOFDAY;
            DeltaTimeNumeric.Value = (decimal)World.TargetDT;
        }

        private void EnqueueAction(Action action)
        {
            _actionQueue.Enqueue(action);
        }

        private void StartServerButton_Click(object sender, EventArgs e)
        {
            if (ushort.TryParse(PortTextBox.Text.Trim(), out ushort port))
            {
                _port = port;
                World.IsNetGame = true;
                World.IsServer = true;
                var addy = AddressTextBox.Text.Trim();
                _address = addy;
                _serverName = ServerNameTextBox.Text.Trim();

                _server = new ServerNetHost(port, addy);
                _netMan = new NetEventManager(_server);
                _discovery = new DiscoveryServer();
                _collisions = new CollisionManager(_netMan);

                _netMan.PlayerDisconnected += NetMan_PlayerDisconnected;
                _netMan.PlayerJoined += NetMan_PlayerJoined;
                _netMan.PlayerRespawned += NetMan_PlayerRespawned;
                _netMan.NewChatMessage += NetMan_NewChatMessage;
                _netMan.PlayerEventMessage += NetMan_PlayerEventMessage;
                _server.PeerTimeoutEvent += Server_PeerTimeoutEvent;

                _objs.PlayerKilledEvent += PlayerKilledEvent;
                _objs.NewPlayerEvent += NewPlayerEvent;

                _server.PeerDisconnectedEvent += Server_PeerDisconnectedEvent;

                if (EnableDiscoveryCheckBox.Checked)
                    _discoveryTimer.Start();

                StartGameThread();
                _updateTimer.Start();

                AddressTextBox.Enabled = false;
                PortTextBox.Enabled = false;
                StartServerButton.Enabled = false;
                ServerNameTextBox.Enabled = false;
            }
        }

        private void PlayerDisconnected(int playerID)
        {
            _netMan.SendPlayerDisconnectPacket((uint)playerID);

            var netPlayer = _currentPlayers.Where(p => p.ID.PlayerID == playerID).FirstOrDefault();
            if (netPlayer != null)
            {
                _currentPlayers.Remove(netPlayer);
                AddNewEventMessage($"'{netPlayer.Name}' has left.");
                _netMan.ClearImpacts(playerID);
            }
        }

        private void Server_PeerTimeoutEvent(object? sender, ENet.Peer e)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(() => Server_PeerTimeoutEvent(sender, e));
            }
            else
            {
                PlayerDisconnected((int)e.ID);
            }
        }

        private void NetMan_PlayerEventMessage(object? sender, string e)
        {
            AddNewEventMessage(e);
        }

        private void NetMan_PlayerRespawned(object? sender, FighterPlane e)
        {
            AddNewEventMessage($"'{e.PlayerName}' has respawned.");
        }

        private void NewPlayerEvent(object? sender, FighterPlane e)
        {
            var joinMsg = $"'{e.PlayerName}' has joined.";
            AddNewEventMessage(joinMsg);

            _server.EnqueuePacket(new PlayerEventPacket(joinMsg));
        }

        private void PlayerKilledEvent(object? sender, EventMessage e)
        {
            AddNewEventMessage(e.Message);

            _server.EnqueuePacket(new PlayerEventPacket(e.Message));
        }

        private void NetMan_NewChatMessage(object? sender, ChatPacket e)
        {
            AddNewEventMessage($"{e.PlayerName}: {e.Message}");
        }

        private void NetMan_PlayerJoined(object? sender, int e)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(() => NetMan_PlayerJoined(sender, e));
            }
            else
            {
                var playerPlane = _objs.GetPlaneByPlayerID(e);
                if (playerPlane != null)
                {
                    var netPlayer = new NetPlayer(playerPlane.ID, playerPlane.PlayerName);
                    var peer = _server.GetPeer(playerPlane.PlayerID);
                    if (peer.HasValue)
                    {
                        netPlayer.IP = peer.Value.IP;
                        netPlayer.Latency = peer.Value.RoundTripTime.ToString();
                    }

                    try
                    {
                        // The first new player added causes an OutOfRangeException
                        // on the bound listbox selectindex method, for some reason...
                        _currentPlayers.Add(netPlayer);
                    }
                    catch (ArgumentOutOfRangeException)
                    { }

                    var joinMsg = $"'{playerPlane.PlayerName}' has joined.";
                    AddNewEventMessage(joinMsg);
                    _server.EnqueuePacket(new PlayerEventPacket(joinMsg));
                }

            }
        }

        // TODO: These events are probably redundant...
        private void NetMan_PlayerDisconnected(object? sender, int e)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(() => NetMan_PlayerDisconnected(sender, e));
            }
            else
            {
                PlayerDisconnected(e);
            }
        }

        private void Server_PeerDisconnectedEvent(object? sender, ENet.Peer e)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(() => Server_PeerDisconnectedEvent(sender, e));
            }
            else
            {
                PlayerDisconnected((int)e.ID);
            }
        }

        private void AddNewEventMessage(string message)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(() => AddNewEventMessage(message));
            }
            else
            {
                ChatBox.Items.Add(message);
                ChatBox.TopIndex = ChatBox.Items.Count - 1;
            }
        }

        private void SendChatMessage(string message)
        {
            _netMan.ChatInterface.SendMessage(message);
            ChatMessageTextBox.Text = string.Empty;

            AddNewEventMessage($"Server: {message}");
        }

        private void ServerUI_Disposed(object? sender, EventArgs e)
        {
            _killThread = true;
            _server?.Stop();
            _server?.Dispose();
            _fpsLimiter?.Dispose();

            _discovery?.StopListen();
            _discovery?.Dispose();
        }

        private void StartGameThread()
        {
            _server.Start();

            _gameThread = new Thread(GameLoop);
            _gameThread.Priority = ThreadPriority.AboveNormal;
            _gameThread.Start();
        }

        private void GameLoop()
        {
            _lastFrameTime = World.CurrentTimeMs();

            while (!this.Disposing && !_killThread)
            {
                AdvanceServer();

                if (!_pauseRenderEvent.Wait(0))
                {
                    World.IsPaused = true;
                    _pauseRenderEvent.Set();
                }
            }
        }

        private void AdvanceServer()
        {
            World.Update();

            var dt = World.CurrentDT;
            var now = World.CurrentTimeMs();

            // Compute time elapsed since last frame.
            var elapFrameTime = now - _lastFrameTime;
            _lastFrameTime = now;

            // Compute current FPS.
            var fps = 1000d / elapFrameTime;
            _renderFPS = fps;

            ProcessQueuedActions();

            // Process net events.
            _timer.Restart();

            _netMan.HandleNetEvents(dt);

            _timer.Stop();
            _netTimeSmooth.Add((float)_timer.Elapsed.TotalMilliseconds);

            // Update/advance objects.
            if (!World.IsPaused)
            {
                // Do collisions.
                _timer.Restart();

                _collisions.DoCollisions(dt);

                _timer.Stop();
                _collisionTimeSmooth.Add((float)_timer.Elapsed.TotalMilliseconds);

                // Update all objects and world.
                _timer.Restart();

                _objs.Update(dt);

                _timer.Stop();
                _updateTimeSmooth.Add((float)_timer.Elapsed.TotalMilliseconds);
            }

            // Render if spectate viewport is active.
            _renderTime = TimeSpan.Zero;

            if (_render != null && !_stopRender)
            {
                _timer.Restart();

                FighterPlane viewPlane = World.GetViewPlane();

                if (viewPlane != null)
                    _render.RenderFrame(viewPlane, dt);

                _timer.Stop();
                _renderTime = _timer.Elapsed;
            }

            _discoveryTimer.Update(dt);

            HandleAIPlaneRespawn();

            _fpsLimiter.Wait(World.NET_SERVER_FPS);
        }

        private void HandleAIPlaneRespawn()
        {
            if (!World.RespawnAIPlanes)
                return;

            foreach (var plane in _objs.Planes)
            {
                if (plane.IsAI && plane.HasCrashed && plane.AIRespawnReady)
                {
                    ResetPlane(plane);
                }
            }
        }

        private void ResetPlane(FighterPlane plane)
        {
            _netMan.SendPlaneReset(plane);

            plane.ThrustOn = true;
            plane.Velocity = new D2DPoint(World.PlaneSpawnVelo, 0f);
            plane.RotationSpeed = 0f;
            plane.IsDisabled = false;
            plane.FixPlane();
            plane.SetPosition(Utilities.FindSafeSpawnPoint(), 0f);
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
            _netMan.SendNewDecoyPacket(decoy);
        }

        private FighterPlane GetAIPlane(AIPersonality? personality = null)
        {
            var pos = Utilities.FindSafeSpawnPoint();

            FighterPlane aiPlane;

            if (personality.HasValue)
                aiPlane = new FighterPlane(pos, personality.Value, World.GetNextPlayerId());
            else
                aiPlane = new FighterPlane(pos, Utilities.GetRandomPersonalities(2), World.GetNextPlayerId());

            aiPlane.PlayerName = "(BOT) " + Utilities.GetRandomName();

            aiPlane.FireMissileCallback = (m) =>
            {
                _objs.EnqueueMissile(m);
                _netMan.SendNewMissilePacket(m);
            };


            aiPlane.FireBulletCallback = b =>
            {
                _objs.EnqueueBullet(b);
                _netMan.SendNewBulletPacket(b);
            };

            aiPlane.DropDecoyCallback = DropDecoy;

            aiPlane.Velocity = new D2DPoint(World.PlaneSpawnVelo, 0f);

            aiPlane.PlayerKilledCallback += _netMan.HandlePlayerKilled;

            return aiPlane;
        }

        private void SpawnAIPlane()
        {
            var aiPlane = GetAIPlane();

            _objs.AddPlane(aiPlane);
            _netMan.ServerSendOtherPlanes();
        }

        private void SpawnAIPlane(AIPersonality personality)
        {
            var aiPlane = GetAIPlane(personality);

            _objs.AddPlane(aiPlane);
            _netMan.ServerSendOtherPlanes();
        }

        private void RemoveAIPlanes()
        {
            foreach (var plane in _objs.Planes)
            {
                if (plane.IsAI)
                    plane.IsExpired = true;
            }
        }

        private void UpdatePlayerList()
        {
            for (int i = 0; i < _currentPlayers.Count; i++)
            {
                var player = _currentPlayers[i];
                var contains = _objs.Contains(player.ID);

                if (!contains)
                    _currentPlayers.RemoveAt(i);
            }

            for (int i = 0; i < _currentPlayers.Count; i++)
            {
                var player = _currentPlayers[i];

                var peer = _server.GetPeer(player.ID.PlayerID);
                if (peer.HasValue)
                {
                    if (peer.Value.RoundTripTime.ToString() != player.Latency)
                    {
                        player.Latency = peer.Value.RoundTripTime.ToString();
                        _currentPlayers.ResetItem(i);
                    }

                    if (peer.Value.PacketsLost.ToString() != player.PacketLoss)
                    {
                        player.PacketLoss = peer.Value.PacketsLost.ToString();
                        _currentPlayers.ResetItem(i);
                    }
                }
            }

            for (int i = 0; i < _currentPlayers.Count; i++)
            {
                var player = _currentPlayers[i];

                var playerPlane = _objs.GetPlaneByPlayerID(player.ID.PlayerID);
                if (playerPlane != null)
                {
                    player.Score = playerPlane.Kills;
                    _currentPlayers.ResetItem(i);
                }
            }

        }

        private void KickSelectedPlayer()
        {
            if (PlayersListBox.SelectedIndex < 0)
                return;

            var player = _currentPlayers[PlayersListBox.SelectedIndex];

            var playerPlane = _objs.GetPlaneByPlayerID(player.ID.PlayerID);
            if (playerPlane != null)
                playerPlane.IsExpired = true;

            var kickPacket = new BasicPacket(PacketTypes.KickPlayer, player.ID);

            _server.EnqueuePacket(kickPacket, SendType.ToAll);
        }

        private string GetInfo()
        {
            UpdateBandwidthStats();

            string infoText = string.Empty;
            infoText += $"Paused: {World.IsPaused}\n\n";

            var numObj = _objs.TotalObjects;
            infoText += $"Num Objects: {numObj}\n";
            infoText += $"Planes: {_objs.Planes.Count}\n";
            infoText += $"Clients: {_server.PeersCount}\n";

            infoText += $"FPS: {Math.Round(_fpsSmooth.Add(_renderFPS), 0)}\n";
            infoText += $"Update ms: {Math.Round(_updateTimeSmooth.Current, 2)}\n";
            infoText += $"Collision ms: {Math.Round(_collisionTimeSmooth.Current, 2)}\n";
            infoText += $"Net ms: {Math.Round(_netTimeSmooth.Current, 2)}\n";

            if (_viewPort != null)
                infoText += $"Render ms: {Math.Round(_renderTime.TotalMilliseconds, 2)}\n";

            infoText += $"Packet Delay: {Math.Round(_netMan.PacketDelay, 2)}\n";
            infoText += $"Packets Rec/s: {Math.Round(_recPacketsSmooth.Current, 2)}\n";
            infoText += $"Packets Sent/s: {Math.Round(_sentPacketsSmooth.Current, 2)}\n";
            infoText += $"MB Rec/s: {Math.Round(_recBytesSmooth.Current, 3)}\n";
            infoText += $"MB Sent/s: {Math.Round(_sentBytesSmooth.Current, 3)}\n";
            infoText += $"DT: {Math.Round(World.TargetDT, 4)}\n";
            infoText += $"TimeOfDay: {Math.Round(World.TimeOfDay, 2)}\n";

            return infoText;
        }

        private void UpdateBandwidthStats()
        {
            const float BYTES_PER_MB = 1000000f;

            if (!_bwTimer.IsRunning)
                _bwTimer.Start();

            _bwTimer.Stop();
            var elap = _bwTimer.Elapsed;
            var recBytesDiff = _netMan.Host.BytesReceived - _lastRecBytes;
            var sentBytesDiff = _netMan.Host.BytesSent - _lastSentBytes;

            var recPacketsDiff = _netMan.Host.PacketsReceived - _lastRecPackets;
            var sentPacketsDiff = _netMan.Host.PacketsSent - _lastSentPackets;

            _lastRecBytes = _netMan.Host.BytesReceived;
            _lastSentBytes = _netMan.Host.BytesSent;
            _lastRecPackets = _netMan.Host.PacketsReceived;
            _lastSentPackets = _netMan.Host.PacketsSent;

            _recPacketsSmooth.Add(recPacketsDiff / elap.TotalSeconds);
            _sentPacketsSmooth.Add(sentPacketsDiff / elap.TotalSeconds);

            _recBytesSmooth.Add((recBytesDiff / elap.TotalSeconds) / BYTES_PER_MB);
            _sentBytesSmooth.Add((sentBytesDiff / elap.TotalSeconds) / BYTES_PER_MB);
            _bwTimer.Restart();
        }

        private void InitViewPort()
        {
            if (_objs.Planes.Count == 0)
                return;

            if (_viewPort == null || (_viewPort != null && _viewPort.IsDisposed))
            {
                _viewPort = new Form();
                _viewPort.Size = new Size(1346, 814);
                _viewPort.KeyPress += ViewPort_KeyPress;
                _viewPort.FormClosing += ViewPort_FormClosing;
                _viewPort.MouseWheel += ViewPort_MouseWheel;

                _render = new Renderer(_viewPort, _netMan);
            }

            _viewPort.WindowState = FormWindowState.Normal;
            _viewPort.Show();
            _viewPort.BringToFront();

            _stopRender = false;
        }

        private void ViewPort_MouseWheel(object? sender, MouseEventArgs e)
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

        private void ViewPort_FormClosing(object? sender, FormClosingEventArgs e)
        {
            e.Cancel = true;

            _viewPort.WindowState = FormWindowState.Minimized;

            _stopRender = true;

            _gameThread.Join(100);
        }

        private void ViewPort_KeyPress(object? sender, KeyPressEventArgs e)
        {
            switch (e.KeyChar)
            {
                case '[':
                    EnqueueAction(() => World.PrevViewPlane());
                    break;
                case ']':

                    EnqueueAction(() => World.NextViewPlane());
                    break;
                case '=' or '+':
                    _render?.ZoomIn();
                    break;

                case '-' or '_':
                    _render?.ZoomOut();
                    break;

                case 'o':
                    _render.ToggleInfo();
                    break;
            }
        }

        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            InfoLabel.Text = GetInfo();
            UpdatePlayerList();

            TimeOfDaySlider.Value = (int)World.TimeOfDay;
            TimeOfDayLabel.Text = $"Time of day: {Math.Round(World.TimeOfDay, 2)}";
        }

        private void PauseButton_Click(object sender, EventArgs e)
        {
            var pauseAction = new Action(() =>
            {
                World.IsPaused = !World.IsPaused;
                _netMan.ServerSendGameState();
            });

            EnqueueAction(pauseAction);
        }

        private void SpawnAIPlaneButton_Click(object sender, EventArgs e)
        {
            var selected = AITypeComboBox.SelectedItem;

            if (selected != null)
            {
                var personality = (AIPersonality)selected;
                _aiPersonality = personality;

                EnqueueAction(() => SpawnAIPlane(_aiPersonality));
            }
        }

        private void ShowViewPortButton_Click(object sender, EventArgs e)
        {
            InitViewPort();
        }

        private void SpawnRandomAIButton_Click(object sender, EventArgs e)
        {
            EnqueueAction(SpawnAIPlane);
        }

        private void RemoveAIPlanesButton_Click(object sender, EventArgs e)
        {
            EnqueueAction(RemoveAIPlanes);
        }

        private void kickToolStripMenuItem_Click(object sender, EventArgs e)
        {
            KickSelectedPlayer();
        }

        private void SentChatButton_Click(object sender, EventArgs e)
        {
            SendChatMessage(ChatMessageTextBox.Text);
        }

        private void ChatMessageTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                SendChatMessage(ChatMessageTextBox.Text);
            }
        }

        private void TimeOfDaySlider_Scroll(object sender, EventArgs e)
        {
            World.TimeOfDay = TimeOfDaySlider.Value;
            _netMan.ServerSendGameState();
        }

        private void EnableDiscoveryCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (EnableDiscoveryCheckBox.Checked)
                _discoveryTimer.Start();
            else
                _discoveryTimer.Stop();
        }

        private void GunsOnlyCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            var toggleGuns = new Action(() =>
            {
                World.GunsOnly = GunsOnlyCheckBox.Checked;
                _netMan.ServerSendGameState();
            });

            EnqueueAction(toggleGuns);
        }

        private void DeltaTimeNumeric_ValueChanged(object sender, EventArgs e)
        {
            if (_server == null)
                return;

            World.TargetDT = (float)DeltaTimeNumeric.Value;
            _netMan.ServerSendGameState();
        }

        private void DefaultDTButton_Click(object sender, EventArgs e)
        {
            World.TargetDT = World.DEFAULT_DT;
            DeltaTimeNumeric.Value = (decimal)World.TargetDT;
        }
    }
}
