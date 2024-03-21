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

        private List<GameObject> _missiles = new List<GameObject>();
        private List<SmokeTrail> _missileTrails = new List<SmokeTrail>();
        private List<GameObject> _decoys = new List<GameObject>();
        private List<GameObjectPoly> _bullets = new List<GameObjectPoly>();
        private List<GameObject> _explosions = new List<GameObject>();
        private List<Plane> _planes = new List<Plane>();
        private List<GameObject> _updateObjects = new List<GameObject>();
        private List<GameObject> _expiredObjects = new List<GameObject>();

        private ConcurrentQueue<GameObject> _newDecoys = new ConcurrentQueue<GameObject>();
        private ConcurrentQueue<GameObject> _newMissiles = new ConcurrentQueue<GameObject>();
        private ConcurrentQueue<Plane> _newPlanes = new ConcurrentQueue<Plane>();

        private string _address;
        private ushort _port;
        private Net.Server _server;


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

                var objs = GetAllObjects();
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
                _explosions.ForEach(o => o.Update(World.DT, World.ViewPortSize, World.RenderScale));
                _decoys.ForEach(o => o.Update(World.DT, World.ViewPortSize, World.RenderScale));
                _missileTrails.ForEach(t => t.Update(World.DT, World.ViewPortSize, World.RenderScale));

                DoAIPlaneBurst(World.DT);
                _decoyTimer.Update(World.DT);

                _timer.Stop();
                _updateTime += _timer.Elapsed;
            }

            _timer.Restart();


            DoNetEvents();

            DoCollisions();
            PruneExpiredObj();

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
            //while (_newTargets.Count > 0)
            //{
            //    if (_newTargets.TryDequeue(out GameObject obj))
            //        _targets.Add(obj);
            //}

            while (_newMissiles.Count > 0)
            {
                if (_newMissiles.TryDequeue(out GameObject obj))
                {
                    _missiles.Add(obj);
                    _missileTrails.Add(new SmokeTrail(obj, o =>
                    {
                        var m = o as GuidedMissile;
                        return m.CenterOfThrust;
                    }));
                }
            }

            bool newPlanes = _newPlanes.Count > 0;

            while (_newPlanes.Count > 0)
            {
                if (_newPlanes.TryDequeue(out Plane plane))
                    _planes.Add(plane);
            }

            if (newPlanes)
                ServerSendOtherPlanes();

            while (_newDecoys.Count > 0)
            {
                if (_newDecoys.TryDequeue(out GameObject decoy))
                    _decoys.Add(decoy);
            }


        }

        private void DoCollisions()
        {
            var now = World.CurrentTime();

            // Targets/AI Planes vs missiles and bullets.
            for (int r = 0; r < _planes.Count; r++)
            {
                var plane = _planes[r] as Plane;
                var planeRTT = _server.GetPlayerRTT(plane.PlayerID);

                if (plane == null)
                    continue;

                // Missiles
                for (int m = 0; m < _missiles.Count; m++)
                {
                    var missile = _missiles[m] as Missile;

                    if (missile.Owner.ID.Equals(plane.ID))
                        continue;

                    if (missile.IsExpired)
                        continue;

                    var missileRTT = _server.GetPlayerRTT(missile.PlayerID);

                    if (plane.CollidesWithNet(missile, out D2DPoint pos, out GameObjectPacket? histState, now - (planeRTT + missileRTT)))
                    {
                        if (histState != null)
                        {
                            var ogState = new GameObjectPacket(plane);

                            plane.Position = histState.Position.ToD2DPoint();
                            plane.Velocity = histState.Velocity.ToD2DPoint();
                            plane.Rotation = histState.Rotation;
                            plane.SyncFixtures();

                            var impactResultM = plane.GetImpactResult(missile, pos);
                            SendNetImpact(missile, plane, impactResultM, histState);

                            plane.Position = ogState.Position.ToD2DPoint();
                            plane.Velocity = ogState.Velocity.ToD2DPoint();
                            plane.Rotation = ogState.Rotation;
                            plane.SyncFixtures();
                        }
                        else
                        {
                            var impactResultM = plane.GetImpactResult(missile, pos);
                            SendNetImpact(missile, plane, impactResultM, histState);
                        }

                        missile.IsExpired = true;
                        AddExplosion(pos);
                    }
                }

                // Bullets
                for (int b = 0; b < _bullets.Count; b++)
                {
                    var bullet = _bullets[b];

                    if (bullet.Owner.ID.Equals(plane.ID))
                        continue;

                    var bulletRTT = _server.GetPlayerRTT(bullet.PlayerID);

                    if (plane.CollidesWithNet(bullet, out D2DPoint pos, out GameObjectPacket? histState, now - (planeRTT + bulletRTT)))
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
                            SendNetImpact(bullet, plane, impactResult, histState);

                            plane.Position = ogState.Position.ToD2DPoint();
                            plane.Velocity = ogState.Velocity.ToD2DPoint();
                            plane.Rotation = ogState.Rotation;
                            plane.SyncFixtures();
                        }
                        else
                        {
                            var impactResult = plane.GetImpactResult(bullet, pos);
                            SendNetImpact(bullet, plane, impactResult, histState);
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
            for (int m = 0; m < _missiles.Count; m++)
            {
                var missile = _missiles[m] as Missile;

                for (int b = 0; b < _bullets.Count; b++)
                {
                    var bullet = _bullets[b];

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
            //for (int b = 0; b < _bullets.Count; b++)
            //{
            //    var bullet = _bullets[b];

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
            for (int a = 0; a < _planes.Count; a++)
            {
                var plane = _planes[a];

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
            var decoys = _decoys;

            bool groundScatter = false;

            for (int i = 0; i < _missiles.Count; i++)
            {
                var missile = _missiles[i] as GuidedMissile;
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

        private void DoNetEvents()
        {
            _frame++;

            double now = 0;
            double totalPacketTime = 0;
            int numPackets = 0;

            // Send plane & missile updates every other frame.
            if (_frame % 2 == 0)
            {
                SendPlaneUpdates();
                SendMissileUpdates();
                //_server.SendSyncPacket();

            }

            SendExpiredObjects();

            now = World.CurrentTime();

            while (_server.PacketReceiveQueue.Count > 0)
            {
                if (_server.PacketReceiveQueue.TryDequeue(out Net.NetPacket packet))
                {
                    totalPacketTime += now - packet.FrameTime;
                    numPackets++;

                    HandleNetPacket(packet);
                }
            }


            if (totalPacketTime > 0f && numPackets > 0)
            {
                var avgDelay = (totalPacketTime / (float)numPackets);
                _packetDelay = _packetDelayAvg.Add(avgDelay);
            }
        }


        private void HandleNetPacket(NetPacket packet)
        {
            switch (packet.Type)
            {
                case PacketTypes.PlaneUpdate:

                    var updPacket = packet as PlaneListPacket;
                    DoNetPlaneUpdates(updPacket);

                    break;
                case PacketTypes.MissileUpdate:

                    var missilePacket = packet as MissileListPacket;
                    DoNetMissileUpdates(missilePacket);

                    break;
                case PacketTypes.Impact:

                    var impactPacket = packet as ImpactPacket;
                    DoNetImpact(impactPacket);

                    break;
                case PacketTypes.NewPlayer:

                    var planePacket = packet as PlanePacket;

                    if (planePacket != null)
                    {
                        var newPlane = new Plane(planePacket.Position.ToD2DPoint(), planePacket.PlaneColor);
                        newPlane.ID = planePacket.ID;
                        planePacket.SyncObj(newPlane);
                        newPlane.IsNetObject = true;
                        newPlane.Radar = new Radar(newPlane, D2DColor.GreenYellow, _missiles, _planes);
                        _planes.Add(newPlane);
                    }

                    ServerSendOtherPlanes();

                    break;
                case PacketTypes.NewBullet:

                    var bulletPacket = packet as BulletPacket;
                    DoNewBullet(bulletPacket);

                    break;
                case PacketTypes.NewMissile:

                    var newMissilePacket = packet as MissilePacket;
                    DoNewMissile(newMissilePacket);

                    break;
                case PacketTypes.NewDecoy:

                    var newDecoyPacket = packet as DecoyPacket;
                    DoNewDecoy(newDecoyPacket);

                    break;
                case PacketTypes.SetID:

                    break;
                case PacketTypes.GetNextID:
                    // Nuttin...
                    break;
                case PacketTypes.ChatMessage:
                    // Nuttin...
                    break;
                case PacketTypes.GetOtherPlanes:

                    if (World.IsServer)
                    {
                        ServerSendOtherPlanes();
                    }
                    else
                    {
                        var listPacket = packet as Net.PlaneListPacket;

                        foreach (var plane in listPacket.Planes)
                        {
                            var existing = TryIDToPlane(plane.ID);

                            if (existing == null)
                            {
                                var newPlane = new Plane(plane.Position.ToD2DPoint(), plane.PlaneColor);
                                newPlane.ID = plane.ID;
                                newPlane.IsNetObject = true;
                                newPlane.Radar = new Radar(newPlane, D2DColor.GreenYellow, _missiles, _planes);
                                _planes.Add(newPlane);
                            }
                        }
                    }

                    break;

                case PacketTypes.ExpiredObjects:
                    var expiredPacket = packet as Net.BasicListPacket;

                    foreach (var p in expiredPacket.Packets)
                    {
                        var obj = GetObjectById(p.ID);

                        if (obj != null)
                            obj.IsExpired = true;
                    }

                    break;

                case PacketTypes.PlayerDisconnect:
                    var disconnectPack = packet as Net.BasicPacket;
                    DoPlayerDisconnected(disconnectPack.ID.PlayerID);

                    break;

                case PacketTypes.PlayerReset:

                    var resetPack = packet as Net.BasicPacket;

                    var resetPlane = GetObjectById(resetPack.ID) as Plane;

                    if (resetPlane != null)
                        resetPlane.FixPlane();

                    break;
            }
        }

        private void SendExpiredObjects()
        {
            var expiredObjPacket = new Net.BasicListPacket();
            _expiredObjects.ForEach(o => expiredObjPacket.Packets.Add(new BasicPacket(PacketTypes.ExpiredObjects, o.ID)));

            if (expiredObjPacket.Packets.Count == 0)
                return;

            _server.EnqueuePacket(expiredObjPacket);

            _expiredObjects.Clear();
        }

        private void SendPlaneUpdates()
        {
            var newPlanesPacket = new Net.PlaneListPacket();

            foreach (var plane in _planes)
            {
                var planePacket = new Net.PlanePacket(plane);
                newPlanesPacket.Planes.Add(planePacket);
            }

            _server.EnqueuePacket(newPlanesPacket);

        }

        private void SendMissileUpdates()
        {
            var newMissilesPacket = new Net.MissileListPacket();

            _missiles.ForEach(m => newMissilesPacket.Missiles.Add(new MissilePacket(m as GuidedMissile)));

            if (newMissilesPacket.Missiles.Count > 0)
                _server.EnqueuePacket(newMissilesPacket);
        }

        private void DoNetPlaneUpdates(PlaneListPacket listPacket)
        {
            foreach (var planeUpdPacket in listPacket.Planes)
            {

                var netPlane = GetNetPlane(planeUpdPacket.ID);

                if (netPlane != null)
                {
                    planeUpdPacket.SyncObj(netPlane);

                    netPlane.NetUpdate(World.DT, World.ViewPortSize, World.RenderScale, planeUpdPacket.Position.ToD2DPoint(), planeUpdPacket.Velocity.ToD2DPoint(), planeUpdPacket.Rotation, planeUpdPacket.FrameTime);
                }
            }
        }

        private void DoNetMissileUpdates(MissileListPacket listPacket)
        {
            foreach (var missileUpdate in listPacket.Missiles)
            {
                var netMissile = GetNetMissile(missileUpdate.ID);

                if (netMissile != null)
                {
                    var netMissileOwner = GetNetPlane(netMissile.Owner.ID, false);
                    var netMissileTarget = GetObjectById(missileUpdate.TargetID);

                    if (netMissileTarget != null)
                        netMissile.Target = netMissileTarget;

                    if (netMissileOwner != null && netMissileOwner.IsNetObject)
                    {
                        missileUpdate.SyncObj(netMissile);

                        netMissile.NetUpdate(World.DT, World.ViewPortSize, World.RenderScale, missileUpdate.Position.ToD2DPoint(), missileUpdate.Velocity.ToD2DPoint(), missileUpdate.Rotation, missileUpdate.FrameTime);
                    }
                }
            }
        }

        private void DoNewBullet(BulletPacket bulletPacket)
        {
            var bullet = new Bullet(bulletPacket.Position.ToD2DPoint(), bulletPacket.Velocity.ToD2DPoint(), bulletPacket.Rotation);
            bullet.ID = bulletPacket.ID;
            bulletPacket.SyncObj(bullet);
            var owner = GetNetPlane(bulletPacket.OwnerID);
            bullet.Owner = owner;

            var age = World.CurrentTime() - bulletPacket.FrameTime;

            // Try to spawn the bullet ahead to compensate for latency?
            //bullet.Position += bullet.Velocity * (float)(age / 1000f);
            //bullet.Position += bullet.Velocity * (float)(age);

            var contains = _bullets.Any(b => b.ID.Equals(bullet.ID));

            if (!contains)
                _bullets.Add(bullet);
        }

        private void DoNewMissile(MissilePacket missilePacket)
        {
            var missileOwner = GetNetPlane(missilePacket.OwnerID);

            if (missileOwner != null)
            {
                var missileTarget = GetNetPlane(missilePacket.TargetID, false);

                var missile = new GuidedMissile(missileOwner, missilePacket.Position.ToD2DPoint(), missilePacket.Velocity.ToD2DPoint(), missilePacket.Rotation);
                missile.IsNetObject = true;
                missile.ID = missilePacket.ID;
                missilePacket.SyncObj(missile);
                missile.Target = missileTarget;
                _newMissiles.Enqueue(missile);
            }
        }

        private void DoNewDecoy(DecoyPacket decoyPacket)
        {
            var decoyOwner = GetNetPlane(decoyPacket.OwnerID);

            if (decoyOwner != null)
            {
                var decoy = new Decoy(decoyOwner);
                decoy.ID = decoyPacket.ID;
                decoyPacket.SyncObj(decoy);

                bool containsDecoy = _decoys.Any(d => d.ID.Equals(decoy.ID));

                if (!containsDecoy)
                {
                    _decoys.Add(decoy);

                    _server.EnqueuePacket(decoyPacket);
                }
            }
        }

        private void SendNetImpact(GameObject impactor, GameObject target, PlaneImpactResult result)
        {
            var impactPacket = new Net.ImpactPacket(target, impactor.ID, result.ImpactPoint, result.DoesDamage, result.WasHeadshot, result.Type == ImpactType.Missile);

            _server.EnqueuePacket(impactPacket);
            DoNetImpact(impactPacket);
        }

        private void SendNetImpact(GameObject impactor, GameObject target, PlaneImpactResult result, GameObjectPacket histState)
        {
            var impactPacket = new Net.ImpactPacket(target, impactor.ID, result.ImpactPoint, result.DoesDamage, result.WasHeadshot, result.Type == ImpactType.Missile);

            if (histState != null)
            {
                impactPacket.Position = histState.Position;
                impactPacket.Velocity = histState.Velocity;
                impactPacket.Rotation = histState.Rotation;
            }

            _server.EnqueuePacket(impactPacket);
            DoNetImpact(impactPacket);
        }

        private void DoPlayerDisconnected(int playerID)
        {
            var objs = GetAllObjects();

            foreach (var obj in objs)
            {
                if (obj.PlayerID == playerID)
                    obj.IsExpired = true;
            }
        }

        private void DoNetImpact(ImpactPacket packet)
        {
            if (packet != null)
            {
                GameObject impactor = null;
                var impactorMissile = _missiles.Where(m => m.ID.Equals(packet.ImpactorID)).FirstOrDefault();
                var impactorBullet = _bullets.Where(b => b.ID.Equals(packet.ImpactorID)).FirstOrDefault();

                if (impactorMissile != null)
                    impactor = impactorMissile;

                if (impactorMissile == null && impactorBullet != null)
                    impactor = impactorBullet;

                if (impactor == null)
                    return;

                impactor.IsExpired = true;

                var target = _planes.Where(p => p.ID.Equals(packet.ID)).FirstOrDefault() as Plane;

                if (target != null)
                {
                    // Move the plane to the server position, do the impact, then move it back.
                    // This is to make sure the impacts/bullet holes show up in the correct place.
                    var curRot = target.Rotation;
                    var curVelo = target.Velocity;
                    var curPos = target.Position;

                    target.Rotation = packet.Rotation;
                    target.Velocity = packet.Velocity.ToD2DPoint();
                    target.Position = packet.Position.ToD2DPoint();
                    target.SyncFixtures();

                    var impactPoint = packet.ImpactPoint.ToD2DPoint();
                    target.DoNetImpact(impactor, impactPoint, packet.DoesDamage, packet.WasHeadshot, packet.WasMissile);

                    target.Rotation = curRot;
                    target.Velocity = curVelo;
                    target.Position = curPos;
                    target.SyncFixtures();

                    AddExplosion(impactPoint);
                }
            }
        }

        private void DoNetDecoy(Decoy decoy)
        {
            var decoyPacket = new Net.DecoyPacket(decoy);

            _server.EnqueuePacket(decoyPacket);
        }

        private void ServerSendOtherPlanes()
        {
            var otherPlanesPackets = new List<Net.PlanePacket>();
            foreach (var plane in _planes)
            {
                otherPlanesPackets.Add(new Net.PlanePacket(plane as Plane));
            }

            var listPacket = new Net.PlaneListPacket(otherPlanesPackets);
            listPacket.Type = PacketTypes.GetOtherPlanes;

            _server.EnqueuePacket(listPacket);
        }

        private void DoAIPlaneDecoys()
        {
            var dropping = _planes.Where(p => p.DroppingDecoy).ToArray();

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
            _newDecoys.Enqueue(decoy);
            DoNetDecoy(decoy);
        }

        private void DoAIPlaneBurst(float dt)
        {
            if (_planes.Any(p => p.FiringBurst))
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
            var firing = _planes.Where(p => p.FiringBurst).ToArray();

            if (firing.Length == 0)
                return;

            for (int i = 0; i < firing.Length; i++)
            {
                var plane = firing[i];
                plane.FireBullet(p => AddExplosion(p));
            }
        }


        private void PruneExpiredObj()
        {
            if (_clearObjs)
            {
                _clearObjs = false;
                Clear();
            }

            for (int o = 0; o < _missiles.Count; o++)
            {
                var missile = _missiles[o];

                if (missile.Altitude <= 0f)
                    missile.IsExpired = true;


                // TODO: Remove missiles fired by destoyed player
                if (missile.IsExpired)
                {
                    _expiredObjects.Add(missile);
                    _missiles.RemoveAt(o);
                }
            }

            for (int o = 0; o < _missileTrails.Count; o++)
            {
                var trail = _missileTrails[o];

                if (trail.IsExpired)
                    _missileTrails.RemoveAt(o);
            }

            for (int o = 0; o < _planes.Count; o++)
            {
                var plane = _planes[o];

                if (plane.IsExpired)
                {
                    _expiredObjects.Add(plane);
                    _planes.RemoveAt(o);
                }
            }


            //for (int o = 0; o < _targets.Count; o++)
            //{
            //    var targ = _targets[o];

            //    if (targ.IsExpired)
            //        _targets.RemoveAt(o);
            //}

            for (int o = 0; o < _bullets.Count; o++)
            {
                var bullet = _bullets[o];

                if (bullet.IsExpired)
                    _bullets.RemoveAt(o);
            }

            for (int e = 0; e < _explosions.Count; e++)
            {
                var explosion = _explosions[e];

                if (explosion.IsExpired)
                    _explosions.RemoveAt(e);
            }

            for (int d = 0; d < _decoys.Count; d++)
            {
                var decoy = _decoys[d];

                if (decoy.IsExpired || decoy.Owner.IsExpired)
                    _decoys.RemoveAt(d);
            }
        }

        private void Clear()
        {
            _missiles.Clear();
            _missileTrails.Clear();
            //_targets.Clear();
            _bullets.Clear();
            _explosions.Clear();
            _planes.Clear();
            _decoys.Clear();

            //_newTargets.Enqueue(_playerPlane);
        }

        private List<GameObject> GetAllObjects(bool localOnly = false)
        {
            _updateObjects.Clear();

            if (localOnly)
            {
                _updateObjects.AddRange(_missiles.Where(m => !m.IsNetObject));
                _updateObjects.AddRange(_bullets.Where(b => !b.IsNetObject));
                _updateObjects.AddRange(_planes.Where(p => !p.IsNetObject));

            }
            else
            {
                _updateObjects.AddRange(_missiles);
                _updateObjects.AddRange(_bullets);
                _updateObjects.AddRange(_planes);

            }

            return _updateObjects;
        }

        private Plane TryIDToPlane(GameID id)
        {
            var plane = _planes.Where(p => p.ID.Equals(id)).FirstOrDefault();

            return plane as Plane;
        }

        private GameObject GetObjectById(GameID id)
        {
            var allObjs = GetAllObjects();
            foreach (var obj in allObjs)
            {
                if (obj.ID.Equals(id))
                    return obj;
            }

            return null;
        }

        private Plane GetNetPlane(GameID id, bool netOnly = true)
        {

            foreach (var plane in _planes)
            {
                if (netOnly && !plane.IsNetObject)
                    continue;

                if (plane.ID.Equals(id))
                    return plane as Plane;
            }


            //foreach (var plane in _planes)
            //{
            //    if (plane.ID.Equals(id)) 
            //        return plane as Plane;
            //}


            //foreach (var plane in _netPlanes)
            //{
            //    if (plane.ID.Equals(id))
            //        return plane as Plane;
            //}

            return null;
        }

        private GuidedMissile GetNetMissile(GameID id)
        {
            foreach (var missile in _missiles)
            {
                if (missile.ID.Equals(id))
                    return missile as GuidedMissile;
            }

            return null;
        }

        private Plane GetAIPlane()
        {
            var range = new D2DPoint(-40000, 40000);
            var pos = new D2DPoint(Helpers.Rnd.NextFloat(range.X, range.Y), Helpers.Rnd.NextFloat(-4000f, -17000f));

            var aiPlane = new Plane(pos, isAI: true);
            aiPlane.PlayerID = World.GetNextPlayerId();
            aiPlane.Radar = new Radar(aiPlane, D2DColor.GreenYellow, _missiles, _planes);

            aiPlane.Radar.SkipFrames = World.PHYSICS_STEPS;

            aiPlane.FireMissileCallback = (m) =>
            {
                _newMissiles.Enqueue(m);
                _server.SendNewMissilePacket(m);
            };


            aiPlane.FireBulletCallback = b =>
            {
                _bullets.Add(b);
                _server.SendNewBulletPacket(b);
            };

            aiPlane.Velocity = new D2DPoint(400f, 0f);

            return aiPlane;
        }

        private void AddExplosion(D2DPoint pos)
        {
            var explosion = new Explosion(pos, 200f, 1.4f);

            _explosions.Add(explosion);
        }


        private void SpawnAIPlane()
        {
            var aiPlane = GetAIPlane();

            _newPlanes.Enqueue(aiPlane);
        }

        private string GetInfo()
        {
            string infoText = string.Empty;
            infoText += $"Paused: {_isPaused}\n\n";

            var numObj = _missiles.Count + _bullets.Count + _explosions.Count + _planes.Count;
            infoText += $"Num Objects: {numObj}\n";
            infoText += $"AI Planes: {_planes.Count(p => !p.IsDamaged && !p.HasCrashed)}\n";

            infoText += $"FPS: {Math.Round(_renderFPS, 0)}\n";
            infoText += $"Update ms: {_updateTime.TotalMilliseconds}\n";
            infoText += $"Collision ms: {_collisionTime.TotalMilliseconds}\n";
            infoText += $"Packet Delay: {_packetDelay}\n";
            infoText += $"Sent B/s: {(World.IsServer ? _server.BytesSentPerSecond : 0)}\n";
            infoText += $"Rec B/s: {(World.IsServer ? _server.BytesReceivedPerSecond : 0)}\n";

            infoText += $"DT: {Math.Round(World.DT, 4)}\n";
            infoText += $"Interp: {World.InterpOn.ToString()}\n";

            return infoText;
        }

        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            InfoLabel.Text = GetInfo();
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

        private void StartServerButton_Click(object sender, EventArgs e)
        {
            if (ushort.TryParse(PortTextBox.Text.Trim(), out ushort port))
            {
                World.IsNetGame = true;
                World.IsServer = true;
                var addy = AddressTextBox.Text.Trim();
                ENet.Library.Initialize();

                _server = new Net.Server(port, addy);

                StartGameThread();

                AddressTextBox.Enabled = false;
                PortTextBox.Enabled = false;
                StartServerButton.Enabled = false;
            }
        }
    }
}
