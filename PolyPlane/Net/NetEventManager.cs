﻿using PolyPlane.GameObjects;
using PolyPlane.GameObjects.Manager;
using PolyPlane.Helpers;
using PolyPlane.Net.NetHost;
using System.Diagnostics;

namespace PolyPlane.Net
{
    public class NetEventManager : IImpactEvent
    {
        public NetPlayHost Host;
        public ChatInterface ChatInterface;
        public bool IsServer = false;
        public FighterPlane PlayerPlane = null;
        public double PacketDelay = 0;

        private GameObjectManager _objs = World.ObjectManager;
        private SmoothDouble _packetDelayAvg = new SmoothDouble(100);
        private Dictionary<int, List<ImpactPacket>> _impacts = new Dictionary<int, List<ImpactPacket>>();
        private List<NetPacket> _deferredPackets = new List<NetPacket>();

        private bool _netIDIsSet = false;
        private bool _receivedFirstSync = false;
        private double _lastNetTime = 0;

        public event EventHandler<int> PlayerIDReceived;
        public event EventHandler<ImpactEvent> ImpactEvent;
        public event EventHandler<ChatPacket> NewChatMessage;
        public event EventHandler<int> PlayerKicked;
        public event EventHandler<int> PlayerDisconnected;
        public event EventHandler<int> PlayerJoined;
        public event EventHandler<FighterPlane> PlayerRespawned;
        public event EventHandler<string> PlayerEventMessage;

        private const long MAX_DEFER_AGE = 400; // Max age allowed for deferred packets.

        public NetEventManager(NetPlayHost host, FighterPlane playerPlane)
        {
            Host = host;
            PlayerPlane = playerPlane;
            IsServer = false;
            ChatInterface = new ChatInterface(this, playerPlane.PlayerName);
            AttachEvents();
        }

        public NetEventManager(NetPlayHost host)
        {
            Host = host;
            PlayerPlane = null;
            IsServer = true;
            ChatInterface = new ChatInterface(this, "Server");
            AttachEvents();
        }

        private void AttachEvents()
        {
            Host.PeerDisconnectedEvent += Host_PeerDisconnectedEvent;
        }

        private void Host_PeerDisconnectedEvent(object? sender, ENet.Peer e)
        {
            DoPlayerDisconnected((int)e.ID);
          
            if (IsServer)
                ClearImpacts((int)e.ID);
        }

        public void DoNetEvents(float dt)
        {
            double now = World.CurrentNetTimeMs();

            if (_lastNetTime == 0)
                _lastNetTime = now;

            var elap = now - _lastNetTime;

            // Send updates at an interval approx twice the frame time.
            // (This will be approx 30 FPS when target FPS is set to 60)
            if (elap >= (World.TARGET_FRAME_TIME_NET))
            {
                SendPlaneUpdates();
                SendMissileUpdates();
                SendExpiredObjects();

                _lastNetTime = World.CurrentNetTimeMs();
            }

            double totalPacketTime = 0;
            int numPackets = 0;

            now = World.CurrentNetTimeMs();

            while (!Host.PacketReceiveQueue.IsEmpty)
            {
                if (Host.PacketReceiveQueue.TryDequeue(out NetPacket packet))
                {
                    var netPacket = packet;
                    totalPacketTime += now - netPacket.FrameTime;
                    numPackets++;

                    HandleNetPacket(netPacket, dt);
                }
            }

            // Enqueue deferred packets to be handled on the next frame.
            EnqueueDeferredPackets();

            if (totalPacketTime > 0f && numPackets > 0)
            {
                var avgDelay = (totalPacketTime / (float)numPackets);
                PacketDelay = _packetDelayAvg.Add(avgDelay);
            }
        }

        /// <summary>
        /// Adds deferred packets back into the receive queue in order to make another attempt at handling them.
        /// </summary>
        private void EnqueueDeferredPackets()
        {
            while (_deferredPackets.Count > 0)
            {
                Host.PacketReceiveQueue.Enqueue(_deferredPackets.First());
                _deferredPackets.RemoveAt(0);
            }
        }


        /// <summary>
        /// Remove deferred packets with IDs matching the specified expired ID.
        /// </summary>
        /// <param name="expiredID"></param>
        private void PruneExpiredDeferredPackets(GameID expiredID)
        {
            for (int i = _deferredPackets.Count - 1; i >= 0; i--)
            {
                var packet = _deferredPackets[i];

                if (packet.ID.Equals(expiredID))
                    _deferredPackets.RemoveAt(i);
            }
        }

        /// <summary>
        /// Queues the specified packet to be handled on the next frame.
        /// 
        /// For situations where we can't handle the packet due to other packets which haven't arrived.
        /// </summary>
        /// <param name="packet"></param>
        private void DeferPacket(NetPacket packet)
        {
            if (packet.Age < MAX_DEFER_AGE)
            {
                _deferredPackets.Add(packet);
            }
            else
            {
                Debug.WriteLine($"Can't defer, too old!  ID: {packet.ID}  Type: {packet.Type}   Age: {packet.Age}");
            }
        }

        /// <summary>
        /// True if we are not the server and we have received the ID and the first sync packets from the server.
        /// </summary>
        /// <returns></returns>
        private bool ClientIsReady()
        {
            if (IsServer)
                return true;

            return !IsServer && _netIDIsSet && _receivedFirstSync;
        }

        private void SaveImpact(ImpactPacket impactPacket)
        {
            if (!this.IsServer)
                return;

            if (impactPacket.ImpactType == ImpactType.Splash)
                return;

            if (_impacts.TryGetValue(impactPacket.ID.PlayerID, out var impacts))
                impacts.Add(impactPacket);
            else
                _impacts.Add(impactPacket.ID.PlayerID, new List<ImpactPacket>() { impactPacket });
        }

        private void ClearImpacts(int playerID)
        {
            if (!this.IsServer)
                return;

            if (_impacts.ContainsKey(playerID))
                _impacts.Remove(playerID);
        }


        private void HandleNetPacket(NetPacket packet, float dt)
        {
            switch (packet.Type)
            {
                case PacketTypes.PlaneListUpdate:

                    var planeListPacket = packet as PlaneListPacket;
                    DoNetPlaneUpdates(planeListPacket);

                    break;

                case PacketTypes.PlaneUpdate:

                    var planePacket = packet as PlanePacket;
                    DoNetPlaneUpdate(planePacket);

                    break;

                case PacketTypes.MissileUpdateList:

                    var missileListPacket = packet as MissileListPacket;
                    DoNetMissileUpdates(missileListPacket);

                    break;

                case PacketTypes.MissileUpdate:

                    var missilePacket = packet as MissilePacket;
                    DoNetMissileUpdate(missilePacket);

                    break;

                case PacketTypes.Impact:

                    var impactPacket = packet as ImpactPacket;
                    DoNetImpact(impactPacket, dt);

                    break;

                case PacketTypes.NewPlayer:

                    var playerPacket = packet as NewPlayerPacket;

                    if (IsServer)
                    {
                        if (playerPacket != null)
                        {
                            var newPlane = new FighterPlane(playerPacket.Position, playerPacket.PlaneColor, playerPacket.ID, isAI: false, isNetPlane: true);
                            newPlane.PlayerName = playerPacket.Name;
                            newPlane.IsNetObject = true;
                            _objs.AddPlane(newPlane);
                        }

                        ServerSendOtherPlanes();
                        ServerSendExistingBullets();
                        ServerSendExistingMissiles();
                        ServerSendExistingDecoys();
                        ServerSendExistingImpacts();
                        SendSyncPacket();
                    }

                    PlayerJoined?.Invoke(this, playerPacket.ID.PlayerID);

                    break;

                case PacketTypes.NewBullet:

                    var bulletPacket = packet as GameObjectPacket;
                    DoNewBullet(bulletPacket, dt);

                    break;

                case PacketTypes.NewMissile:

                    var newMissilePacket = packet as MissilePacket;
                    DoNewMissile(newMissilePacket);

                    break;

                case PacketTypes.NewDecoy:

                    var newDecoyPacket = packet as GameObjectPacket;
                    DoNewDecoy(newDecoyPacket);

                    break;

                case PacketTypes.SetID:

                    if (!IsServer)
                    {
                        var idPacket = packet as BasicPacket;

                        _netIDIsSet = true;

                        var newID = new GameID(idPacket.ID.PlayerID, PlayerPlane.ID.ObjectID);
                        _objs.ChangeObjID(PlayerPlane, newID);

                        PlayerPlane.Position = idPacket.Position;
                        PlayerPlane.SyncFixtures();

                        var newPlayerPacket = new NewPlayerPacket(PlayerPlane);
                        Host.EnqueuePacket(newPlayerPacket);

                        PlayerIDReceived?.Invoke(this, packet.ID.PlayerID);
                    }

                    break;

                case PacketTypes.ChatMessage:

                    var chatPacket = packet as ChatPacket;
                    NewChatMessage?.Invoke(this, chatPacket);

                    break;

                case PacketTypes.GetOtherPlanes:

                    if (!IsServer)
                    {
                        var listPacket = packet as PlayerListPacket;
                        DoNewPlayers(listPacket);
                    }

                    break;

                case PacketTypes.ExpiredObjects:

                    var expiredPacket = packet as BasicListPacket;

                    foreach (var p in expiredPacket.Packets)
                    {
                        var obj = _objs.GetObjectByID(p.ID);

                        if (obj != null)
                        {
                            obj.Position = p.Position;
                            obj.IsExpired = true;
                        }

                        PruneExpiredDeferredPackets(p.ID);
                    }

                    break;

                case PacketTypes.PlayerDisconnect:

                    var disconnectPack = packet as BasicPacket;
                    DoPlayerDisconnected(disconnectPack.ID.PlayerID);
                    ClearImpacts(disconnectPack.ID.PlayerID);

                    break;

                case PacketTypes.PlayerReset:

                    var resetPack = packet as BasicPacket;

                    var resetPlane = _objs.GetObjectByID(resetPack.ID) as FighterPlane;

                    if (resetPlane != null)
                    {
                        ClearImpacts(resetPack.ID.PlayerID);
                        resetPlane.FixPlane();
                        PlayerRespawned?.Invoke(this, resetPlane);
                    }

                    break;

                case PacketTypes.ServerSync:

                    var syncPack = packet as SyncPacket;
                    if (syncPack != null)
                    {
                        if (!IsServer)
                        {
                            var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                            World.ServerTimeOffset = syncPack.ServerTime - now;
                            World.TimeOfDay = syncPack.TimeOfDay;
                            World.TimeOfDayDir = syncPack.TimeOfDayDir;
                            World.GunsOnly = syncPack.GunsOnly;
                            World.DT = syncPack.DeltaTime;

                            _receivedFirstSync = true;
                        }
                    }
                    break;

                case PacketTypes.KickPlayer:

                    var kickPacket = packet as BasicPacket;

                    if (!IsServer)
                    {
                        if (kickPacket.ID.Equals(PlayerPlane.ID))
                        {
                            Host.SendPlayerDisconnectPacket((uint)kickPacket.ID.PlayerID);
                            Host.Disconnect(kickPacket.ID.PlayerID);
                        }
                    }

                    ClearImpacts(kickPacket.ID.PlayerID);
                    PlayerKicked?.Invoke(this, kickPacket.ID.PlayerID);

                    break;

                case PacketTypes.ImpactList:

                    if (!IsServer)
                    {
                        var impactsPacket = packet as ImpactListPacket;

                        if (impactsPacket != null)
                        {
                            HandleNewImpactList(impactsPacket);
                        }
                    }
                    break;

                case PacketTypes.BulletList:

                    var bulletList = packet as GameObjectListPacket;

                    if (!IsServer)
                    {
                        bulletList.Packets.ForEach(b => DoNewBullet(b, dt));
                    }

                    break;

                case PacketTypes.MissileList:

                    var missileList = packet as MissileListPacket;

                    if (!IsServer)
                    {
                        missileList.Missiles.ForEach(DoNewMissile);
                    }

                    break;

                case PacketTypes.DecoyList:

                    var decoyList = packet as GameObjectListPacket;

                    if (!IsServer)
                    {
                        decoyList.Packets.ForEach(d =>
                        {
                            var owner = GetNetPlane(d.OwnerID);

                            if (owner != null)
                            {
                                var decoy = new Decoy(owner, d.Position, d.Velocity);
                                decoy.ID = d.ID;
                                _objs.EnqueueDecoy(decoy);
                            }
                        });
                    }

                    break;

                case PacketTypes.PlayerEvent:

                    var eventPacket = packet as PlayerEventPacket;

                    if (eventPacket != null)
                    {
                        PlayerEventMessage?.Invoke(this, eventPacket.Message);
                    }

                    break;
            }
        }

        private void HandleNewImpactList(ImpactListPacket impacts)
        {
            foreach (var impact in impacts.Impacts)
            {
                var plane = _objs.GetPlaneByPlayerID(impact.ID.PlayerID);

                if (plane != null)
                {
                    var ogState = new PlanePacket(plane);

                    plane.Rotation = impact.Rotation;
                    plane.Velocity = impact.Velocity;
                    plane.Position = impact.Position;
                    plane.SyncFixtures();

                    plane.AddImpact(impact.ImpactPoint, impact.ImpactAngle);

                    plane.Rotation = ogState.Rotation;
                    plane.Velocity = ogState.Velocity;
                    plane.Position = ogState.Position;
                    plane.SyncFixtures();
                }
            }
        }

        private void SendPlaneUpdates()
        {
            if (IsServer)
            {
                var newPlanesPacket = new PlaneListPacket();

                foreach (var plane in _objs.Planes)
                {
                    // Don't send updates for human planes.
                    // Those packets are already re-broadcast by the net host.
                    if (!plane.IsAI)
                        continue;

                    var planePacket = new PlanePacket(plane, PacketTypes.PlaneUpdate);
                    newPlanesPacket.Planes.Add(planePacket);
                }

                if (newPlanesPacket.Planes.Count > 0)
                    Host.EnqueuePacket(newPlanesPacket);
            }
            else
            {
                var planePacket = new PlanePacket(PlayerPlane, PacketTypes.PlaneUpdate);
                planePacket.Type = PacketTypes.PlaneUpdate;
                Host.EnqueuePacket(planePacket);
            }
        }

        private void SendMissileUpdates()
        {
            var newMissilesPacket = new MissileListPacket();

            if (IsServer)
            {
                _objs.Missiles.ForEach(m =>
                {
                    // Don't send updates for net missiles.
                    // (Already re-broadcast by the net host.)
                    if (!m.IsNetObject)
                        newMissilesPacket.Missiles.Add(new MissilePacket(m as GuidedMissile, PacketTypes.MissileUpdate));
                });
            }
            else
            {
                var missiles = _objs.Missiles.Where(m => m.PlayerID == PlayerPlane.PlayerID);
                foreach (var m in missiles)
                    newMissilesPacket.Missiles.Add(new MissilePacket(m as GuidedMissile, PacketTypes.MissileUpdate));
            }

            if (newMissilesPacket.Missiles.Count > 0)
                Host.EnqueuePacket(newMissilesPacket);
        }

        private void SendExpiredObjects()
        {
            var expiredObjPacket = new BasicListPacket(PacketTypes.ExpiredObjects);

            // Collect expired objects and remove them as we go.
            var expiredObjs = _objs.ExpiredObjects();
            while (expiredObjs.Count > 0)
            {
                var obj = expiredObjs[0];
                var packet = new BasicPacket(PacketTypes.ExpiredObjects, obj.ID, obj.Position);
                expiredObjPacket.Packets.Add(packet);

                expiredObjs.RemoveAt(0);
            }

            if (expiredObjPacket.Packets.Count > 0)
                Host.EnqueuePacket(expiredObjPacket);
        }

        public void SendNewBulletPacket(Bullet bullet)
        {
            var bulletPacket = new GameObjectPacket(bullet, PacketTypes.NewBullet);
            Host.EnqueuePacket(bulletPacket);
        }

        public void SendNewMissilePacket(GuidedMissile missile)
        {
            var missilePacket = new MissilePacket(missile, PacketTypes.NewMissile);
            Host.EnqueuePacket(missilePacket);
        }

        public void SendSyncPacket()
        {
            var syncPacket = new SyncPacket(World.CurrentNetTimeMs(), World.TimeOfDay, World.TimeOfDayDir, World.GunsOnly, World.DT);
            Host.EnqueuePacket(syncPacket);
        }

        public void SendNewChatPacket(string message, string playerName)
        {
            var chatPacket = new ChatPacket(message.Trim(), playerName);
            Host.EnqueuePacket(chatPacket);
        }

        public void SendNewDecoyPacket(Decoy decoy)
        {
            var decoyPacket = new GameObjectPacket(decoy, PacketTypes.NewDecoy);
            Host.EnqueuePacket(decoyPacket);
        }

        public void ServerSendOtherPlanes()
        {
            var otherPlanesPackets = new List<NewPlayerPacket>();

            foreach (var plane in _objs.Planes)
            {
                otherPlanesPackets.Add(new NewPlayerPacket(plane));
            }

            var listPacket = new PlayerListPacket(PacketTypes.GetOtherPlanes, otherPlanesPackets);
            Host.EnqueuePacket(listPacket);
        }

        public void ServerSendExistingBullets()
        {
            var bulletsPacket = new GameObjectListPacket(PacketTypes.BulletList);

            foreach (var bullet in _objs.Bullets)
            {
                bulletsPacket.Packets.Add(new GameObjectPacket(bullet, PacketTypes.NewBullet));
            }

            Host.EnqueuePacket(bulletsPacket);
        }

        public void ServerSendExistingMissiles()
        {
            var missileListPacket = new MissileListPacket();
            missileListPacket.Type = PacketTypes.MissileList;

            foreach (var missile in _objs.Missiles)
            {
                missileListPacket.Missiles.Add(new MissilePacket(missile as GuidedMissile, PacketTypes.NewMissile));
            }

            Host.EnqueuePacket(missileListPacket);
        }

        public void ServerSendExistingDecoys()
        {
            var decoysPacket = new GameObjectListPacket(PacketTypes.DecoyList);

            foreach (var decoy in _objs.Decoys)
            {
                decoysPacket.Packets.Add(new GameObjectPacket(decoy, PacketTypes.NewDecoy));
            }

            Host.EnqueuePacket(decoysPacket);
        }

        public void ServerSendExistingImpacts()
        {
            var impactsPacket = new ImpactListPacket();

            foreach (var impactList in _impacts.Values)
            {
                impactsPacket.Impacts.AddRange(impactList);
            }

            Host.EnqueuePacket(impactsPacket);
        }

        public void SendPlaneReset(FighterPlane plane)
        {
            var resetPacket = new BasicPacket(PacketTypes.PlayerReset, plane.ID);
            ClearImpacts(plane.PlayerID);
            Host.EnqueuePacket(resetPacket);
        }

        public void SendNetImpact(GameObject impactor, GameObject target, PlaneImpactResult result, GameObjectPacket histState)
        {
            var impactPacket = new ImpactPacket(target, impactor.ID, result.ImpactPoint, result.ImpactAngle, result.DamageAmount, result.WasHeadshot, result.Type);
            impactPacket.OwnerID = impactor.Owner.ID;

            SaveImpact(impactPacket);

            Host.EnqueuePacket(impactPacket);
        }

        public void SendNewDecoy(Decoy decoy)
        {
            SendNewDecoyPacket(decoy);
        }


        // Net updates.

        private void DoNewPlayers(PlayerListPacket players)
        {
            foreach (var player in players.Players)
            {
                var existing = _objs.Contains(player.ID);

                if (!existing)
                {
                    var newPlane = new FighterPlane(player.Position, player.PlaneColor, player.ID, isAI: false, isNetPlane: true);
                    newPlane.PlayerName = player.Name;
                    newPlane.IsNetObject = true;
                    newPlane.LagAmount = World.CurrentNetTimeMs() - players.FrameTime;
                    newPlane.PlayerHitCallback = (evt) => ImpactEvent?.Invoke(this, evt);

                    _objs.AddPlane(newPlane);
                }
            }
        }

        private void DoNetPlaneUpdates(PlaneListPacket listPacket)
        {
            // If we are a client and our ID has been set.
            if (!ClientIsReady())
            {
                DeferPacket(listPacket);
                return;
            }

            foreach (var planeUpdPacket in listPacket.Planes)
            {
                DoNetPlaneUpdate(planeUpdPacket);
            }
        }

        private void DoNetPlaneUpdate(PlanePacket planePacket)
        {
            // If we are a client and our ID has been set.
            if (!ClientIsReady())
            {
                DeferPacket(planePacket);
                return;
            }

            var netPlane = GetNetPlane(planePacket.ID);

            if (netPlane != null)
            {
                planePacket.SyncObj(netPlane);
                netPlane.NetUpdate(planePacket.Position, planePacket.Velocity, planePacket.Rotation, planePacket.FrameTime);
            }
        }

        private void DoNetMissileUpdates(MissileListPacket listPacket)
        {
            foreach (var missileUpdate in listPacket.Missiles)
            {
                DoNetMissileUpdate(missileUpdate);
            }
        }

        private void DoNetMissileUpdate(MissilePacket missilePacket)
        {
            var netMissile = _objs.GetObjectByID(missilePacket.ID) as GuidedMissile;

            if (netMissile != null)
            {
                var netMissileOwner = _objs.GetObjectByID(netMissile.Owner.ID);

                if (_objs.TryGetObjectByID(missilePacket.TargetID, out GameObject netMissileTarget))
                {
                    if (netMissileTarget != null)
                    {
                        if (!netMissileTarget.Equals(netMissile.Target))
                        {
                            if (netMissile.Target != null && netMissile.Target is DummyObject)
                                netMissile.Target.IsExpired = true;

                            netMissile.ChangeTarget(netMissileTarget);
                        }
                    }
                }

                if (netMissileOwner != null && netMissileOwner.IsNetObject)
                {
                    missilePacket.SyncObj(netMissile);
                    netMissile.NetUpdate(missilePacket.Position, missilePacket.Velocity, missilePacket.Rotation, missilePacket.FrameTime);
                }
            }
            else
            {
                // If we receive an update for a non-existent missile, defer the update for the next frame.
                DeferPacket(missilePacket);
            }
        }

        private void DoNetImpact(ImpactPacket packet, float dt)
        {
            if (packet != null)
            {
                var impactor = _objs.GetObjectByID(packet.ImpactorID);
                var impactorOwner = _objs.GetObjectByID(packet.OwnerID);

                if (impactor == null)
                {
                    // If the impactor hasn't arrived yet, or has already been removed
                    // add a dummy object in its place.
                    // This can happen with splash damage impacts coming in from the server for
                    // missiles which have already been expired.
                    impactor = _objs.AddDummyObject(packet.ImpactorID);
                }

                impactor.Owner = impactorOwner;
                impactor.Position = packet.ImpactPoint;

                // Go ahead and expire the impactor.
                if (impactor is not FighterPlane)
                    impactor.IsExpired = true;

                var target = _objs.GetObjectByID(packet.ID) as FighterPlane;

                if (target != null)
                {
                    // Move the plane to the server position, do the impact, then move it back.
                    // This is to make sure the impacts/bullet holes show up in the correct place.
                    var ogState = new PlanePacket(target);

                    target.Rotation = packet.Rotation;
                    target.Position = packet.Position;
                    target.SyncFixtures();

                    var impactPoint = packet.ImpactPoint;
                    var result = new PlaneImpactResult(packet.ImpactType, impactPoint, packet.ImpactAngle, packet.DamageAmount, packet.WasHeadshot);

                    target.HandleImpactResult(impactor, result, dt);

                    target.Rotation = ogState.Rotation;
                    target.Position = ogState.Position;
                    target.SyncFixtures();
                }
            }
        }

        private void DoNewBullet(GameObjectPacket bulletPacket, float dt)
        {
            var owner = GetNetPlane(bulletPacket.OwnerID);

            if (owner == null)
            {
                // Defer new bullets until owner is added. (Maybe)
                DeferPacket(bulletPacket);
                return;
            }

            var bullet = new Bullet(bulletPacket.Position, bulletPacket.Velocity, bulletPacket.Rotation);
            bullet.IsNetObject = true;
            bullet.ID = bulletPacket.ID;

            bulletPacket.SyncObj(bullet);

            bullet.Owner = owner;
            bullet.LagAmount = World.CurrentNetTimeMs() - bulletPacket.FrameTime;

            // Try to spawn the bullet ahead (extrapolate) to compensate for latency?
            bullet.Position += bullet.Velocity * (bullet.LagAmountFrames * dt);

            _objs.EnqueueBullet(bullet);
        }

        private void DoNewMissile(MissilePacket missilePacket)
        {
            var missileOwner = GetNetPlane(missilePacket.OwnerID);

            if (missileOwner != null)
            {
                var missileTarget = _objs.GetObjectByID(missilePacket.TargetID);

                // If the missile target doesn't exist (yet?),
                // spawn an invisible dummy object so we can handle net updates for it.
                // This can happen if the missile is targeting a decoy
                // which is already expired by this point.
                if (missileTarget == null)
                    missileTarget = _objs.AddDummyObject(missilePacket.TargetID);

                var missile = new GuidedMissile(missileOwner, missilePacket.Position, missilePacket.Velocity, missilePacket.Rotation);
                missile.IsNetObject = true;
                missile.ID = missilePacket.ID;
                missilePacket.SyncObj(missile);
                missile.Target = missileTarget;
                missile.LagAmount = World.CurrentNetTimeMs() - missilePacket.FrameTime;

                _objs.EnqueueMissile(missile);
            }
            else
            {
                DeferPacket(missilePacket);
            }
        }

        private void DoNewDecoy(GameObjectPacket decoyPacket)
        {
            var decoyOwner = GetNetPlane(decoyPacket.OwnerID);

            if (decoyOwner != null)
            {
                var decoy = new Decoy(decoyOwner, decoyOwner.ExhaustPosition, decoyPacket.Velocity);
                decoy.IsNetObject = true;
                decoy.ID = decoyPacket.ID;
                decoyPacket.SyncObj(decoy);

                _objs.EnqueueDecoy(decoy);
            }
            else
            {
                DeferPacket(decoyPacket);
            }
        }

        private void DoPlayerDisconnected(int playerID)
        {
            var playerPlane = _objs.GetPlaneByPlayerID(playerID);
            if (playerPlane != null)
                PlayerDisconnected?.Invoke(this, playerPlane.ID.PlayerID);

            var objs = _objs.GetObjectsByPlayer(playerID);

            foreach (var obj in objs)
            {
                if (obj.PlayerID == playerID)
                    obj.IsExpired = true;
            }
        }

        private FighterPlane GetNetPlane(GameID id, bool netOnly = true)
        {
            if (_objs.TryGetObjectByID(id, out GameObject obj))
            {
                if (obj != null && obj is FighterPlane plane)
                {
                    if (netOnly && !plane.IsNetObject)
                        return null;

                    return plane;
                }
            }

            return null;
        }
    }
}
