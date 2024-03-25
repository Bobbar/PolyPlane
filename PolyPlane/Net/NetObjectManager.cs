using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PolyPlane.GameObjects;
using unvell.D2DLib;

namespace PolyPlane.Net
{
    public class NetObjectManager
    {
        public GameObjectManager _objs;
        public NetPlayHost _host;
        public bool IsServer = false;
        public Plane PlayerPlane = null;

        public Action<D2DColor> ScreenFlashCallback = null;
        public Action ScreenShakeCallback = null;


        private long _frame = 0;
        private bool _netIDIsSet = false;

        public NetObjectManager(GameObjectManager objectManager, NetPlayHost host, Plane playerPlane)
        {
            _objs = objectManager;
            _host = host;
            PlayerPlane = playerPlane;
            IsServer = false;
        }

        public NetObjectManager(GameObjectManager objectManager, NetPlayHost host)
        {
            _objs = objectManager;
            _host = host;
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

            while (_host.PacketReceiveQueue.Count > 0)
            {
                if (_host.PacketReceiveQueue.TryDequeue(out Net.NetPacket packet))
                {
                    totalPacketTime += now - packet.FrameTime;
                    numPackets++;

                    HandleNetPacket(packet);
                }
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
                            newPlane.Radar = new Radar(newPlane, D2DColor.GreenYellow, _objs.Missiles, _objs.Planes);
                            _objs.AddPlane(newPlane);
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

                        _objs.ChangeObjID(PlayerPlane, new GameID(packet.ID.PlayerID, PlayerPlane.ID.ObjectID));
                        var netPacket = new PlanePacket(PlayerPlane, PacketTypes.NewPlayer);
                        _host.EnqueuePacket(netPacket);
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
                            var existing = _objs.Contains(plane.ID);

                            if (!existing)
                            {
                                var newPlane = new Plane(plane.Position.ToD2DPoint(), plane.PlaneColor);
                                newPlane.ID = plane.ID;
                                newPlane.IsNetObject = true;
                                newPlane.Radar = new Radar(newPlane, D2DColor.GreenYellow, _objs.Missiles, _objs.Planes);
                                _objs.AddPlane(newPlane);
                            }
                        }
                    }

                    break;

                case PacketTypes.ExpiredObjects:
                    var expiredPacket = packet as Net.BasicListPacket;

                    foreach (var p in expiredPacket.Packets)
                    {
                        var obj = _objs.GetObjectByID(p.ID);

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

                    var resetPlane = _objs.GetObjectByID(resetPack.ID) as Plane;

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
                foreach (var plane in _objs.Planes)
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
                _host.EnqueuePacket(newPlanesPacket);
        }

        private void SendMissileUpdates()
        {
            var newMissilesPacket = new Net.MissileListPacket();

            if (IsServer)
            {
                _objs.Missiles.ForEach(m => newMissilesPacket.Missiles.Add(new MissilePacket(m as GuidedMissile)));


            }
            else
            {
                var missiles = _objs.Missiles.Where(m => m.PlayerID == PlayerPlane.PlayerID).ToList();
                missiles.ForEach(m => newMissilesPacket.Missiles.Add(new MissilePacket(m as GuidedMissile)));
            }

            if (newMissilesPacket.Missiles.Count > 0)
                _host.EnqueuePacket(newMissilesPacket);
        }

        private void SendExpiredObjects()
        {
            var expiredObjPacket = new Net.BasicListPacket();
            _objs.ExpiredObjects().ForEach(o => expiredObjPacket.Packets.Add(new BasicPacket(PacketTypes.ExpiredObjects, o.ID)));

            if (expiredObjPacket.Packets.Count == 0)
                return;

            _host.EnqueuePacket(expiredObjPacket);
        }

        public void ServerSendOtherPlanes()
        {
            var otherPlanesPackets = new List<Net.PlanePacket>();
            foreach (var plane in _objs.Planes)
            {
                otherPlanesPackets.Add(new Net.PlanePacket(plane as Plane));
            }

            var listPacket = new Net.PlaneListPacket(otherPlanesPackets);
            listPacket.Type = PacketTypes.GetOtherPlanes;

            _host.EnqueuePacket(listPacket);
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

            _host.EnqueuePacket(impactPacket);
            DoNetImpact(impactPacket);
        }

        public void SendNewDecoy(Decoy decoy)
        {
            var decoyPacket = new Net.DecoyPacket(decoy);

            _host.EnqueuePacket(decoyPacket);
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
                    netPlane.NetUpdate(World.DT, World.ViewPortSize, World.RenderScale, planeUpdPacket.Position.ToD2DPoint(), planeUpdPacket.Velocity.ToD2DPoint(), planeUpdPacket.Rotation, planeUpdPacket.FrameTime);
                }
            }
        }

        private void DoNetMissileUpdates(MissileListPacket listPacket)
        {
            foreach (var missileUpdate in listPacket.Missiles)
            {
                var netMissile = _objs.GetObjectByID(missileUpdate.ID) as GuidedMissile;

                if (netMissile != null)
                {
                    var netMissileOwner = _objs.GetObjectByID(netMissile.Owner.ID);

                    if (_objs.TryGetObjectByID(missileUpdate.TargetID, out GameObject netMissileTarget))
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
                var impactor = _objs.GetObjectByID(packet.ImpactorID);

                if (impactor == null)
                    return;

                //if (impactor.IsExpired)
                //    return;

                impactor.IsExpired = true;

                var target = _objs.GetObjectByID(packet.ID) as Plane;

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
            //var owner = _objs.GetObjectByID(bulletPacket.OwnerID);

            // TODO: How to handle bullets that arrive before owner plane has been added?
            if (owner == null)
                return;

            bullet.Owner = owner;
            bullet.ClientCreateTime = bulletPacket.FrameTime;
            bullet.LagAmount = World.CurrentTime() - bulletPacket.FrameTime;

            // Try to spawn the bullet ahead to compensate for latency?
            bullet.Position += bullet.Velocity * (float)(bullet.LagAmount / 1000f);

            _objs.AddBullet(bullet);
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
                _objs.EnqueueMissile(missile);
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

                bool containsDecoy = _objs.Contains(decoy.ID);

                if (!containsDecoy)
                {
                    _objs.AddDecoy(decoy);

                    if (IsServer)
                        _host.EnqueuePacket(decoyPacket);
                }
            }
        }

        private void DoPlayerDisconnected(int playerID)
        {
            var objs = _objs.GetObjectsByPlayer(playerID);

            foreach (var obj in objs)
            {
                if (obj.PlayerID == playerID)
                    obj.IsExpired = true;
            }
        }


        private void AddExplosion(D2DPoint pos)
        {
            var explosion = new Explosion(pos, 200f, 1.4f);
            _objs.AddExplosion(explosion);
        }


        private Plane GetNetPlane(GameID id, bool netOnly = true)
        {
            foreach (var plane in _objs.Planes)
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
