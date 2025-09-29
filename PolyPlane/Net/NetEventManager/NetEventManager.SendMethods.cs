using PolyPlane.GameObjects;
using PolyPlane.GameObjects.Managers;

namespace PolyPlane.Net
{
    public partial class NetEventManager
    {
        private void SendPlaneUpdates()
        {
            if (IsServer)
            {
                var planeListPacket = new PlaneListPacket();
                var planeStatusListPacket = new PlaneStatusListPacket();

                foreach (var plane in _objs.Planes)
                {
                    // Don't send updates for human planes.
                    // Those packets are already re-broadcast by the net host.
                    if (plane.IsAI)
                    {
                        var planePacket = new PlanePacket(plane, PacketTypes.PlaneUpdate);
                        planeListPacket.Planes.Add(planePacket);
                    }

                    var statusPacket = new PlaneStatusPacket(plane);
                    planeStatusListPacket.Planes.Add(statusPacket);

                    // Batch the list packet as needed.
                    if (planeListPacket.Planes.Count >= LIST_PACKET_BATCH_COUNT || planeStatusListPacket.Planes.Count >= LIST_PACKET_BATCH_COUNT)
                    {
                        Host.EnqueuePacket(planeListPacket, SendType.ToAll);
                        Host.EnqueuePacket(planeStatusListPacket, SendType.ToAll);

                        planeListPacket = new PlaneListPacket();
                        planeStatusListPacket = new PlaneStatusListPacket();
                    }
                }

                if (planeListPacket.Planes.Count > 0)
                    Host.EnqueuePacket(planeListPacket, SendType.ToAll);

                if (planeStatusListPacket.Planes.Count > 0)
                    Host.EnqueuePacket(planeStatusListPacket, SendType.ToAll);
            }
            else
            {
                var planePacket = new PlanePacket(PlayerPlane, PacketTypes.PlaneUpdate);
                Host.EnqueuePacket(planePacket, SendType.ToAllExcept, PlayerPlane.PlayerID);
            }
        }

        private void SendMissileUpdates()
        {
            var missileListPacket = new MissileListPacket(PacketTypes.MissileUpdateList);

            IEnumerable<GameObject> missiles;

            if (IsServer)
                missiles = _objs.Missiles.Where(m => m.IsNetObject == false);
            else
                missiles = _objs.Missiles.Where(m => m.PlayerID == PlayerPlane.PlayerID);

            foreach (var missile in missiles)
            {
                var missilePacket = new MissilePacket(missile as GuidedMissile, PacketTypes.MissileUpdate);
                missileListPacket.Missiles.Add(missilePacket);

                // Batch the list packet as needed.
                if (missileListPacket.Missiles.Count >= LIST_PACKET_BATCH_COUNT)
                {
                    Enqueue(missileListPacket);

                    missileListPacket = new MissileListPacket(PacketTypes.MissileUpdateList);
                }
            }

            if (missileListPacket.Missiles.Count > 0)
                Enqueue(missileListPacket);


            void Enqueue(NetPacket packet)
            {
                if (IsServer)
                    Host.EnqueuePacket(packet, SendType.ToAll);
                else
                    Host.EnqueuePacket(packet, SendType.ToAllExcept, PlayerPlane.PlayerID);
            }
        }

        private void SendExpiredObjects()
        {
            var expiredObjPacket = new BasicListPacket(PacketTypes.ExpiredObjects);

            // Collect expired objects and remove them as we go.
            var expiredObjs = _objs.ExpiredObjects();
            while (expiredObjs.Count > 0)
            {
                var obj = expiredObjs[0];

                // Send all expired objects from server.
                // Clients send only their local objects.
                if (IsServer || !IsServer && !obj.IsNetObject)
                {
                    var packet = new BasicPacket(PacketTypes.ExpiredObjects, obj.ID, obj.Position);
                    expiredObjPacket.Packets.Add(packet);

                    // Batch the list packet as needed.
                    if (expiredObjPacket.Packets.Count >= LIST_PACKET_BATCH_COUNT)
                    {
                        Host.EnqueuePacket(expiredObjPacket);

                        expiredObjPacket = new BasicListPacket(PacketTypes.ExpiredObjects);
                    }
                }

                expiredObjs.RemoveAt(0);
            }

            if (expiredObjPacket.Packets.Count > 0)
                Host.EnqueuePacket(expiredObjPacket);
        }

        public void SendNewBulletPacket(Bullet bullet)
        {
            var bulletPacket = new GameObjectPacket(bullet, PacketTypes.NewBullet);

            if (IsServer)
                Host.EnqueuePacket(bulletPacket);
            else
                Host.EnqueuePacket(bulletPacket, SendType.ToAllExcept, bullet.PlayerID);
        }

        public void SendNewMissilePacket(GuidedMissile missile)
        {
            var missilePacket = new MissilePacket(missile, PacketTypes.NewMissile);

            if (IsServer)
                Host.EnqueuePacket(missilePacket);
            else
                Host.EnqueuePacket(missilePacket, SendType.ToAllExcept, missile.PlayerID);
        }

        public void SendNewDecoyPacket(Decoy decoy)
        {
            var decoyPacket = new GameObjectPacket(decoy, PacketTypes.NewDecoy);

            if (IsServer)
                Host.EnqueuePacket(decoyPacket);
            else
                Host.EnqueuePacket(decoyPacket, SendType.ToAllExcept, decoy.PlayerID);
        }

        public void ClientSendSyncRequest()
        {
            var syncPacket = new SyncPacket(World.CurrentTimeTicks(), isResponse: false);
            Host.EnqueuePacket(syncPacket);
        }

        public void ServerSendSyncResponse(SyncPacket requestPacket)
        {
            var syncResponse = new SyncPacket(requestPacket.ClientTime, isResponse: true);
            syncResponse.ServerTime = World.CurrentTimeTicks();

            Host.EnqueuePacket(syncResponse, SendType.ToOnly, requestPacket.PeerID);
        }

        public void ServerSendGameState()
        {
            var gameStatePacket = new GameStatePacket(World.TimeOfDay, World.TimeOfDayDir, World.GunsOnly, World.IsPaused, World.GameSpeed);
            Host.EnqueuePacket(gameStatePacket);
        }

        public void ServerSendPeerSyncRequests()
        {
            foreach (var peerID in _peersNeedingSync)
            {
                var syncReq = new SyncPacket(0, isResponse: false);
                Host.EnqueuePacket(syncReq, SendType.ToOnly, peerID);
            }

            _peersNeedingSync.Clear();
        }

        public void SendNewChatPacket(string message, string playerName)
        {
            var chatPacket = new ChatPacket(message.Trim(), playerName);
            Host.EnqueuePacket(chatPacket);
        }

        public void ServerSendOtherPlanes()
        {
            var otherPlanesPackets = new PlayerListPacket(PacketTypes.GetOtherPlanes);

            foreach (var plane in _objs.Planes)
            {
                otherPlanesPackets.Players.Add(new NewPlayerPacket(plane));

                // Batch the list packet as needed.
                if (otherPlanesPackets.Players.Count >= LIST_PACKET_BATCH_COUNT)
                {
                    Host.EnqueuePacket(otherPlanesPackets);

                    otherPlanesPackets = new PlayerListPacket(PacketTypes.GetOtherPlanes);
                }
            }

            if (otherPlanesPackets.Players.Count > 0)
                Host.EnqueuePacket(otherPlanesPackets);
        }

        public void ServerSendExistingBullets(GameID destID)
        {
            var bulletsPacket = new GameObjectListPacket(PacketTypes.BulletList);

            foreach (var bullet in _objs.Bullets)
            {
                bulletsPacket.Packets.Add(new GameObjectPacket(bullet, PacketTypes.NewBullet));

                // Batch the list packet as needed.
                if (bulletsPacket.Packets.Count >= LIST_PACKET_BATCH_COUNT)
                {
                    Host.EnqueuePacket(bulletsPacket, SendType.ToOnly, destID.PlayerID);

                    bulletsPacket = new GameObjectListPacket(PacketTypes.BulletList);
                }
            }

            if (bulletsPacket.Packets.Count > 0)
                Host.EnqueuePacket(bulletsPacket, SendType.ToOnly, destID.PlayerID);
        }

        public void ServerSendExistingMissiles(GameID destID)
        {
            var missileListPacket = new MissileListPacket(PacketTypes.MissileList);

            foreach (var missile in _objs.Missiles)
            {
                missileListPacket.Missiles.Add(new MissilePacket(missile as GuidedMissile, PacketTypes.NewMissile));

                // Batch the list packet as needed.
                if (missileListPacket.Missiles.Count >= LIST_PACKET_BATCH_COUNT)
                {
                    Host.EnqueuePacket(missileListPacket, SendType.ToOnly, destID.PlayerID);

                    missileListPacket = new MissileListPacket(PacketTypes.MissileList);
                }
            }

            if (missileListPacket.Missiles.Count > 0)
                Host.EnqueuePacket(missileListPacket, SendType.ToOnly, destID.PlayerID);
        }

        public void ServerSendExistingDecoys(GameID destID)
        {
            var decoysPacket = new GameObjectListPacket(PacketTypes.DecoyList);

            foreach (var decoy in _objs.Decoys)
            {
                decoysPacket.Packets.Add(new GameObjectPacket(decoy, PacketTypes.NewDecoy));

                // Batch the list packet as needed.
                if (decoysPacket.Packets.Count >= LIST_PACKET_BATCH_COUNT)
                {
                    Host.EnqueuePacket(decoysPacket, SendType.ToOnly, destID.PlayerID);

                    decoysPacket = new GameObjectListPacket(PacketTypes.DecoyList);
                }
            }

            if (decoysPacket.Packets.Count > 0)
                Host.EnqueuePacket(decoysPacket, SendType.ToOnly, destID.PlayerID);
        }

        public void ServerSendExistingImpacts(GameID destID)
        {
            var impactsPacket = new ImpactListPacket(destID);

            foreach (var impactList in _impacts.Values)
            {
                foreach (var impact in impactList)
                {
                    impact.ImpactType |= ImpactType.Existing;

                    impactsPacket.Impacts.Add(impact);

                    // Batch the list packet as needed.
                    if (impactsPacket.Impacts.Count >= LIST_PACKET_BATCH_COUNT)
                    {
                        Host.EnqueuePacket(impactsPacket, SendType.ToOnly, destID.PlayerID);

                        impactsPacket = new ImpactListPacket(destID);
                    }
                }
            }

            if (impactsPacket.Impacts.Count > 0)
                Host.EnqueuePacket(impactsPacket, SendType.ToOnly, destID.PlayerID);
        }

        public void ClientSendPlaneReset(FighterPlane plane)
        {
            if (IsServer)
                return;

            var resetPacket = new BasicPacket(PacketTypes.PlayerReset, plane.ID);
            ClearImpacts(plane.PlayerID);
            Host.EnqueuePacket(resetPacket);
        }

        public void ServerSendPlaneReset(FighterPlane plane, D2DPoint spawnPos)
        {
            if (!IsServer)
                return;

            var resetPacket = new BasicPacket(PacketTypes.PlayerReset, plane.ID, spawnPos);
            ClearImpacts(plane.PlayerID);
            Host.EnqueuePacket(resetPacket);
        }

        public void SendNetImpact(PlaneImpactResult result)
        {
            var impactor = result.ImpactorObject;
            var target = result.TargetPlane;

            var impactPacket = new ImpactPacket(target, impactor.ID, result);
            impactPacket.OwnerID = impactor.Owner.ID;

            SaveImpact(impactPacket);

            Host.EnqueuePacket(impactPacket);
        }

        public void SendPlayerDisconnectPacket(uint playerID)
        {
            var packet = new BasicPacket(PacketTypes.PlayerDisconnect, new GameID(playerID));
            Host.EnqueuePacket(packet);
        }
    }
}
