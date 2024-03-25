using PolyPlane.Net;

namespace PolyPlane.GameObjects.Manager
{
    public class CollisionManager
    {
        private GameObjectManager _objs;
        private NetObjectManager _netMan;
        //private NetPlayHost _playHost;


        public CollisionManager(GameObjectManager objs, NetObjectManager netMan)
        {
            _objs = objs;
            _netMan = netMan;
        }


        public void DoCollisions()
        {
            if (!_netMan.IsServer)
            {
                HandleGroundImpacts();
                return;
            }

            const float LAG_COMP_FACT = 1f;
            var now = World.CurrentTime();

            // Targets/AI Planes vs missiles and bullets.
            for (int r = 0; r < _objs.Planes.Count; r++)
            {
                var plane = _objs.Planes[r] as Plane;
                var planeRTT = _netMan.Host.GetPlayerRTT(plane.PlayerID);

                if (plane == null)
                    continue;

                // Missiles
                for (int m = 0; m < _objs.Missiles.Count; m++)
                {
                    var missile = _objs.Missiles[m] as Missile;

                    if (missile.Owner.ID.Equals(plane.ID))
                        continue;

                    if (missile.IsExpired)
                        continue;

                    var missileRTT = _netMan.Host.GetPlayerRTT(missile.PlayerID);

                    if (plane.CollidesWithNet(missile, out D2DPoint pos, out GameObjectPacket? histState, now - ((planeRTT + missile.LagAmount + missileRTT) * LAG_COMP_FACT)))
                    {
                        if (histState != null)
                        {
                            var ogState = new GameObjectPacket(plane);

                            plane.Position = histState.Position.ToD2DPoint();
                            plane.Velocity = histState.Velocity.ToD2DPoint();
                            plane.Rotation = histState.Rotation;
                            plane.SyncFixtures();

                            var impactResultM = plane.GetImpactResult(missile, pos);
                            _netMan.SendNetImpact(missile, plane, impactResultM, histState);

                            plane.Position = ogState.Position.ToD2DPoint();
                            plane.Velocity = ogState.Velocity.ToD2DPoint();
                            plane.Rotation = ogState.Rotation;
                            plane.SyncFixtures();
                        }
                        else
                        {
                            var impactResultM = plane.GetImpactResult(missile, pos);
                            _netMan.SendNetImpact(missile, plane, impactResultM, histState);
                        }

                        missile.IsExpired = true;
                        _objs.AddExplosion(pos);
                    }
                }

                // Bullets
                for (int b = 0; b < _objs.Bullets.Count; b++)
                {
                    var bullet = _objs.Bullets[b] as Bullet;

                    if (bullet.IsExpired)
                        continue;

                    if (bullet.Owner.ID.Equals(plane.ID))
                        continue;

                    var bulletRTT = _netMan.Host.GetPlayerRTT(bullet.PlayerID);

                    if (plane.CollidesWithNet(bullet, out D2DPoint pos, out GameObjectPacket? histState, now - ((planeRTT + bullet.LagAmount + bulletRTT) * LAG_COMP_FACT)))
                    {
                        if (!plane.IsExpired)
                            _objs.AddExplosion(pos);

                        if (histState != null)
                        {
                            var ogState = new GameObjectPacket(plane);

                            plane.Position = histState.Position.ToD2DPoint();
                            plane.Velocity = histState.Velocity.ToD2DPoint();
                            plane.Rotation = histState.Rotation;
                            plane.SyncFixtures();

                            var impactResult = plane.GetImpactResult(bullet, pos);
                            _netMan.SendNetImpact(bullet, plane, impactResult, histState);

                            plane.Position = ogState.Position.ToD2DPoint();
                            plane.Velocity = ogState.Velocity.ToD2DPoint();
                            plane.Rotation = ogState.Rotation;
                            plane.SyncFixtures();
                        }
                        else
                        {
                            var impactResult = plane.GetImpactResult(bullet, pos);
                            _netMan.SendNetImpact(bullet, plane, impactResult, histState);

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
            for (int m = 0; m < _objs.Missiles.Count; m++)
            {
                var missile = _objs.Missiles[m] as Missile;

                if (missile.IsExpired)
                    continue;

                for (int b = 0; b < _objs.Bullets.Count; b++)
                {
                    var bullet = _objs.Bullets[b] as Bullet;

                    if (bullet.IsExpired)
                        continue;

                    if (bullet.Owner == missile.Owner)
                        continue;

                    if (missile.CollidesWith(bullet, out D2DPoint posb))
                    {
                        if (!missile.IsExpired)
                            _objs.AddExplosion(posb);

                        missile.IsExpired = true;
                        bullet.IsExpired = true;
                    }
                }

            }


            //// Handle player plane vs bullets.
            //for (int b = 0; b < _objs.Bullets.Count; b++)
            //{
            //    var bullet = _objs.Bullets[b];

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
            for (int a = 0; a < _objs.Planes.Count; a++)
            {
                var plane = _objs.Planes[a];

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

    }
}
