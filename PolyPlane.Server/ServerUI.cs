using PolyPlane.AI_Behavior;
using PolyPlane.GameObjects;
using PolyPlane.GameObjects.Manager;
using PolyPlane.GameObjects.Tools;
using PolyPlane.Helpers;
using PolyPlane.Net;
using PolyPlane.Net.Discovery;
using PolyPlane.Net.NetHost;
using PolyPlane.Rendering;
using System.ComponentModel;
using System.Diagnostics;


namespace PolyPlane.Server
{
    public partial class ServerUI : Form
    {
        private bool _pauseRequested = false;
        private bool _spawnAIPlane = false;
        private bool _spawnRandomAIPlane = false;
        private bool _clearAIPlanes = false;
        private bool _stopRender = false;
        private bool _killThread = false;
        private bool _toggleGunsOnly = false;

        private AIPersonality _aiPersonality = AIPersonality.Normal;
        private Thread _gameThread;
        private int _multiThreadNum = 4;

        private GameTimer _discoveryTimer = new GameTimer(2f, true);
        private GameTimer _syncTimer = new GameTimer(6f, true);

        private ManualResetEventSlim _pauseRenderEvent = new ManualResetEventSlim(true);

        private Stopwatch _timer = new Stopwatch();
        private TimeSpan _renderTime = new TimeSpan();
        private TimeSpan _updateTime = new TimeSpan();
        private TimeSpan _collisionTime = new TimeSpan();
        private TimeSpan _netTime = new TimeSpan();
        private SmoothFloat _updateTimeSmooth = new SmoothFloat(50);
        private SmoothFloat _collisionTimeSmooth = new SmoothFloat(50);
        private SmoothFloat _netTimeSmooth = new SmoothFloat(50);

        private long _lastRenderTime = 0;
        private float _renderFPS = 0;
        private FPSLimiter _fpsLimiter = new FPSLimiter();
        private uint _lastRec = 0;
        private uint _lastSent = 0;
        private Stopwatch _bwTimer = new Stopwatch();
        private SmoothDouble _sentSmooth = new SmoothDouble(50);
        private SmoothDouble _recSmooth = new SmoothDouble(50);

        private string _address;
        private string _serverName;
        private int _port;
        private GameObjectManager _objs = World.ObjectManager;
        private NetEventManager _netMan;
        private DiscoveryServer _discovery;
        private CollisionManager _collisions;
        private NetPlayHost _server;
        private RenderManager _render = null;

        private bool _queueNextViewId = false;
        private bool _queuePrevViewId = false;
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

            // Periodically broadcast discovery & time sync packets.
            _discoveryTimer.TriggerCallback = () => _discovery?.BroadcastServerInfo(new DiscoveryPacket(_address, _serverName, _port));
            _syncTimer.TriggerCallback = () => _netMan.SendSyncPacket();

            _multiThreadNum = Environment.ProcessorCount - 2;


            AITypeComboBox.Items.Clear();
            AITypeComboBox.DataSource = Enum.GetValues<AIPersonality>();

            _currentPlayers.RaiseListChangedEvents = true;

            PlayersListBox.DataBindings.Clear();
            PlayersListBox.DataSource = _currentPlayers;

            ChatMessageTextBox.MaxLength = ChatInterface.MAX_CHARS;

            TimeOfDaySlider.Maximum = (int)World.MAX_TIMEOFDAY;
            DeltaTimeNumeric.Value = (decimal)World.DT;
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

                _objs.PlayerKilledEvent += PlayerKilledEvent;
                _objs.NewPlayerEvent += NewPlayerEvent;

                _server.PeerDisconnectedEvent += Server_PeerDisconnectedEvent;

                if (EnableDiscoveryCheckBox.Checked)
                    _discoveryTimer.Start();

                _syncTimer.Start();

                StartGameThread();
                _updateTimer.Start();

                AddressTextBox.Enabled = false;
                PortTextBox.Enabled = false;
                StartServerButton.Enabled = false;
                ServerNameTextBox.Enabled = false;
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

                    _currentPlayers.Add(netPlayer);

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
                var netPlayer = _currentPlayers.Where(p => p.ID.PlayerID == e).FirstOrDefault();
                if (netPlayer != null)
                {
                    _currentPlayers.Remove(netPlayer);
                }
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
                var netPlayer = _currentPlayers.Where(p => p.ID.PlayerID == e.ID).FirstOrDefault();
                if (netPlayer != null)
                {
                    _currentPlayers.Remove(netPlayer);
                }
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
            _updateTime = TimeSpan.Zero;
            _collisionTime = TimeSpan.Zero;
            _renderTime = TimeSpan.Zero;
            _netTime = TimeSpan.Zero;

            _timer.Restart();
            ProcessObjQueue();
            _timer.Stop();
            _updateTime += _timer.Elapsed;

            // Update/advance objects.
            if (!World.IsPaused)
            {
                _timer.Restart();

                // Update all objects.
                _objs.Update(World.DT);

                _timer.Stop();
                _updateTime += _timer.Elapsed;

                _timer.Restart();
                _collisions.DoCollisions();
                _timer.Stop();
                _collisionTime += _timer.Elapsed;

                _timer.Restart();

                World.UpdateAirDensityAndWind(World.DT);

                _timer.Stop();
                _updateTime += _timer.Elapsed;
            }

            _timer.Restart();

            if (_render != null && !_stopRender)
            {
                FighterPlane viewPlane = World.GetViewPlane();
                
                if (viewPlane != null)
                    _render.RenderFrame(viewPlane);
            }

            _timer.Stop();
            _renderTime = _timer.Elapsed;


            _timer.Restart();
            _netMan.DoNetEvents();
            _timer.Stop();
            _netTime = _timer.Elapsed;
            _netTimeSmooth.Add((float)_netTime.TotalMilliseconds);

            _discoveryTimer.Update(World.DT);
            _syncTimer.Update(World.DT);

            var fpsNow = DateTime.UtcNow.Ticks;
            var fps = TimeSpan.TicksPerSecond / (float)(fpsNow - _lastRenderTime);
            _lastRenderTime = fpsNow;
            _renderFPS = fps;

            if (this._pauseRequested)
            {
                if (!World.IsPaused)
                    World.IsPaused = true;
                else
                    World.IsPaused = false;

                this._pauseRequested = false;
            }

            if (this._spawnAIPlane)
            {
                SpawnAIPlane(_aiPersonality);
                this._spawnAIPlane = false;
            }


            if (this._spawnRandomAIPlane)
            {
                SpawnAIPlane();
                this._spawnRandomAIPlane = false;
            }

            if (_clearAIPlanes)
            {
                _clearAIPlanes = false;
                RemoveAIPlanes();
            }

            if (_toggleGunsOnly)
            {
                World.GunsOnly = GunsOnlyCheckBox.Checked;
                _netMan.SendSyncPacket();
                _toggleGunsOnly = false;
            }

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
            plane.Position = Utilities.FindSafeSpawnPoint(_objs, plane);
            plane.Velocity = new D2DPoint(500f, 0f);
            plane.SyncFixtures();
            plane.RotationSpeed = 0f;
            plane.Rotation = 0f;
            plane.IsDisabled = false;
            plane.FixPlane();
        }

        private void ProcessObjQueue()
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

        }

        private void DropDecoy(Decoy decoy)
        {
            _objs.EnqueueDecoy(decoy);
            _netMan.SendNewDecoy(decoy);
        }

        private FighterPlane GetAIPlane(AIPersonality? personality = null)
        {
            var pos = Utilities.FindSafeSpawnPoint(_objs);

            FighterPlane aiPlane;

            if (personality.HasValue)
                aiPlane = new FighterPlane(pos, personality.Value, World.GetNextPlayerId());
            else
                aiPlane = new FighterPlane(pos, Utilities.RandomEnum<AIPersonality>(), World.GetNextPlayerId());

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

            aiPlane.Velocity = new D2DPoint(400f, 0f);

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
            _server.EnqueuePacket(kickPacket);
        }

        private string GetInfo()
        {
            UpdateBandwidthStats();

            string infoText = string.Empty;
            infoText += $"Paused: {World.IsPaused}\n\n";

            var numObj = _objs.TotalObjects;
            infoText += $"Num Objects: {numObj}\n";
            infoText += $"Planes: {_objs.Planes.Count}\n";
            infoText += $"Clients: {_server.Host.PeersCount}\n";

            infoText += $"FPS: {Math.Round(_renderFPS, 0)}\n";
            infoText += $"Update ms: {_updateTimeSmooth.Add((float)Math.Round(_updateTime.TotalMilliseconds, 2))}\n";
            infoText += $"Collision ms: {_collisionTimeSmooth.Add((float)Math.Round(_collisionTime.TotalMilliseconds, 2))}\n";
            infoText += $"Net ms: {_netTimeSmooth.Add((float)Math.Round(_collisionTime.TotalMilliseconds, 2))}\n";

            if (_viewPort != null)
                infoText += $"Render ms: {Math.Round(_renderTime.TotalMilliseconds, 2)}\n";

            infoText += $"Packet Delay: {Math.Round(_netMan.PacketDelay, 2)}\n";

            infoText += $"Bytes Rec: {_netMan.Host.Host.BytesReceived}\n";
            infoText += $"Bytes Sent: {_netMan.Host.Host.BytesSent}\n";
            infoText += $"MB Rec/s: {Math.Round(_recSmooth.Current, 2)}\n";
            infoText += $"MB Sent/s: {Math.Round(_sentSmooth.Current, 2)}\n";
            infoText += $"DT: {Math.Round(World.DT, 4)}\n";
            infoText += $"Interp: {World.InterpOn.ToString()}\n";
            infoText += $"TimeOfDay: {World.TimeOfDay.ToString()}\n";

            return infoText;
        }

        private void UpdateBandwidthStats()
        {
            const float BYTES_PER_MB = 1000000f;

            if (!_bwTimer.IsRunning)
                _bwTimer.Start();

            _bwTimer.Stop();
            var elap = _bwTimer.Elapsed;
            var recDiff = _netMan.Host.Host.BytesReceived - _lastRec;
            var sentDiff = _netMan.Host.Host.BytesSent - _lastSent;

            _lastRec = _netMan.Host.Host.BytesReceived;
            _lastSent = _netMan.Host.Host.BytesSent;

            _sentSmooth.Add((sentDiff / elap.TotalSeconds) / BYTES_PER_MB);
            _recSmooth.Add((recDiff / elap.TotalSeconds) / BYTES_PER_MB);
            _bwTimer.Restart();
        }

        private void InitViewPort()
        {
            if (_viewPort != null)
                return;

            _viewPort = new Form();
            _viewPort.Size = new Size(1346, 814);
            _viewPort.KeyPress += ViewPort_KeyPress;
            _viewPort.Disposed += ViewPort_Disposed;
            _viewPort.Show();

            _render = new RenderManager(_viewPort, _netMan);
            _stopRender = false;
        }

        private void ViewPort_Disposed(object? sender, EventArgs e)
        {
            // TODO: Figure out why we get no output after the first time the viewport is closed and reopened.
            _stopRender = true;
            _render?.Dispose();
            _render = null;
            _viewPort.KeyPress -= ViewPort_KeyPress;
            _viewPort.Disposed -= ViewPort_Disposed;
            _viewPort = null;
        }

        private void ViewPort_KeyPress(object? sender, KeyPressEventArgs e)
        {
            switch (e.KeyChar)
            {

                case '[':
                    _queuePrevViewId = true;

                    break;
                case ']':

                    _queueNextViewId = true;
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
            _pauseRequested = true;
        }

        private void SpawnAIPlaneButton_Click(object sender, EventArgs e)
        {
            var selected = AITypeComboBox.SelectedItem;

            if (selected != null)
            {
                var personality = (AIPersonality)selected;
                _aiPersonality = personality;
                _spawnAIPlane = true;
            }
        }

        private void InterpCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            World.InterpOn = InterpCheckBox.Checked;
        }

        private void ShowViewPortButton_Click(object sender, EventArgs e)
        {
            InitViewPort();
        }

        private void SpawnRandomAIButton_Click(object sender, EventArgs e)
        {
            _spawnRandomAIPlane = true;
        }

        private void RemoveAIPlanesButton_Click(object sender, EventArgs e)
        {
            _clearAIPlanes = true;
        }

        private void kickToolStripMenuItem_Click(object sender, EventArgs e)
        {
            KickSelectedPlayer();
        }

        private void SentChatButton_Click(object sender, EventArgs e)
        {
            _netMan.ChatInterface.SendMessage(ChatMessageTextBox.Text);
            ChatMessageTextBox.Text = string.Empty;
        }

        private void ChatMessageTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                _netMan.ChatInterface.SendMessage(ChatMessageTextBox.Text);
                ChatMessageTextBox.Text = string.Empty;
            }
        }

        private void TimeOfDaySlider_Scroll(object sender, EventArgs e)
        {
            World.TimeOfDay = TimeOfDaySlider.Value;
        }

        private void TimeOfDaySlider_ValueChanged(object sender, EventArgs e)
        {

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
            _toggleGunsOnly = true;
        }

        private void DeltaTimeNumeric_ValueChanged(object sender, EventArgs e)
        {
            if (_server == null)
                return;

            World.DT = (float)DeltaTimeNumeric.Value;
            _netMan.SendSyncPacket();
        }

        private void DefaultDTButton_Click(object sender, EventArgs e)
        {
            World.DT = World.DEFAULT_DT;
            DeltaTimeNumeric.Value = (decimal)World.DT;
        }
    }
}
