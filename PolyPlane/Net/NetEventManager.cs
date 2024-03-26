using PolyPlane.GameObjects;
using unvell.D2DLib;

namespace PolyPlane.Net
{
    public class NetEventManager
    {
        public GameObjectManager Objs;
        public NetPlayHost Host;
        public bool IsServer = false;
        public Plane PlayerPlane = null;
        public double PacketDelay = 0;

        public Action<D2DColor> ScreenFlashCallback = null;
        public Action ScreenShakeCallback = null;

        private SmoothDouble _packetDelayAvg = new SmoothDouble(100);

        private long _frame = 0;
        private bool _netIDIsSet = false;

        public NetEventManager(GameObjectManager objectManager, NetPlayHost host, Plane playerPlane)
        {
            Objs = objectManager;
            Host = host;
            PlayerPlane = playerPlane;
            IsServer = false;
        }

        public NetEventManager(GameObjectManager objectManager, NetPlayHost host)
        {
            Objs = objectManager;
            Host = host;
            PlayerPlane = null;
            IsServer = true;
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
                //_server.SendSyncPacket();
            }

            SendExpiredObjects();

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
                        var planePacket = packet as PlanePacket;

                        if (planePacket != null)
                        {
                            var newPlane = new Plane(planePacket.Position.ToD2DPoint(), planePacket.PlaneColor);
                            newPlane.ID = planePacket.ID;
                            planePacket.SyncObj(newPlane);
                            newPlane.IsNetObject = true;
                            newPlane.Radar = new Radar(newPlane, D2DColor.GreenYellow, Objs.Missiles, Objs.Planes);
                            Objs.AddPlane(newPlane);
                        }

                        ServerSendOtherPlanes();
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
                        var netPacket = new PlanePacket(PlayerPlane, PacketTypes.NewPlayer);
                        Host.EnqueuePacket(netPacket);
                    }

                    break;
                case PacketTypes.GetNextID:
                    // Nuttin...
                    break;
                case PacketTypes.ChatMessage:
                    // Nuttin...
                    break;
                case PacketTypes.GetOtherPlanes:

                    if (IsServer)
                    {
                        ServerSendOtherPlanes();
                    }
                    else
                    {
                        var listPacket = packet as Net.PlaneListPacket;

                        foreach (var plane in listPacket.Planes)
                        {
                            var existing = Objs.Contains(plane.ID);

                            if (!existing)
                            {
                                var newPlane = new Plane(plane.Position.ToD2DPoint(), plane.PlaneColor);
                                newPlane.ID = plane.ID;
                                newPlane.IsNetObject = true;
                                newPlane.LagAmount = World.CurrentTime() - listPacket.FrameTime;
                                newPlane.ClientCreateTime = listPacket.FrameTime;
                                newPlane.Radar = new Radar(newPlane, D2DColor.GreenYellow, Objs.Missiles, Objs.Planes);
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

                    var resetPlane = Objs.GetObjectByID(resetPack.ID) as Plane;

                    if (resetPlane != null)
                        resetPlane.FixPlane();

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
                Objs.Missiles.ForEach(m => newMissilesPacket.Missiles.Add(new MissilePacket(m as GuidedMissile)));
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
            var expiredObjPacket = new Net.BasicListPacket();
            Objs.ExpiredObjects().ForEach(o => expiredObjPacket.Packets.Add(new BasicPacket(PacketTypes.ExpiredObjects, o.ID)));

            if (expiredObjPacket.Packets.Count == 0)
                return;

            Host.EnqueuePacket(expiredObjPacket);
        }

        public void ServerSendOtherPlanes()
        {
            var otherPlanesPackets = new List<Net.PlanePacket>();
            foreach (var plane in Objs.Planes)
            {
                otherPlanesPackets.Add(new Net.PlanePacket(plane as Plane));
            }

            var listPacket = new Net.PlaneListPacket(otherPlanesPackets);
            listPacket.Type = PacketTypes.GetOtherPlanes;

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
                            netMissile.Target = netMissileTarget;
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

                var target = Objs.GetObjectByID(packet.ID) as Plane;

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


                    if (!IsServer)
                    {
                        AddExplosion(impactPoint);

                        // Player hit by enemy.
                        if (target.ID.Equals(PlayerPlane.ID))
                        {
                            ScreenShakeCallback();
                            ScreenFlashCallback(D2DColor.Red);
                        }

                        // Player hit an enemy.
                        if (packet.DoesDamage && impactor.Owner.ID.Equals(PlayerPlane.ID))
                        {
                            ScreenFlashCallback(D2DColor.Green);
                        }
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
            //var owner = Objs.GetObjectByID(bulletPacket.OwnerID);

            // TODO: How to handle bullets that arrive before owner plane has been added?
            if (owner == null)
                return;

            bullet.Owner = owner;
            bullet.ClientCreateTime = bulletPacket.FrameTime;
            bullet.LagAmount = World.CurrentTime() - bulletPacket.FrameTime;

            // Try to spawn the bullet ahead to compensate for latency?
            bullet.Position += bullet.Velocity * (float)(bullet.LagAmount / 1000f);

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


        private void AddExplosion(D2DPoint pos)
        {
            var explosion = new Explosion(pos, 200f, 1.4f);
            Objs.AddExplosion(explosion);
        }


        private Plane GetNetPlane(GameID id, bool netOnly = true)
        {
            foreach (var plane in Objs.Planes)
            {
                if (netOnly && !plane.IsNetObject)
                    continue;

                if (plane.ID.Equals(id))
                    return plane as Plane;
            }

            return null;
        }
    }
}
