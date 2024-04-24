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

            const float LAG_COMP_OFFSET = 40f;

            var now = World.CurrentTime();

            // Targets/AI Planes vs missiles and bullets.
            for (int r = 0; r < _objs.Planes.Count; r++)
            {
                var plane = _objs.Planes[r] as FighterPlane;

                if (plane == null)
                    continue;

                uint planeRTT = 0;

                if (_isNetGame)
                    planeRTT = _netMan.Host.GetPlayerRTT(plane.PlayerID);

                var nearObjs = _objs.GetNear(plane);

                foreach (var obj in nearObjs)
                {
                    if (obj is Missile missile)
                    {
                        var missileOwner = missile.Owner as FighterPlane;

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
                                    plane.Rotation = histState.Rotation;
                                    plane.SyncFixtures();

                                    var impactResultM = plane.GetImpactResult(missile, pos);
                                    _netMan.SendNetImpact(missile, plane, impactResultM, histState);

                                    plane.Position = ogState.Position.ToD2DPoint();
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

                    if (obj is Bullet bullet)
                    {
                        var bulletOwner = bullet.Owner as FighterPlane;

                        if (bullet.IsExpired)
                            continue;

                        if (bullet.Owner.ID.Equals(plane.ID))
                            continue;


                        if (_isNetGame)
                        {
                            uint bulletRTT = 0;

                            if (_isNetGame)
                                bulletRTT = _netMan.Host.GetPlayerRTT(bullet.PlayerID);

                            var bulletLagComp = planeRTT + bullet.LagAmount + bulletRTT + LAG_COMP_OFFSET;

                            if (bulletOwner.IsAI)
                                bulletLagComp = planeRTT;

                            if (plane.CollidesWithNet(bullet, out D2DPoint pos, out GameObjectPacket? histState, now - bulletLagComp))
                            {
                                if (!bullet.IsExpired)
                                    _objs.AddBulletExplosion(pos);

                                if (histState != null)
                                {
                                    var ogState = new GameObjectPacket(plane);

                                    plane.Position = histState.Position.ToD2DPoint();
                                    plane.Rotation = histState.Rotation;
                                    plane.SyncFixtures();

                                    var impactResult = plane.GetImpactResult(bullet, pos);
                                    _netMan.SendNetImpact(bullet, plane, impactResult, histState);

                                    plane.Position = ogState.Position.ToD2DPoint();
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
                }
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
            // Planes.
            for (int a = 0; a < _objs.Planes.Count; a++)
            {
                var plane = _objs.Planes[a];

                if (plane.Altitude <= 0f && !plane.InResetCooldown)
                {
                    if (!plane.HasCrashed)
                    {
                        var pointingRight = Helpers.IsPointingRight(plane.Rotation);
                        if (pointingRight)
                            plane.Rotation = 0f;
                        else
                            plane.Rotation = 180f;

                        plane.DoHitGround();
                    }

                    plane.Velocity *= new D2DPoint(0.998f, 0f);
                    plane.Position = new D2DPoint(plane.Position.X, 0f);
                    plane.RotationSpeed = 0f;
                }
            }

            // Bullets & missiles.
            if (!World.IsNetGame || (World.IsNetGame && !World.IsServer))
            {
                foreach (var bullet in _objs.Bullets)
                {
                    if (bullet.Altitude <= 0f && !bullet.IsExpired)
                    {
                        bullet.IsExpired = true;

                        _objs.AddBulletExplosion(bullet.Position);
                    }
                }
            }

            foreach (var missile in _objs.Missiles)
            {
                if (missile.Altitude <= 0f && !missile.IsExpired)
                {
                    missile.IsExpired = true;

                    _objs.AddExplosion(missile.Position);
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
                var target = missile.Target as FighterPlane;

                if (target == null)
                    continue;

                if (missile == null)
                    continue;

                // No sense in trying to control missiles we don't have control of...
                if (missile.IsNetObject)
                    continue;

                // Decoys dont work if target is being painted.?
                //if (missile.Owner.IsObjInFOV(target, World.SENSOR_FOV * 0.25f))
                //    continue;

                GameObject maxTempObj;
                var maxTemp = 0f;
                const float MaxEngineTemp = 1800f;
                const float MaxDecoyTemp = 3000f;

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

                HandleGroundScatter(missile);
            }
        }

        private void HandleGroundScatter(GuidedMissile missile)
        {
            const float GROUND_SCATTER_ALT = 3000f;

            if (missile.IsNetObject)
                return;

            if (missile.Guidance == null)
                return;

            if (missile.Target != null)
            {
                if (missile.Target.Altitude <= GROUND_SCATTER_ALT)
                {
                    const int CHANCE_INIT = 10;
                    var chance = CHANCE_INIT;

                    var altFact = 1f - Helpers.Factor(missile.Target.Altitude, GROUND_SCATTER_ALT);

                    chance -= (int)(altFact * 5);

                    if (!missile.Guidance.GroundScatterInCooldown)
                    {
                        var rnd1 = Helpers.Rnd.Next(chance);
                        var rnd2 = Helpers.Rnd.Next(chance);
                        if (rnd1 == rnd2)
                        {
                            missile.Guidance.LostInGround = true;
                            Log.Msg("Lost in ground scatter....");
                        }
                    }
                }
                else
                {
                    missile.Guidance.LostInGround = false;
                }
            }
        }
    }
}
