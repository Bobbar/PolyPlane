using PolyPlane.GameObjects;
using PolyPlane.GameObjects.Manager;
using PolyPlane.Helpers;
using PolyPlane.Net.NetHost;

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

        private long _frame = 0;
        private bool _netIDIsSet = false;

        public event EventHandler<int> PlayerIDReceived;
        public event EventHandler<ImpactEvent> ImpactEvent;
        public event EventHandler<ChatPacket> NewChatMessage;
        public event EventHandler<int> PlayerKicked;
        public event EventHandler<int> PlayerDisconnected;
        public event EventHandler<int> PlayerJoined;
        public event EventHandler<FighterPlane> PlayerRespawned;
        public event EventHandler<string> PlayerEventMessage;

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
            if (!IsServer)
            {
                var otherObjs = _objs.GetAllNetObjects();
                foreach (var obj in otherObjs)
                    obj.IsExpired = true;
            }
            else
                ClearImpacts((int)e.ID);
        }

        public void DoNetEvents()
        {
            _frame++;

            double now = 0;
            double totalPacketTime = 0;
            int numPackets = 0;

            // Send updates every other frame.
            if (_frame % 2 == 0)
            {
                SendPlaneUpdates();
                SendMissileUpdates();
                SendExpiredObjects();
            }

            now = World.CurrentTime();

            while (Host.PacketReceiveQueue.Count > 0)
            {
                if (Host.PacketReceiveQueue.TryDequeue(out object packet))
                {
                    var netPacket = (NetPacket)packet;
                    totalPacketTime += now - netPacket.FrameTime;
                    numPackets++;

                    HandleNetPacket(netPacket);
                }
            }

            if (totalPacketTime > 0f && numPackets > 0)
            {
                var avgDelay = (totalPacketTime / (float)numPackets);
                PacketDelay = _packetDelayAvg.Add(avgDelay);
            }
        }

        private void HandleNetPacket(NetPacket packet)
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
                case PacketTypes.MissileUpdate:

                    var missilePacket = packet as MissileListPacket;
                    DoNetMissileUpdates(missilePacket);

                    break;
                case PacketTypes.Impact:

                    var impactPacket = packet as ImpactPacket;
                    DoNetImpact(impactPacket);

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
                        ServerSendExistingDecoys();
                        SendSyncPacket();
                    }

                    PlayerJoined?.Invoke(this, playerPacket.ID.PlayerID);

                    break;
                case PacketTypes.NewBullet:

                    var bulletPacket = packet as GameObjectPacket;
                    DoNewBullet(bulletPacket);

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
                        _netIDIsSet = true;

                        var newID = new GameID(packet.ID.PlayerID, PlayerPlane.ID.ObjectID);
                        _objs.ChangeObjID(PlayerPlane, newID);
                        var netPacket = new NewPlayerPacket(PlayerPlane);
                        Host.EnqueuePacket(netPacket);

                        PlayerIDReceived?.Invoke(this, packet.ID.PlayerID);
                    }

                    break;
                case PacketTypes.ChatMessage:

                    var chatPacket = packet as ChatPacket;
                    NewChatMessage?.Invoke(this, chatPacket);

                    break;
                case PacketTypes.GetOtherPlanes:

                    if (IsServer)
                    {
                        ServerSendOtherPlanes();
                        ServerSendExistingImpacts();
                        ServerSendExistingBullets();
                        ServerSendExistingDecoys();
                    }
                    else
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
                            obj.IsExpired = true;
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
                        bulletList.Packets.ForEach(b => DoNewBullet(b));
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

        private void SaveImpact(ImpactPacket impactPacket)
        {
            if (!this.IsServer)
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

            // Collect expried objs and remove them as we go.
            var expiredObjs = _objs.ExpiredObjects();
            while (expiredObjs.Count > 0)
            {
                expiredObjPacket.Packets.Add(new BasicPacket(PacketTypes.ExpiredObjects, expiredObjs[0].ID));
                expiredObjs.RemoveAt(0);
            }

            if (expiredObjPacket.Packets.Count > 0)
                Host.EnqueuePacket(expiredObjPacket);
        }

        public void SendNewBulletPacket(Bullet bullet)
        {
            var netPacket = new GameObjectPacket(bullet, PacketTypes.NewBullet);
            Host.EnqueuePacket(netPacket);
        }

        public void SendNewMissilePacket(GuidedMissile missile)
        {
            var netPacket = new MissilePacket(missile, PacketTypes.NewMissile);
            Host.EnqueuePacket(netPacket);
        }

        public void SendSyncPacket()
        {
            var packet = new SyncPacket(World.CurrentTime(), World.TimeOfDay, World.TimeOfDayDir, World.GunsOnly, World.DT);
            Host.EnqueuePacket(packet);
        }

        public void SendNewChatPacket(string message, string playerName)
        {
            var packet = new ChatPacket(message.Trim(), playerName);
            Host.EnqueuePacket(packet);
        }

        public void SendNewDecoyPacket(Decoy decoy)
        {
            var packet = new GameObjectPacket(decoy, PacketTypes.NewDecoy);
            Host.EnqueuePacket(packet);
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
            var impactPacket = new ImpactPacket(target, impactor.ID, result.ImpactPoint, result.ImpactAngle, result.DoesDamage, result.WasHeadshot, result.Type == ImpactType.Missile);
            SaveImpact(impactPacket);

            if (histState != null)
            {
                impactPacket.Position = histState.Position;
                impactPacket.Velocity = histState.Velocity;
                impactPacket.Rotation = histState.Rotation;
            }

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
                    newPlane.LagAmount = World.CurrentTime() - players.FrameTime;
                    _objs.AddPlane(newPlane);
                }
            }
        }

        private void DoNetPlaneUpdates(PlaneListPacket listPacket)
        {
            if (!IsServer && !_netIDIsSet)
                return;

            foreach (var planeUpdPacket in listPacket.Planes)
            {
                DoNetPlaneUpdate(planeUpdPacket);
            }
        }

        private void DoNetPlaneUpdate(PlanePacket planePacket)
        {
            if (!IsServer && !_netIDIsSet)
                return;

            var netPlane = GetNetPlane(planePacket.ID);

            if (netPlane != null)
            {
                planePacket.SyncObj(netPlane);

                netPlane.LagAmount = World.CurrentTime() - planePacket.FrameTime;
                netPlane.NetUpdate(World.DT, planePacket.Position, planePacket.Velocity, planePacket.Rotation, planePacket.FrameTime);
            }
        }

        private void DoNetMissileUpdates(MissileListPacket listPacket)
        {
            foreach (var missileUpdate in listPacket.Missiles)
            {
                var netMissile = _objs.GetObjectByID(missileUpdate.ID) as GuidedMissile;

                if (netMissile == null)
                {
                    // If we receive an update for a non-existent missile, just go ahead and create it.
                    DoNewMissile(missileUpdate);
                    continue;
                }

                if (netMissile != null)
                {
                    var netMissileOwner = _objs.GetObjectByID(netMissile.Owner.ID);

                    if (_objs.TryGetObjectByID(missileUpdate.TargetID, out GameObject netMissileTarget))
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
                        missileUpdate.SyncObj(netMissile);
                        netMissile.NetUpdate(World.DT, missileUpdate.Position, missileUpdate.Velocity, missileUpdate.Rotation, missileUpdate.FrameTime);
                    }
                }
            }
        }

        private void DoNetImpact(ImpactPacket packet)
        {
            if (packet != null)
            {
                var impactor = _objs.GetObjectByID(packet.ImpactorID) as GameObjectPoly;

                if (impactor == null)
                    return;

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
                    var result = new PlaneImpactResult(packet.WasMissile ? ImpactType.Missile : ImpactType.Bullet, impactPoint, packet.ImpactAngle, packet.DoesDamage, packet.WasHeadshot);
                    target.HandleImpactResult(impactor, result);

                    target.Rotation = ogState.Rotation;
                    target.Position = ogState.Position;
                    target.SyncFixtures();

                    if (!IsServer)
                    {
                        ImpactEvent?.Invoke(this, new ImpactEvent(target, impactor, packet.DoesDamage));

                    }
                }
            }
        }

        private void DoNewBullet(GameObjectPacket bulletPacket)
        {
            var bullet = new Bullet(bulletPacket.Position, bulletPacket.Velocity, bulletPacket.Rotation);
            bullet.IsNetObject = true;
            bullet.ID = bulletPacket.ID;

            bulletPacket.SyncObj(bullet);
            var owner = GetNetPlane(bulletPacket.OwnerID);

            // TODO: How to handle bullets that arrive before owner plane has been added?
            if (owner == null)
                return;

            bullet.Owner = owner;
            bullet.LagAmount = World.CurrentTime() - bulletPacket.FrameTime;
            // Try to spawn the bullet ahead (extrapolate) to compensate for latency?
            bullet.Position += bullet.Velocity * (bullet.LagAmountFrames * World.DT);

            _objs.EnqueueBullet(bullet);
        }

        private void DoNewMissile(MissilePacket missilePacket)
        {
            var missileOwner = GetNetPlane(missilePacket.OwnerID);

            if (missileOwner != null)
            {
                var missileTarget = _objs.GetObjectByID(missilePacket.TargetID);

                // If the missile target doesn't exist (yet?), spawn an invisible dummy object so we can handle net updates for it.
                if (missileTarget == null)
                    missileTarget = _objs.AddDummyObject();

                var missile = new GuidedMissile(missileOwner, missilePacket.Position, missilePacket.Velocity, missilePacket.Rotation);
                missile.IsNetObject = true;
                missile.ID = missilePacket.ID;
                missilePacket.SyncObj(missile);
                missile.Target = missileTarget;
                missile.LagAmount = World.CurrentTime() - missilePacket.FrameTime;
                _objs.EnqueueMissile(missile);
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

                if (IsServer)
                    Host.EnqueuePacket(decoyPacket);
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
