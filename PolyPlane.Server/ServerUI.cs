using PolyPlane.GameObjects;
using PolyPlane.GameObjects.Manager;
using PolyPlane.Net;
using PolyPlane.Rendering;
using System.Diagnostics;
using unvell.D2DLib;


namespace PolyPlane.Server
{
    public partial class ServerUI : Form
    {
        public string InfoText;
        public bool PauseRequested = false;
        public bool SpawnIAPlane = false;
        private Thread _gameThread;
        private int _multiThreadNum = 4;

        private GameTimer _burstTimer = new GameTimer(0.25f, true);
        private GameTimer _decoyTimer = new GameTimer(0.25f, true);
        private GameTimer _playerBurstTimer = new GameTimer(0.1f, true);

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
        private WaitableTimer _waitTimer = new WaitableTimer();
        private Stopwatch _fpsTimer = new Stopwatch();
        private long _frame = 0;
        private double _packetDelay = 0f;
        private SmoothDouble _packetDelayAvg = new SmoothDouble(100);

        private bool _clearObjs = false;

        private GameObjectManager _objs = new GameObjectManager();
        private NetEventManager _netMan;
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

            this.Disposed += ServerUI_Disposed; ;

            _updateTimer.Tick += UpdateTimer_Tick;
            _updateTimer.Interval = 16;


            _burstTimer.TriggerCallback = () => DoAIPlaneBursts();
            _decoyTimer.TriggerCallback = () => DoAIPlaneDecoys();

            _multiThreadNum = Environment.ProcessorCount - 2;
        }

        private void StartServerButton_Click(object sender, EventArgs e)
        {
            if (ushort.TryParse(PortTextBox.Text.Trim(), out ushort port))
            {
                World.IsNetGame = true;
                World.IsServer = true;
                var addy = AddressTextBox.Text.Trim();
                ENet.Library.Initialize();

                _server = new Net.ServerNetHost(port, addy);
                _netMan = new NetEventManager(_objs, _server);
                _collisions = new CollisionManager(_objs, _netMan);

                StartGameThread();
                _updateTimer.Start();

                AddressTextBox.Enabled = false;
                PortTextBox.Enabled = false;
                StartServerButton.Enabled = false;
            }
        }

        private void ServerUI_Disposed(object? sender, EventArgs e)
        {
            _server?.Stop();
            _server?.Dispose();


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

            ProcessObjQueue();

            Plane viewPlane = GetViewPlane();
           

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

                DoDecoySuccess();

                _playerBurstTimer.Update(World.DT);

                DoAIPlaneBurst(World.DT);
                _decoyTimer.Update(World.DT);

                _timer.Stop();
                _updateTime += _timer.Elapsed;
            }

            _timer.Restart();

            if (_render != null)
                _render.RenderFrame(viewPlane);

            _netMan.DoNetEvents();
            _objs.PruneExpired();

            var fpsNow = DateTime.UtcNow.Ticks;
            var fps = TimeSpan.TicksPerSecond / (float)(fpsNow - _lastRenderTime);
            _lastRenderTime = fpsNow;
            _renderFPS = fps;


            //this.InfoText = GetInfo();

            if (this.PauseRequested)
            {
                if (!_isPaused)
                    _isPaused = true;
                else
                    _isPaused = false;

                this.PauseRequested = false;
            }

            if (this.SpawnIAPlane)
            {
                SpawnAIPlane();
                this.SpawnIAPlane = false;
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

        private Plane GetViewPlane()
        {
            //var idPlane = IDToPlane(_aiPlaneViewID);
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

        private void DropDecoy(Plane plane)
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
                plane.FireBullet(p => AddExplosion(p));
            }
        }

        private void Clear()
        {

            _objs.Clear();

            //_objs.Missiles.Clear();
            //_missileTrails.Clear();
            ////_targets.Clear();
            //_objs.Bullets.Clear();
            //_explosions.Clear();
            //_objs.Planes.Clear();
            //_decoys.Clear();

            //_newTargets.Enqueue(_playerPlane);
        }





        private Plane GetAIPlane()
        {
            var range = new D2DPoint(-40000, 40000);
            var pos = new D2DPoint(Helpers.Rnd.NextFloat(range.X, range.Y), Helpers.Rnd.NextFloat(-4000f, -17000f));

            var aiPlane = new Plane(pos, isAI: true);
            aiPlane.PlayerID = World.GetNextPlayerId();
            aiPlane.Radar = new Radar(aiPlane, D2DColor.GreenYellow, _objs.Missiles, _objs.Planes);

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

        private void AddExplosion(D2DPoint pos)
        {
            var explosion = new Explosion(pos, 200f, 1.4f);

            _objs.AddExplosion(explosion);
        }

        private void SpawnAIPlane()
        {
            var aiPlane = GetAIPlane();

            _objs.AddPlane(aiPlane);
            _netMan.ServerSendOtherPlanes();
        }

        private string GetInfo()
        {
            string infoText = string.Empty;
            infoText += $"Paused: {_isPaused}\n\n";

            var numObj = _objs.TotalObjects;
            infoText += $"Num Objects: {numObj}\n";
            infoText += $"AI Planes: {_objs.Planes.Count(p => !p.IsDamaged && !p.HasCrashed)}\n";

            infoText += $"FPS: {Math.Round(_renderFPS, 0)}\n";
            infoText += $"Update ms: {_updateTime.TotalMilliseconds}\n";
            infoText += $"Collision ms: {_collisionTime.TotalMilliseconds}\n";
            infoText += $"Packet Delay: {_netMan.PacketDelay}\n";
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
            SpawnIAPlane = true;
        }

        private void InterpCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            World.InterpOn = InterpCheckBox.Checked;
        }

        private void ShowViewPortButton_Click(object sender, EventArgs e)
        {
            InitViewPort();


        }
    }
}
