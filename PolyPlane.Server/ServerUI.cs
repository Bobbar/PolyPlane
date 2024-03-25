using PolyPlane.GameObjects;
using PolyPlane.Net;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
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
        private NetObjectManager _netMan;
        private string _address;
        private ushort _port;

        private Net.NetPlayHost _server;

        private System.Windows.Forms.Timer _updateTimer = new System.Windows.Forms.Timer();

        public ServerUI()
        {
            InitializeComponent();

            this.Disposed += ServerUI_Disposed; ;

            _updateTimer.Tick += UpdateTimer_Tick;
            _updateTimer.Interval = 16;
            _updateTimer.Start();


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
                _netMan = new NetObjectManager(_objs, _server);

                StartGameThread();

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

            // Update/advance objects.
            if (!_isPaused)
            {
                var partialDT = World.SUB_DT;

                var objs = _objs.GetAllObjects();
                var numObj = objs.Count;

                for (int i = 0; i < World.PHYSICS_STEPS; i++)
                {
                    _timer.Restart();

                    DoCollisions();

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

            _netMan.DoNetEvents();
            _objs.PruneExpired();

            var fpsNow = DateTime.UtcNow.Ticks;
            var fps = TimeSpan.TicksPerSecond / (float)(fpsNow - _lastRenderTime);
            _lastRenderTime = fpsNow;
            _renderFPS = fps;


            this.InfoText = GetInfo();

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


        private void ProcessObjQueue()
        {
            _objs.SyncAll();
        }

        private void DoCollisions()
        {
            const float LAG_COMP_FACT = 1f;
            var now = World.CurrentTime();

            // Targets/AI Planes vs missiles and bullets.
            for (int r = 0; r < _objs.Planes.Count; r++)
            {
                var plane = _objs.Planes[r] as Plane;
                var planeRTT = _server.GetPlayerRTT(plane.PlayerID);

                if (plane == null)
                    continue;

                // Missiles
                for (int m = 0; m < _objs.Missiles.Count; m++)
                {
                    var missile = _objs.Missiles[m] as Missile;

                    if (missile.Owner.ID.Equals(plane.ID))
                        continue;

                    if (missile.IsExpired)
                        continue;

                    var missileRTT = _server.GetPlayerRTT(missile.PlayerID);

                    if (plane.CollidesWithNet(missile, out D2DPoint pos, out GameObjectPacket? histState, now - ((planeRTT + missile.LagAmount + missileRTT) * LAG_COMP_FACT)))
                    {
                        if (histState != null)
                        {
                            var ogState = new GameObjectPacket(plane);

                            plane.Position = histState.Position.ToD2DPoint();
                            plane.Velocity = histState.Velocity.ToD2DPoint();
                            plane.Rotation = histState.Rotation;
                            plane.SyncFixtures();

                            var impactResultM = plane.GetImpactResult(missile, pos);
                            _netMan.SendNetImpact(missile, plane, impactResultM, histState);
                         
                            plane.Position = ogState.Position.ToD2DPoint();
                            plane.Velocity = ogState.Velocity.ToD2DPoint();
                            plane.Rotation = ogState.Rotation;
                            plane.SyncFixtures();
                        }
                        else
                        {
                            var impactResultM = plane.GetImpactResult(missile, pos);
                            _netMan.SendNetImpact(missile, plane, impactResultM, histState);
                        }

                        missile.IsExpired = true;
                        AddExplosion(pos);
                    }
                }

                // Bullets
                for (int b = 0; b < _objs.Bullets.Count; b++)
                {
                    var bullet = _objs.Bullets[b] as Bullet;

                    if (bullet.IsExpired)
                        continue;

                    if (bullet.Owner.ID.Equals(plane.ID))
                        continue;

                    var bulletRTT = _server.GetPlayerRTT(bullet.PlayerID);

                    if (plane.CollidesWithNet(bullet, out D2DPoint pos, out GameObjectPacket? histState, now - ((planeRTT + bullet.LagAmount + bulletRTT) * LAG_COMP_FACT)))
                    {
                        if (!plane.IsExpired)
                            AddExplosion(pos);

                        if (histState != null)
                        {
                            var ogState = new GameObjectPacket(plane);

                            plane.Position = histState.Position.ToD2DPoint();
                            plane.Velocity = histState.Velocity.ToD2DPoint();
                            plane.Rotation = histState.Rotation;
                            plane.SyncFixtures();

                            var impactResult = plane.GetImpactResult(bullet, pos);
                            _netMan.SendNetImpact(bullet, plane, impactResult, histState);

                            plane.Position = ogState.Position.ToD2DPoint();
                            plane.Velocity = ogState.Velocity.ToD2DPoint();
                            plane.Rotation = ogState.Rotation;
                            plane.SyncFixtures();
                        }
                        else
                        {
                            var impactResult = plane.GetImpactResult(bullet, pos);
                            _netMan.SendNetImpact(bullet, plane, impactResult, histState);

                        }


                        bullet.IsExpired = true;
                    }


                    //if (targ.CollidesWith(bullet, out D2DPoint pos) && !bullet.Owner.ID.Equals(targ.ID))
                    //{
                    //    if (!targ.IsExpired)
                    //        AddExplosion(pos);

                    //    if (targ is Plane plane2)
                    //    {

                    //        var impactResult = plane2.GetImpactResult(bullet, pos);
                    //        SendNetImpact(bullet, plane2, impactResult);
                    //    }

                    //    bullet.IsExpired = true;
                    //}
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
            for (int m = 0; m < _objs.Missiles.Count; m++)
            {
                var missile = _objs.Missiles[m] as Missile;

                if (missile.IsExpired)
                    continue;

                for (int b = 0; b < _objs.Bullets.Count; b++)
                {
                    var bullet = _objs.Bullets[b] as Bullet;

                    if (bullet.IsExpired)
                        continue;

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

            }


            //// Handle player plane vs bullets.
            //for (int b = 0; b < _objs.Bullets.Count; b++)
            //{
            //    var bullet = _objs.Bullets[b];

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
            for (int a = 0; a < _objs.Planes.Count; a++)
            {
                var plane = _objs.Planes[a];

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
            infoText += $"Packet Delay: {_packetDelay}\n";
            //infoText += $"Sent B/s: {(World.IsServer ? _server.BytesSentPerSecond : 0)}\n";
            //infoText += $"Rec B/s: {(World.IsServer ? _server.BytesReceivedPerSecond : 0)}\n";

            infoText += $"DT: {Math.Round(World.DT, 4)}\n";
            infoText += $"Interp: {World.InterpOn.ToString()}\n";

            //if (_objs.Planes.Count > 0)
            //{
            //    var plane = _objs.Planes[0];
            //    infoText += $"Pos: {plane.Position}\n";

            //}

            return infoText;
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

      
    }
}
