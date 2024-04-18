using PolyPlane.GameObjects;
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

        private long _frame = 0;
        private bool _netIDIsSet = false;

        public event EventHandler<int> PlayerIDReceived;
        public event EventHandler<ImpactEvent> ImpactEvent;
        public event EventHandler<ChatPacket> NewChatMessage;

        public NetEventManager(GameObjectManager objectManager, NetPlayHost host, FighterPlane playerPlane)
        {
            Objs = objectManager;
            Host = host;
            PlayerPlane = playerPlane;
            IsServer = false;
            ChatInterface = new ChatInterface(this, playerPlane.PlayerName);
        }

        public NetEventManager(GameObjectManager objectManager, NetPlayHost host)
        {
            Objs = objectManager;
            Host = host;
            PlayerPlane = null;
            IsServer = true;
            ChatInterface = new ChatInterface(this, "Player");
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

                    if (IsServer)
                    {
                        var planePacket = packet as NewPlayerPacket;

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

                    break;

                case PacketTypes.PlayerReset:

                    var resetPack = packet as Net.BasicPacket;

                    var resetPlane = Objs.GetObjectByID(resetPack.ID) as FighterPlane;

                    if (resetPlane != null)
                        resetPlane.FixPlane();

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

            var listPacket = new Net.PlayerListPacket(PacketTypes.GetOtherPlanes, otherPlanesPackets);
            Host.EnqueuePacket(listPacket);
        }

        public void SendNetImpact(GameObject impactor, GameObject target, PlaneImpactResult result, GameObjectPacket histState)
        {
            var impactPacket = new Net.ImpactPacket(target, impactor.ID, result.ImpactPoint, result.DoesDamage, result.WasHeadshot, result.Type == ImpactType.Missile);

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
            var decoyPacket = new Net.DecoyPacket(decoy);

            Host.EnqueuePacket(decoyPacket);
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
                    var curRot = target.Rotation;
                    var curVelo = target.Velocity;
                    var curPos = target.Position;

                    target.Rotation = packet.Rotation;
                    target.Velocity = packet.Velocity.ToD2DPoint();
                    target.Position = packet.Position.ToD2DPoint();
                    target.SyncFixtures();

                    // TODO: Consider sending the planes flip direction over the net, as it is likely to not be in sync with clients.
                    var impactPoint = packet.ImpactPoint.ToD2DPoint();
                    var result = new PlaneImpactResult(packet.WasMissile ? ImpactType.Missile : ImpactType.Bullet, impactPoint, packet.DoesDamage);
                    target.HandleImpactResult(impactor, result);

                    target.Rotation = curRot;
                    target.Velocity = curVelo;
                    target.Position = curPos;
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

        private void DoNewBullet(BulletPacket bulletPacket)
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

        private void DoNewDecoy(DecoyPacket decoyPacket)
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
            var objs = Objs.GetObjectsByPlayer(playerID);

            foreach (var obj in objs)
            {
                if (obj.PlayerID == playerID)
                    obj.IsExpired = true;
            }
        }

        private FighterPlane GetNetPlane(GameID id, bool netOnly = true)
        {
            foreach (var plane in Objs.Planes)
            {
                if (netOnly && !plane.IsNetObject)
                    continue;

                if (plane.ID.Equals(id))
                    return plane as FighterPlane;
            }

            return null;
        }
    }
}
