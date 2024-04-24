﻿using PolyPlane.GameObjects;
using PolyPlane.GameObjects.Manager;

namespace PolyPlane.Net
{
    public class NetEventManager : IImpactEvent
    {
        public GameObjectManager Objs;
        public NetPlayHost Host;
        public ChatInterface ChatInterface;
        public bool IsServer = false;
        public FighterPlane PlayerPlane = null;
        public double PacketDelay = 0;

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

        public NetEventManager(GameObjectManager objectManager, NetPlayHost host, FighterPlane playerPlane)
        {
            Objs = objectManager;
            Host = host;
            PlayerPlane = playerPlane;
            IsServer = false;
            ChatInterface = new ChatInterface(this, playerPlane.PlayerName);
            AttachEvents();
        }

        public NetEventManager(GameObjectManager objectManager, NetPlayHost host)
        {
            Objs = objectManager;
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
                var otherObjs = Objs.GetAllNetObjects();
                foreach (var obj in otherObjs)
                    obj.IsExpired = true;
            }
        }

        public void DoNetEvents()
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
                SendExpiredObjects();
            }

            now = World.CurrentTime();

            while (Host.PacketReceiveQueue.Count > 0)
            {
                if (Host.PacketReceiveQueue.TryDequeue(out Net.NetPacket packet))
                {
                    totalPacketTime += now - packet.FrameTime;
                    numPackets++;

                    HandleNetPacket(packet);
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
                    var planePacket = packet as NewPlayerPacket;

                    if (IsServer)
                    {

                        if (planePacket != null)
                        {
                            var newPlane = new FighterPlane(planePacket.Position.ToD2DPoint(), planePacket.PlaneColor);
                            newPlane.ID = planePacket.ID;
                            newPlane.PlayerName = planePacket.Name;
                            newPlane.IsNetObject = true;
                            newPlane.Radar = new Radar(newPlane, World.HudColor, Objs.Missiles, Objs.Planes);
                            Objs.AddPlane(newPlane);
                        }

                        ServerSendOtherPlanes();
                        Host.SendSyncPacket();
                    }

                    PlayerJoined?.Invoke(this, planePacket.ID.PlayerID);

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

                        Objs.ChangeObjID(PlayerPlane, new GameID(packet.ID.PlayerID, PlayerPlane.ID.ObjectID));
                        var netPacket = new NewPlayerPacket(PlayerPlane);
                        Host.EnqueuePacket(netPacket);

                        PlayerIDReceived?.Invoke(this, packet.ID.PlayerID);
                    }

                    break;
                case PacketTypes.GetNextID:
                    // Nuttin...
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
                    }
                    else
                    {
                        var listPacket = packet as Net.PlayerListPacket;

                        foreach (var player in listPacket.Players)
                        {
                            var existing = Objs.Contains(player.ID);

                            if (!existing)
                            {
                                var newPlane = new FighterPlane(player.Position.ToD2DPoint(), player.PlaneColor);
                                newPlane.ID = player.ID;
                                newPlane.PlayerName = player.Name;
                                newPlane.IsNetObject = true;
                                newPlane.LagAmount = World.CurrentTime() - listPacket.FrameTime;
                                newPlane.ClientCreateTime = listPacket.FrameTime;
                                newPlane.Radar = new Radar(newPlane, World.HudColor, Objs.Missiles, Objs.Planes);
                                Objs.AddPlane(newPlane);
                            }
                        }
                    }

                    break;

                case PacketTypes.ExpiredObjects:
                    var expiredPacket = packet as Net.BasicListPacket;

                    foreach (var p in expiredPacket.Packets)
                    {
                        var obj = Objs.GetObjectByID(p.ID);

                        if (obj != null)
                            obj.IsExpired = true;
                    }

                    break;

                case PacketTypes.PlayerDisconnect:
                    var disconnectPack = packet as Net.BasicPacket;
                    DoPlayerDisconnected(disconnectPack.ID.PlayerID);
                    ClearImpacts(disconnectPack.ID.PlayerID);

                    break;

                case PacketTypes.PlayerReset:

                    var resetPack = packet as Net.BasicPacket;

                    var resetPlane = Objs.GetObjectByID(resetPack.ID) as FighterPlane;

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
                var plane = Objs.GetPlaneByPlayerID(impact.ID.PlayerID);

                if (plane != null)
                {
                    var ogState = new PlanePacket(plane);

                    plane.Rotation = impact.Rotation;
                    plane.Velocity = impact.Velocity.ToD2DPoint();
                    plane.Position = impact.Position.ToD2DPoint();
                    plane.SyncFixtures();

                    plane.AddImpact(impact.ImpactPoint.ToD2DPoint());

                    plane.Rotation = ogState.Rotation;
                    plane.Velocity = ogState.Velocity.ToD2DPoint();
                    plane.Position = ogState.Position.ToD2DPoint();
                    plane.SyncFixtures();
                }
            }
        }

        private void SendPlaneUpdates()
        {
            var newPlanesPacket = new Net.PlaneListPacket();

            if (IsServer)
            {
                foreach (var plane in Objs.Planes)
                {
                    // Don't send updates for human planes.
                    // Those packets are already re-broadcast by the net host.
                    if (!plane.IsAI)
                        continue;

                    var planePacket = new Net.PlanePacket(plane);
                    newPlanesPacket.Planes.Add(planePacket);
                }
            }
            else
            {
                var planePacket = new Net.PlanePacket(PlayerPlane);
                newPlanesPacket.Planes.Add(planePacket);
            }

            if (newPlanesPacket.Planes.Count > 0)
                Host.EnqueuePacket(newPlanesPacket);
        }

        private void SendMissileUpdates()
        {
            var newMissilesPacket = new Net.MissileListPacket();

            if (IsServer)
            {
                Objs.Missiles.ForEach(m =>
                {
                    // Don't send updates for net missiles.
                    // (Already re-broadcast by the net host.)
                    if (!m.IsNetObject)
                        newMissilesPacket.Missiles.Add(new MissilePacket(m as GuidedMissile));
                });
            }
            else
            {
                var missiles = Objs.Missiles.Where(m => m.PlayerID == PlayerPlane.PlayerID).ToList();
                missiles.ForEach(m => newMissilesPacket.Missiles.Add(new MissilePacket(m as GuidedMissile)));
            }

            if (newMissilesPacket.Missiles.Count > 0)
                Host.EnqueuePacket(newMissilesPacket);
        }

        private void SendExpiredObjects()
        {
            var expiredObjPacket = new BasicListPacket(PacketTypes.ExpiredObjects);

            // Collect expried objs and remove them as we go.
            var expiredObjs = Objs.ExpiredObjects();
            while (expiredObjs.Count > 0)
            {
                expiredObjPacket.Packets.Add(new BasicPacket(PacketTypes.ExpiredObjects, expiredObjs[0].ID));
                expiredObjs.RemoveAt(0);
            }

            if (expiredObjPacket.Packets.Count == 0)
                return;

            Host.EnqueuePacket(expiredObjPacket);
        }

        public void ServerSendOtherPlanes()
        {
            var otherPlanesPackets = new List<NewPlayerPacket>();

            foreach (var plane in Objs.Planes)
            {
                otherPlanesPackets.Add(new NewPlayerPacket(plane));
            }

            var listPacket = new PlayerListPacket(PacketTypes.GetOtherPlanes, otherPlanesPackets);
            Host.EnqueuePacket(listPacket);
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

        public void SendNetImpact(GameObject impactor, GameObject target, PlaneImpactResult result, GameObjectPacket histState)
        {
            var impactPacket = new ImpactPacket(target, impactor.ID, result.ImpactPoint, result.DoesDamage, result.WasHeadshot, result.Type == ImpactType.Missile);
            SaveImpact(impactPacket);   

            if (histState != null)
            {
                impactPacket.Position = histState.Position;
                impactPacket.Velocity = histState.Velocity;
                impactPacket.Rotation = histState.Rotation;
            }

            Host.EnqueuePacket(impactPacket);
            DoNetImpact(impactPacket);
        }

        public void SendNewDecoy(Decoy decoy)
        {
            Host.SendNewDecoyPacket(decoy);
        }


        // Net updates.

        private void DoNetPlaneUpdates(PlaneListPacket listPacket)
        {
            if (!IsServer && !_netIDIsSet)
                return;

            foreach (var planeUpdPacket in listPacket.Planes)
            {
                var netPlane = GetNetPlane(planeUpdPacket.ID);

                if (netPlane != null)
                {
                    planeUpdPacket.SyncObj(netPlane);

                    netPlane.LagAmount = World.CurrentTime() - listPacket.FrameTime;
                    netPlane.NetUpdate(World.DT, World.ViewPortSize, World.RenderScale, planeUpdPacket.Position.ToD2DPoint(), planeUpdPacket.Velocity.ToD2DPoint(), planeUpdPacket.Rotation, planeUpdPacket.FrameTime);
                }
            }
        }

        private void DoNetMissileUpdates(MissileListPacket listPacket)
        {
            foreach (var missileUpdate in listPacket.Missiles)
            {
                var netMissile = Objs.GetObjectByID(missileUpdate.ID) as GuidedMissile;

                if (netMissile != null)
                {
                    var netMissileOwner = Objs.GetObjectByID(netMissile.Owner.ID);

                    if (Objs.TryGetObjectByID(missileUpdate.TargetID, out GameObject netMissileTarget))
                    {
                        if (netMissileTarget != null)
                        {
                            if (!netMissileTarget.ID.Equals(netMissile.Target.ID))
                                netMissile.ChangeTarget(netMissileTarget);
                        }
                    }

                    if (netMissileOwner != null && netMissileOwner.IsNetObject)
                    {
                        missileUpdate.SyncObj(netMissile);
                        netMissile.NetUpdate(World.DT, World.ViewPortSize, World.RenderScale, missileUpdate.Position.ToD2DPoint(), missileUpdate.Velocity.ToD2DPoint(), missileUpdate.Rotation, missileUpdate.FrameTime);
                    }
                }
            }
        }

        private void DoNetImpact(ImpactPacket packet)
        {
            if (packet != null)
            {
                var impactor = Objs.GetObjectByID(packet.ImpactorID);

                if (impactor == null)
                    return;

                //if (impactor.IsExpired)
                //    return;

                impactor.IsExpired = true;

                var target = Objs.GetObjectByID(packet.ID) as FighterPlane;

                if (target != null)
                {
                    // Move the plane to the server position, do the impact, then move it back.
                    // This is to make sure the impacts/bullet holes show up in the correct place.

                    var ogState = new PlanePacket(target);

                    target.Rotation = packet.Rotation;
                    target.Velocity = packet.Velocity.ToD2DPoint();
                    target.Position = packet.Position.ToD2DPoint();
                    target.SyncFixtures();

                    var impactPoint = packet.ImpactPoint.ToD2DPoint();
                    var result = new PlaneImpactResult(packet.WasMissile ? ImpactType.Missile : ImpactType.Bullet, impactPoint, packet.DoesDamage, packet.WasHeadshot);
                    target.HandleImpactResult(impactor, result);

                    target.Rotation = ogState.Rotation;
                    target.Velocity = ogState.Velocity.ToD2DPoint();
                    target.Position = ogState.Position.ToD2DPoint();
                    target.SyncFixtures();

                    if (!IsServer)
                    {
                        ImpactEvent?.Invoke(this, new ImpactEvent(target, impactor, packet.DoesDamage));

                        if (impactor is Bullet)
                            Objs.AddBulletExplosion(impactPoint);
                        else
                            Objs.AddExplosion(impactPoint);
                    }
                }
            }
        }

        private void DoNewBullet(GameObjectPacket bulletPacket)
        {
            var bullet = new Bullet(bulletPacket.Position.ToD2DPoint(), bulletPacket.Velocity.ToD2DPoint(), bulletPacket.Rotation);
            bullet.ID = bulletPacket.ID;
            bulletPacket.SyncObj(bullet);
            var owner = GetNetPlane(bulletPacket.OwnerID);

            // TODO: How to handle bullets that arrive before owner plane has been added?
            if (owner == null)
                return;

            bullet.Owner = owner;
            bullet.ClientCreateTime = bulletPacket.FrameTime;
            bullet.LagAmount = World.CurrentTime() - bulletPacket.FrameTime;

            // Try to spawn the bullet ahead to compensate for latency?
            bullet.Position += bullet.Velocity * (float)(bullet.LagAmount / 1000f);
            bullet.AddExplosionCallback = Objs.AddBulletExplosion;

            Objs.AddBullet(bullet);
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
                missile.LagAmount = World.CurrentTime() - missilePacket.FrameTime;
                Objs.EnqueueMissile(missile);
            }
        }

        private void DoNewDecoy(GameObjectPacket decoyPacket)
        {
            var decoyOwner = GetNetPlane(decoyPacket.OwnerID);

            if (decoyOwner != null)
            {
                var decoy = new Decoy(decoyOwner);
                decoy.ID = decoyPacket.ID;
                decoyPacket.SyncObj(decoy);

                bool containsDecoy = Objs.Contains(decoy.ID);

                if (!containsDecoy)
                {
                    Objs.AddDecoy(decoy);

                    if (IsServer)
                        Host.EnqueuePacket(decoyPacket);
                }
            }
        }

        private void DoPlayerDisconnected(int playerID)
        {
            var playerPlane = Objs.GetPlaneByPlayerID(playerID);
            if (playerPlane != null)
                PlayerDisconnected?.Invoke(this, playerPlane.ID.PlayerID);

            var objs = Objs.GetObjectsByPlayer(playerID);

            foreach (var obj in objs)
            {
                if (obj.PlayerID == playerID)
                    obj.IsExpired = true;
            }
        }

        private FighterPlane GetNetPlane(GameID id, bool netOnly = true)
        {
            if (Objs.TryGetObjectByID(id, out GameObject obj))
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
