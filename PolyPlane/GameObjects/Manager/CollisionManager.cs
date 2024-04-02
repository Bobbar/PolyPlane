using PolyPlane.Net;

namespace PolyPlane.GameObjects.Manager
{
    public class CollisionManager : IImpactEvent
    {
        private GameObjectManager _objs;
        private NetEventManager _netMan;

        private bool _isNetGame = true;

        public event EventHandler<ImpactEvent> ImpactEvent;

        public CollisionManager(GameObjectManager objs, NetEventManager netMan)
        {
            _objs = objs;
            _netMan = netMan;
        }

        public CollisionManager(GameObjectManager objs)
        {
            _objs = objs;
            _isNetGame = false;
        }

        public void DoCollisions()
        {
            if (_netMan != null && !_netMan.IsServer)
            {
                HandleGroundImpacts();
                return;
            }

            const float LAG_COMP_OFFSET = 60f;

            var now = World.CurrentTime();

            // Targets/AI Planes vs missiles and bullets.
            for (int r = 0; r < _objs.Planes.Count; r++)
            {
                var plane = _objs.Planes[r] as Plane;

                if (plane == null)
                    continue;

                uint planeRTT = 0;

                if (_isNetGame)
                    planeRTT = _netMan.Host.GetPlayerRTT(plane.PlayerID);

                // Missiles
                for (int m = 0; m < _objs.Missiles.Count; m++)
                {
                    var missile = _objs.Missiles[m] as Missile;
                    var missileOwner = missile.Owner as Plane;

                    if (missile.Owner.ID.Equals(plane.ID))
                        continue;

                    if (missile.IsExpired)
                        continue;

                    if (_isNetGame)
                    {
                        var missileRTT = _netMan.Host.GetPlayerRTT(missile.PlayerID);
                        var missileLagComp = (planeRTT + missile.LagAmount + missileRTT + LAG_COMP_OFFSET);

                        // Don't compensate as much for AI planes?
                        if (missileOwner.IsAI)
                            missileLagComp = planeRTT;

                        if (plane.CollidesWithNet(missile, out D2DPoint pos, out GameObjectPacket? histState, now - missileLagComp))
                        {
                            if (!missile.IsExpired)
                                _objs.AddExplosion(pos);


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
                        }
                    }
                    else
                    {
                        if (plane.CollidesWith(missile, out D2DPoint pos))
                        {
                            if (!missile.IsExpired)
                            {
                                _objs.AddExplosion(pos);

                                var result = plane.GetImpactResult(missile, pos);

                                //if (result.DoesDamage)
                                ImpactEvent?.Invoke(this, new ImpactEvent(plane, missile, result.DoesDamage));
                            }

                            plane.DoImpact(missile, pos);

                            missile.IsExpired = true;
                        }
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


                    if (_isNetGame)
                    {
                        uint bulletRTT = 0;

                        if (_isNetGame)
                            bulletRTT = _netMan.Host.GetPlayerRTT(bullet.PlayerID);


                        if (plane.CollidesWithNet(bullet, out D2DPoint pos, out GameObjectPacket? histState, now - (planeRTT + bullet.LagAmount + bulletRTT + LAG_COMP_OFFSET)))
                        {
                            if (!bullet.IsExpired)
                                _objs.AddBulletExplosion(pos);

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
                    }
                    else
                    {
                        if (plane.CollidesWith(bullet, out D2DPoint pos))
                        {
                            if (!bullet.IsExpired)
                            {
                                _objs.AddBulletExplosion(pos);

                                var result = plane.GetImpactResult(bullet, pos);

                                ImpactEvent?.Invoke(this, new ImpactEvent(plane, bullet, result.DoesDamage));

                                plane.DoImpact(bullet, pos);
                            }

                            bullet.IsExpired = true;
                        }
                    }

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


            HandleGroundImpacts();
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

        /// <summary>
        /// Consider distracting missiles with decoys.
        /// </summary>
        public void DoDecoySuccess()
        {
            // Test for decoy success.
            const float MIN_DECOY_FOV = 10f;
            var decoys = _objs.Decoys;

            bool groundScatter = false;

            for (int i = 0; i < _objs.Missiles.Count; i++)
            {
                var missile = _objs.Missiles[i] as GuidedMissile;
                var target = missile.Target as Plane;

                if (target == null)
                    continue;

                if (missile == null)
                    continue;

                // Decoys dont work if target is being painted.?
                //if (missile.Owner.IsObjInFOV(target, World.SENSOR_FOV * 0.25f))
                //    continue;

                GameObject maxTempObj;
                var maxTemp = 0f;
                const float MaxEngineTemp = 1800f;
                const float MaxDecoyTemp = 2000f;

                const float EngineRadius = 4f;
                const float DecoyRadius = 2f;

                var targetDist = D2DPoint.Distance(missile.Position, target.Position);
                var targetTemp = MaxEngineTemp * target.ThrustAmount * EngineRadius;
                var engineArea = 4f * (float)Math.PI * (float)Math.Pow(targetDist, 2f);
                targetTemp /= engineArea;

                maxTempObj = target;
                maxTemp = targetTemp;

                for (int k = 0; k < decoys.Count; k++)
                {
                    var decoy = decoys[k];

                    if (!missile.IsObjInFOV(decoy, MIN_DECOY_FOV))
                        continue;

                    //if (missile.Owner.IsObjInFOV(target, World.SENSOR_FOV * 0.25f) && )
                    //    continue;

                    var dist = D2DPoint.Distance(decoy.Position, missile.Position);
                    var decoyTemp = (MaxDecoyTemp * DecoyRadius) / (4f * (float)Math.PI * (float)Math.Pow(dist, 2f));

                    if (decoyTemp > maxTemp)
                    {
                        maxTemp = decoyTemp;
                        maxTempObj = decoy;
                    }

                }

                if (maxTempObj is Decoy)
                {
                    missile.DoChangeTargetChance(maxTempObj);
                }

            }
        }
    }
}
