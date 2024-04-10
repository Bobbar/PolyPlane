﻿using PolyPlane.AI_Behavior;
using PolyPlane.GameObjects;
using PolyPlane.GameObjects.Manager;
using PolyPlane.Net;
using PolyPlane.Net.Discovery;
using PolyPlane.Rendering;
using System.Diagnostics;
using unvell.D2DLib;


namespace PolyPlane.Server
{
    public partial class ServerUI : Form
    {
        public string InfoText;
        public bool PauseRequested = false;
        private bool _spawnAIPlane = false;
        private bool _spawnRandomAIPlane = false;
        private bool _clearAIPlanes = false;

        private AIPersonality _aiPersonality = AIPersonality.Normal;
        private Thread _gameThread;
        private int _multiThreadNum = 4;

        private GameTimer _burstTimer = new GameTimer(0.25f, true);
        private GameTimer _decoyTimer = new GameTimer(0.25f, true);
        private GameTimer _playerBurstTimer = new GameTimer(0.1f, true);
        private GameTimer _discoveryTimer = new GameTimer(2f, true);
        private GameTimer _syncTimer = new GameTimer(10f, true);

        private ManualResetEventSlim _pauseRenderEvent = new ManualResetEventSlim(true);
        private ManualResetEventSlim _stopRenderEvent = new ManualResetEventSlim(true);

        private bool _isPaused = false;

        private Stopwatch _timer = new Stopwatch();
        private TimeSpan _renderTime = new TimeSpan();
        private TimeSpan _updateTime = new TimeSpan();
        private TimeSpan _collisionTime = new TimeSpan();
        private TimeSpan _netTime = new TimeSpan();
        private long _lastRenderTime = 0;
        private float _renderFPS = 0;
        private FPSLimiter _fpsLimiter = new FPSLimiter();
        private long _frame = 0;
        private double _packetDelay = 0f;
        private SmoothDouble _packetDelayAvg = new SmoothDouble(100);
        private uint _lastRec = 0;
        private uint _lastSent = 0;
        private Stopwatch _bwTimer = new Stopwatch();
        private SmoothDouble _sentSmooth = new SmoothDouble(50);
        private SmoothDouble _recSmooth = new SmoothDouble(50);
        private SmoothDouble _netSmooth = new SmoothDouble(50);

        private bool _clearObjs = false;

        private string _address;
        private string _serverName;
        private int _port;
        private GameObjectManager _objs = new GameObjectManager();
        private NetEventManager _netMan;
        private DiscoveryServer _discovery;
        private CollisionManager _collisions;
        private NetPlayHost _server;
        private RenderManager _render = null;

        private bool _queueNextViewId = false;
        private bool _queuePrevViewId = false;
        private int _aiPlaneViewID = -1;

        private Form _viewPort = null;

        private System.Windows.Forms.Timer _updateTimer = new System.Windows.Forms.Timer();

        public ServerUI()
        {
            InitializeComponent();

            var localIP = Helpers.GetLocalIP();

            if (localIP != null)
            {
                _address = localIP;
                AddressTextBox.Text = localIP;
            }

            this.Disposed += ServerUI_Disposed; ;

            _updateTimer.Tick += UpdateTimer_Tick;
            _updateTimer.Interval = 16;


            _burstTimer.TriggerCallback = () => DoAIPlaneBursts();
            _decoyTimer.TriggerCallback = () => DoAIPlaneDecoys();

            // Periodically broadcast discovery & time sync packets.
            _discoveryTimer.TriggerCallback = () => _discovery?.BroadcastServerInfo(new DiscoveryPacket(_address, _serverName, _port));
            _syncTimer.TriggerCallback = () => _server.SendSyncPacket();

            _multiThreadNum = Environment.ProcessorCount - 2;


            AITypeComboBox.Items.Clear();
            AITypeComboBox.DataSource = Enum.GetValues<AIPersonality>();
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

                ENet.Library.Initialize();

                _server = new ServerNetHost(port, addy);
                _netMan = new NetEventManager(_objs, _server);
                _discovery = new DiscoveryServer();
                _collisions = new CollisionManager(_objs, _netMan);

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

        private void ServerUI_Disposed(object? sender, EventArgs e)
        {
            _server?.Stop();
            _server?.Dispose();
            _fpsLimiter?.Dispose();

            _discovery?.StopListen();
            _discovery?.Dispose();
            ENet.Library.Deinitialize();
        }

        private void StartGameThread()
        {
            _server.Start();

            _gameThread = new Thread(GameLoop);
            _gameThread.Priority = ThreadPriority.AboveNormal;
            _gameThread.Start();
            _decoyTimer.Start();
        }

        private void GameLoop()
        {
            while (!this.Disposing)
            {
                AdvanceServer();

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
            _renderTime = TimeSpan.Zero;
            _netTime = TimeSpan.Zero;

            ProcessObjQueue();

            FighterPlane viewPlane = GetViewPlane();

            // Update/advance objects.
            if (!_isPaused)
            {
                var partialDT = World.SUB_DT;

                var objs = _objs.GetAllObjects();
                var numObj = objs.Count;

                for (int i = 0; i < World.PHYSICS_STEPS; i++)
                {
                    _timer.Restart();

                    _collisions.DoCollisions();

                    _timer.Stop();

                    _collisionTime += _timer.Elapsed;

                    _timer.Restart();

                    objs.ForEachParallel(o => o.Update(partialDT, World.ViewPortSize, World.RenderScale), _multiThreadNum);

                    _timer.Stop();
                    _updateTime += _timer.Elapsed;
                }

                _timer.Restart();

                World.UpdateAirDensityAndWind(World.DT);

                _collisions.DoDecoySuccess();

                _playerBurstTimer.Update(World.DT);

                DoAIPlaneBurst(World.DT);
                _decoyTimer.Update(World.DT);

                _timer.Stop();
                _updateTime += _timer.Elapsed;
            }

            _timer.Restart();

            if (_render != null)
                _render.RenderFrame(viewPlane);

            _timer.Stop();
            _renderTime = _timer.Elapsed;

            _objs.PruneExpired();


            _timer.Restart();
            _netMan.DoNetEvents();
            _timer.Stop();
            _netTime = _timer.Elapsed;

            _discoveryTimer.Update(World.DT);
            _syncTimer.Update(World.DT);

            var fpsNow = DateTime.UtcNow.Ticks;
            var fps = TimeSpan.TicksPerSecond / (float)(fpsNow - _lastRenderTime);
            _lastRenderTime = fpsNow;
            _renderFPS = fps;

            if (this.PauseRequested)
            {
                if (!_isPaused)
                    _isPaused = true;
                else
                    _isPaused = false;

                this.PauseRequested = false;
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

            _fpsLimiter.Wait(World.NET_SERVER_FPS);
        }

        private FighterPlane GetViewPlane()
        {
            var idPlane = _objs.GetPlaneByPlayerID(_aiPlaneViewID);

            if (idPlane != null)
            {
                World.ViewID = idPlane.ID;
                return idPlane;
            }
            else
                return null;
        }

        private void ProcessObjQueue()
        {
            _objs.SyncAll();

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
            if (plane.IsDamaged)
                return;

            var decoy = new Decoy(plane);
            _objs.EnqueueDecoy(decoy);
            _netMan.SendNewDecoy(decoy);
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
                plane.FireBullet(p => _objs.AddBulletExplosion(p));
            }
        }

        private FighterPlane GetAIPlane()
        {
            var range = new D2DPoint(-40000, 40000);
            var pos = new D2DPoint(Helpers.Rnd.NextFloat(range.X, range.Y), Helpers.Rnd.NextFloat(-4000f, -17000f));

            var aiPlane = new FighterPlane(pos, Helpers.RandomEnum<AIPersonality>());
            aiPlane.PlayerID = World.GetNextPlayerId();
            aiPlane.Radar = new Radar(aiPlane, D2DColor.GreenYellow, _objs.Missiles, _objs.Planes);
            aiPlane.PlayerName = Helpers.GetRandomName();
            aiPlane.Radar.SkipFrames = World.PHYSICS_STEPS;

            aiPlane.FireMissileCallback = (m) =>
            {
                _objs.EnqueueMissile(m);
                _server.SendNewMissilePacket(m);
            };


            aiPlane.FireBulletCallback = b =>
            {
                _objs.AddBullet(b);
                _server.SendNewBulletPacket(b);
            };

            aiPlane.Velocity = new D2DPoint(400f, 0f);

            return aiPlane;
        }

        private FighterPlane GetAIPlane(AIPersonality personality)
        {
            var range = new D2DPoint(-40000, 40000);
            var pos = new D2DPoint(Helpers.Rnd.NextFloat(range.X, range.Y), Helpers.Rnd.NextFloat(-4000f, -17000f));

            var aiPlane = new FighterPlane(pos, personality);
            aiPlane.PlayerID = World.GetNextPlayerId();
            aiPlane.Radar = new Radar(aiPlane, D2DColor.GreenYellow, _objs.Missiles, _objs.Planes);
            aiPlane.PlayerName = Helpers.GetRandomName();
            aiPlane.Radar.SkipFrames = World.PHYSICS_STEPS;

            aiPlane.FireMissileCallback = (m) =>
            {
                _objs.EnqueueMissile(m);
                _server.SendNewMissilePacket(m);
            };


            aiPlane.FireBulletCallback = b =>
            {
                _objs.AddBullet(b);
                _server.SendNewBulletPacket(b);
            };

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

        private string GetInfo()
        {
            if (!_bwTimer.IsRunning)
                _bwTimer.Start();

            string infoText = string.Empty;
            infoText += $"Paused: {_isPaused}\n\n";

            var numObj = _objs.TotalObjects;
            infoText += $"Num Objects: {numObj}\n";
            infoText += $"AI Planes: {_objs.Planes.Count(p => !p.IsDamaged && !p.HasCrashed)}\n";
            infoText += $"FPS: {Math.Round(_renderFPS, 0)}\n";
            infoText += $"Update ms: {Math.Round(_updateTime.TotalMilliseconds, 2)}\n";
            infoText += $"Collision ms: {Math.Round(_collisionTime.TotalMilliseconds, 2)}\n";
            //infoText += $"Net ms: {Math.Round(_netTime.TotalMilliseconds, 2)}\n";
            infoText += $"Net ms: {Math.Round(_netSmooth.Add(_netMan.Host.NetTime.TotalMilliseconds), 2)}\n";

            if (_viewPort != null)
                infoText += $"Render ms: {Math.Round(_renderTime.TotalMilliseconds, 2)}\n";

            infoText += $"Packet Delay: {Math.Round(_netMan.PacketDelay, 2)}\n";

            infoText += $"Bytes Rec: {_netMan.Host.Host.BytesReceived}\n";
            infoText += $"Bytes Sent: {_netMan.Host.Host.BytesSent}\n";

            _bwTimer.Stop();
            var elap = _bwTimer.Elapsed;
            var recDiff = _netMan.Host.Host.BytesReceived - _lastRec;
            var sentDiff = _netMan.Host.Host.BytesSent - _lastSent;

            _lastRec = _netMan.Host.Host.BytesReceived;
            _lastSent = _netMan.Host.Host.BytesSent;

            var sentPerSec = _sentSmooth.Add((sentDiff / elap.TotalSeconds) / 100000f);
            var recPerSec = _recSmooth.Add((recDiff / elap.TotalSeconds) / 100000f);
            _bwTimer.Restart();

            infoText += $"MB Rec/s: {Math.Round(recPerSec, 2)}\n";
            infoText += $"MB Sent/s: {Math.Round(sentPerSec, 2)}\n";
            //infoText += $"Sent B/s: {(World.IsServer ? _server.BytesSentPerSecond : 0)}\n";
            //infoText += $"Rec B/s: {(World.IsServer ? _server.BytesReceivedPerSecond : 0)}\n";

            infoText += $"DT: {Math.Round(World.DT, 4)}\n";
            infoText += $"Interp: {World.InterpOn.ToString()}\n";

            return infoText;
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

        private void InitViewPort()
        {
            if (_viewPort != null)
                return;

            _viewPort = new Form();
            _viewPort.Size = new Size(1346, 814);
            _viewPort.KeyPress += ViewPort_KeyPress;
            _viewPort.Show();

            _render = new RenderManager(_viewPort, _objs, _netMan);
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


            //InfoLabel.Text = InfoText;
        }

        private void PauseButton_Click(object sender, EventArgs e)
        {
            PauseRequested = true;
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
    }
}
