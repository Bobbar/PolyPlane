using PolyPlane.GameObjects;
using PolyPlane.Net;
using unvell.D2DLib;

namespace PolyPlane
{
    public partial class PolyPlaneUI : Form
    {

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
            }

            SendExpiredObjects();

            now = _client.CurrentTime;

            while (_client.PacketReceiveQueue.Count > 0)
            {
                if (_client.PacketReceiveQueue.TryDequeue(out NetPacket packet))
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

                    if (!_netIDIsSet)
                        return;

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

                    _netIDIsSet = true;

                    _playerPlane.PlayerID = packet.ID.PlayerID;
                    _client.PlaneID = packet.ID.PlayerID;

                    _client.SendNewPlanePacket(_playerPlane);

                    break;
                case PacketTypes.GetNextID:
                    // Nuttin...
                    break;
                case PacketTypes.ChatMessage:
                    // Nuttin...
                    break;
                case PacketTypes.GetOtherPlanes:

                    var listPacket = packet as Net.PlaneListPacket;

                    foreach (var plane in listPacket.Planes)
                    {
                        var existing = TryIDToPlane(plane.ID);

                        if (existing == null)
                        {
                            var newPlane = new Plane(plane.Position.ToD2DPoint(), plane.PlaneColor);
                            newPlane.ID = plane.ID;
                            newPlane.IsNetObject = true;
                            newPlane.Radar = new Radar(newPlane, _hudColor, _missiles, _planes);
                            _planes.Add(newPlane);
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

                case PacketTypes.ServerSync:
                    //var syncPack = packet as Net.SyncPacket;

                    //if (syncPack != null)
                    //{
                    //    var now = DateTime.UtcNow.TimeOfDay.TotalMilliseconds;
                    //    var diff = now - syncPack.ServerTime;
                    //    World.ServerTimeOffset = -diff;
                    //}


                    break;

                case PacketTypes.PlayerReset:

                    var resetPack = packet as Net.BasicPacket;

                    var resetPlane = GetObjectById(resetPack.ID) as Plane;

                    if (resetPlane != null)
                        resetPlane.FixPlane();

                    break;
            }
        }

        private void SendPlayerReset()
        {
            var resetPacket = new BasicPacket(PacketTypes.PlayerReset, _playerPlane.ID);
            _client.EnqueuePacket(resetPacket);
        }

        private void SendExpiredObjects()
        {
            var expiredObjPacket = new Net.BasicListPacket();
            _expiredObjects.ForEach(o => expiredObjPacket.Packets.Add(new BasicPacket(PacketTypes.ExpiredObjects, o.ID)));

            if (expiredObjPacket.Packets.Count == 0)
                return;

            _client.EnqueuePacket(expiredObjPacket);

            _expiredObjects.Clear();
        }

        private void SendPlaneUpdates()
        {
            var newPlanesPacket = new Net.PlaneListPacket();

            var planePacket = new Net.PlanePacket(_playerPlane);
            newPlanesPacket.Planes.Add(planePacket);

            _client.EnqueuePacket(newPlanesPacket);

        }

        private void SendMissileUpdates()
        {
            var newMissilesPacket = new Net.MissileListPacket();
            var missiles = _missiles.Where(m => m.PlayerID == _playerPlane.PlayerID).ToList();
            missiles.ForEach(m => newMissilesPacket.Missiles.Add(new MissilePacket(m as GuidedMissile)));

            if (newMissilesPacket.Missiles.Count > 0)
                _client.EnqueuePacket(newMissilesPacket);
        }

        private void DoNetPlaneUpdates(PlaneListPacket listPacket)
        {
            if (!_netIDIsSet)
                return;

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
            bullet.ClientCreateTime = bulletPacket.FrameTime;
            bullet.LagAmount = World.CurrentTime() - bulletPacket.FrameTime;
            //var age = World.CurrentTime() - bulletPacket.FrameTime;

            // Try to spawn the bullet ahead to compensate for latency?
            bullet.Position += bullet.Velocity * (float)(bullet.LagAmount / 1000f);
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
                if (missileOwner.ID.Equals(_playerPlane.ID))
                    return;

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
                }
            }
        }

        private void SendNetImpact(GameObject impactor, GameObject target, PlaneImpactResult result)
        {
            var impactPacket = new Net.ImpactPacket(target, impactor.ID, result.ImpactPoint, result.DoesDamage, result.WasHeadshot, result.Type == ImpactType.Missile);

            _client.EnqueuePacket(impactPacket);
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

                    if (target.ID.Equals(_playerPlane.ID))
                    {
                        DoScreenShake();
                        DoScreenFlash(D2DColor.Red);
                    }

                    if (packet.DoesDamage && impactor.Owner.ID.Equals(_playerPlane.ID))
                    {
                        DoScreenFlash(D2DColor.Green);
                    }

                }
            }
        }



        private void DoNetDecoy(Decoy decoy)
        {
            var decoyPacket = new Net.DecoyPacket(decoy);
            _client.EnqueuePacket(decoyPacket);
        }
    }
}
