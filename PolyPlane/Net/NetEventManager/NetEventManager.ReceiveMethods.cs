using PolyPlane.GameObjects;
using PolyPlane.GameObjects.Manager;
using PolyPlane.Rendering;

namespace PolyPlane.Net
{
    public partial class NetEventManager
    {

        private void HandleExpiredObjects(BasicListPacket expiredObjectsPacket)
        {
            if (expiredObjectsPacket == null)
                return;

            foreach (var p in expiredObjectsPacket.Packets)
            {
                var obj = _objs.GetObjectByID(p.ID);

                if (obj != null)
                {
                    obj.Position = p.Position;
                    obj.IsExpired = true;
                }

                PruneExpiredDeferredPackets(p.ID);
            }
        }

        private void HandlePlaneStatusListUpdate(PlaneStatusListPacket statusListPacket)
        {
            if (statusListPacket == null)
                return;

            foreach (var status in statusListPacket.Planes)
            {
                HandlePlaneStatusUpdate(status);
            }
        }

        private void HandlePlaneStatusUpdate(PlaneStatusPacket statusPacket)
        {
            var plane = _objs.GetPlaneByPlayerID(statusPacket.ID.PlayerID);

            if (plane != null)
            {
                plane.IsDisabled = statusPacket.IsDisabled;
                plane.Health = statusPacket.Health;
                plane.Kills = statusPacket.Score;
                plane.Deaths = statusPacket.Deaths;
            }
            else
            {
                DeferPacket(statusPacket);
            }
        }

        private void HandlePlayerScored(PlayerScoredPacket scorePacket)
        {
            if (scorePacket == null)
                return;

            var scorePlane = _objs.GetPlaneByPlayerID(scorePacket.ID.PlayerID);
            var victimPlane = _objs.GetPlaneByPlayerID(scorePacket.VictimID.PlayerID);

            if (scorePlane != null && victimPlane != null)
            {
                scorePlane.Kills = scorePacket.Score;
                victimPlane.Deaths = scorePacket.Deaths;

                victimPlane.DoPlayerKilled(scorePlane, scorePacket.ImpactType);

                PlayerScoredEvent?.Invoke(this, new PlayerScoredEventArgs(scorePlane, victimPlane, scorePacket.WasHeadshot));
            }
            else
            {
                DeferPacket(scorePacket);
            }
        }

        public void HandlePlayerKilled(PlayerKilledEventArgs killedEvent)
        {
            var scorePlane = killedEvent.AttackPlane;
            var killedPlane = killedEvent.KilledPlane;

            if (scorePlane != null)
            {
                var scorePacket = new PlayerScoredPacket(scorePlane.ID, killedPlane.ID, scorePlane.Kills, killedPlane.Deaths, killedEvent.ImpactType);
                Host.EnqueuePacket(scorePacket);
            }
        }

        private void HandleNewImpactList(ImpactListPacket impacts, float dt)
        {
            foreach (var impact in impacts.Impacts)
            {
                HandleNetImpact(impact, dt);
            }
        }

        private void HandleNewPlayers(PlayerListPacket players)
        {
            foreach (var player in players.Players)
            {
                var existing = _objs.Contains(player.ID);

                if (!existing)
                {
                    var newPlane = new FighterPlane(player.Position, player.PlaneColor, player.ID, isAI: false, isNetPlane: true);
                    newPlane.PlayerName = player.Name;
                    newPlane.IsNetObject = true;
                    newPlane.LagAmount = player.Age;
                    newPlane.PlayerHitCallback = (evt) => ImpactEvent?.Invoke(this, evt);

                    _objs.AddPlane(newPlane);
                }
            }
        }

        private void HandleNetPlaneUpdates(PlaneListPacket listPacket)
        {
            // If we are a client and our ID has been set.
            if (!ClientIsReady())
            {
                DeferPacket(listPacket);
                return;
            }

            foreach (var planeUpdPacket in listPacket.Planes)
            {
                HandleNetPlaneUpdate(planeUpdPacket);
            }
        }

        private void HandleNetPlaneUpdate(PlanePacket planePacket)
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

        private void HandleNetMissileUpdates(MissileListPacket listPacket)
        {
            foreach (var missileUpdate in listPacket.Missiles)
            {
                HandleNetMissileUpdate(missileUpdate);
            }
        }

        private void HandleNetMissileUpdate(MissilePacket missilePacket)
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

        private void HandleNetImpact(ImpactPacket packet, float dt)
        {
            if (packet != null)
            {
                var impactor = _objs.GetObjectByID(packet.ImpactorID);
                var impactorOwner = _objs.GetObjectByID(packet.OwnerID);

                if (impactorOwner == null)
                {
                    // Defer if owner not present yet.
                    DeferPacket(packet);
                    return;
                }

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

                    target.SetPosition(packet.Position, packet.Rotation);

                    // Flip the plane poly to match the state from the impact packet.
                    bool flipped = false;

                    if (target.Polygon.IsFlipped != packet.WasFlipped)
                    {
                        target.FlipY();
                        flipped = true;
                    }

                    var result = new PlaneImpactResult(packet);
                    result.TargetPlane = target;
                    result.ImpactorObject = impactor;

                    target.HandleImpactResult(result, dt);

                    target.SetPosition(ogState.Position, ogState.Rotation);

                    if (flipped)
                        target.FlipY();
                }
            }
        }

        private void HandleNewBullet(GameObjectPacket bulletPacket, float dt)
        {
            var owner = GetNetPlane(bulletPacket.OwnerID);

            if (owner == null)
            {
                // Defer new bullets until owner is added. (Maybe)
                DeferPacket(bulletPacket);
                return;
            }

            var bullet = new Bullet(bulletPacket.Position, bulletPacket.Velocity, bulletPacket.Rotation);
            bullet.ID = bulletPacket.ID;

            bulletPacket.SyncObj(bullet);

            bullet.Owner = owner;
            bullet.LagAmount = bulletPacket.Age;

            // Try to spawn the bullet ahead (extrapolate) to compensate for latency?
            bullet.Position += bullet.Velocity * (bullet.LagAmountFrames * dt);

            _objs.EnqueueBullet(bullet);
        }

        private void HandleNewMissile(MissilePacket missilePacket)
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
                missile.ID = missilePacket.ID;
                missilePacket.SyncObj(missile);
                missile.Target = missileTarget;
                missile.LagAmount = missilePacket.Age;

                _objs.EnqueueMissile(missile);
            }
            else
            {
                DeferPacket(missilePacket);
            }
        }

        private void HandleNewDecoy(GameObjectPacket decoyPacket)
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

        private void HandlePlayerDisconnected(int playerID)
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

        private void ClientHandleSyncResponse(SyncPacket syncPacket)
        {
            // Compute server time offset.
            // Loosely based off of the NTP algo.
            // https://en.wikipedia.org/wiki/Network_Time_Protocol

            var t0 = syncPacket.ClientTime;
            var t1 = syncPacket.ServerTime;
            var t2 = syncPacket.FrameTime;
            var t3 = World.CurrentTimeTicks();

            // Compute the time difference and smooth it.
            var theta = ((t1 - t0) + (t2 - t3)) / 2d;
            var thetaSmooth = _thetaSmooth.Add(theta);

            // Add the server frame delay to the offset.
            var serverFrameTimeTicks = TimeSpan.FromMilliseconds(World.SERVER_FRAME_TIME).Ticks;
            var serverOffset = thetaSmooth + serverFrameTimeTicks;

            // Compute an offset delta.
            var offsetDelta = _offsetDeltaSmooth.Add(_serverTimeOffsetSmooth.Current - serverOffset);
            var offsetDeltaMs = TimeSpan.FromTicks((long)offsetDelta).TotalMilliseconds;

            int minAttempts = 5;

            // Do a few more attempts for new clients.
            if (!_initialOffsetSet)
                minAttempts = 10;

            // Stop the sync cycle once the deviation between offsets calms down,
            // or if we make too many attempts to zero in on the true server time.
            if ((_syncCount > minAttempts && Math.Abs(offsetDeltaMs) <= SYNC_MAX_DELTA) || _syncCount >= SYNC_MAX_ATTEMPTS)
            {
                var newOffset = _serverTimeOffsetSmooth.Current;

                // Set the new offset if we are confident in the result.
                if ((_syncCount <= SYNC_MAX_ATTEMPTS && Math.Abs(offsetDeltaMs) <= SYNC_MAX_DELTA) || _initialOffsetSet == false)
                {
                    World.ServerTimeOffset = newOffset;
                    _initialOffsetSet = true;
                }

                _syncingServerTime = false;
                _syncCount = 0;
                _serverTimeOffsetSmooth.Clear();
                _offsetDeltaSmooth.Clear();
                _thetaSmooth.Clear();
            }
            else
            {
                // Accumulate the computed offsets.
                _serverTimeOffsetSmooth.Add(serverOffset);
            }

            _syncCount++;
            _lastSyncTime = t3;
            _receivedFirstSync = true;
        }
    }
}
