using PolyPlane.GameObjects.Particles;
using PolyPlane.Helpers;
using PolyPlane.Net;

namespace PolyPlane.GameObjects.Managers
{
    public class CollisionManager
    {
        private GameObjectManager _objs = World.ObjectManager;
        private NetEventManager _netMan;
        private ActionScheduler _rateLimitedActions = new ActionScheduler();

        private bool _isNetGame = true;

        public CollisionManager(NetEventManager netMan)
        {
            _netMan = netMan;

            // Server will need to rate limit explosion impulses.
            if (World.IsServer)
                _rateLimitedActions.AddAction(World.TARGET_FRAME_TIME, () => HandleExplosionImpulse(World.CurrentDT));
        }

        public CollisionManager()
        {
            _isNetGame = false;
        }

        public void DoCollisions(float dt)
        {
            var doLocalCollisions = true;

            if (World.IsNetGame && World.IsClient)
                doLocalCollisions = false;

            var now = World.CurrentNetTimeMs();

            // Targets/AI Planes vs missiles and bullets.
            for (int r = 0; r < _objs.Planes.Count; r++)
            {
                var plane = _objs.Planes[r];

                if (plane == null)
                    continue;

                var nearObjs = _objs.GetNear(plane);

                foreach (var obj in nearObjs)
                {
                    if (doLocalCollisions)
                    {
                        if (obj is GuidedMissile missile)
                        {
                            if (missile.Owner.Equals(plane))
                                continue;

                            if (missile.IsExpired)
                                continue;

                            if (_isNetGame)
                            {
                                var missileLagComp = (long)(missile.LagAmount + World.NET_INTERP_AMOUNT);

                                if (plane.CollidesWithNet(missile, out D2DPoint pos, out GameObjectPacket? histState, now - missileLagComp, dt))
                                {
                                    if (histState != null)
                                    {
                                        var ogState = new GameObjectPacket(plane);

                                        plane.SetPosition(histState.Position, histState.Rotation);

                                        var impactResultM = plane.GetImpactResult(missile, pos);
                                        plane.HandleImpactResult(impactResultM, dt);
                                        _netMan.SendNetImpact(impactResultM);

                                        plane.SetPosition(ogState.Position, ogState.Rotation);
                                    }
                                    else
                                    {
                                        var impactResultM = plane.GetImpactResult(missile, pos);
                                        plane.HandleImpactResult(impactResultM, dt);
                                        _netMan.SendNetImpact(impactResultM);
                                    }

                                    missile.IsExpired = true;
                                }
                            }
                            else
                            {
                                if (plane.CollidesWith(missile, out D2DPoint pos, dt))
                                {
                                    if (!missile.IsExpired)
                                    {
                                        var result = plane.GetImpactResult(missile, pos);

                                        plane.HandleImpactResult(result, dt);

                                        missile.Position = pos;
                                        missile.IsExpired = true;
                                    }
                                }
                            }
                        }
                        else if (obj is Bullet bullet)
                        {
                            if (bullet.IsExpired)
                                continue;

                            if (bullet.Owner.Equals(plane))
                                continue;

                            if (_isNetGame)
                            {
                                var bulletLagComp = (long)(bullet.LagAmount + World.NET_INTERP_AMOUNT);

                                if (plane.CollidesWithNet(bullet, out D2DPoint pos, out GameObjectPacket? histState, now - bulletLagComp, dt))
                                {
                                    if (histState != null)
                                    {
                                        var ogState = new GameObjectPacket(plane);

                                        plane.SetPosition(histState.Position, histState.Rotation);

                                        var impactResult = plane.GetImpactResult(bullet, pos);
                                        plane.HandleImpactResult(impactResult, dt);
                                        _netMan.SendNetImpact(impactResult);

                                        plane.SetPosition(ogState.Position, ogState.Rotation);
                                    }
                                    else
                                    {
                                        var impactResult = plane.GetImpactResult(bullet, pos);
                                        plane.HandleImpactResult(impactResult, dt);
                                        _netMan.SendNetImpact(impactResult);
                                    }

                                    bullet.IsExpired = true;
                                }
                            }
                            else
                            {
                                if (plane.CollidesWith(bullet, out D2DPoint pos, dt))
                                {
                                    if (!bullet.IsExpired)
                                    {
                                        var result = plane.GetImpactResult(bullet, pos);

                                        plane.HandleImpactResult(result, dt);

                                        bullet.Position = pos;
                                        bullet.IsExpired = true;
                                    }
                                }
                            }
                        }
                    }

                    // Do plane particle pushes.
                    DoParticleImpulse(plane, obj, dt);
                }
            }

            // Handle missiles hit by bullets and missiles hitting decoys.
            for (int m = 0; m < _objs.Missiles.Count; m++)
            {
                var missile = _objs.Missiles[m] as GuidedMissile;

                if (missile.IsExpired)
                    continue;

                var nearObjs = _objs.GetNear(missile);

                foreach (var obj in nearObjs)
                {
                    if (doLocalCollisions)
                    {
                        if (obj is Bullet bullet)
                        {
                            if (bullet.IsExpired)
                                continue;

                            if (bullet.Owner.Equals(missile.Owner))
                                continue;

                            if (_isNetGame)
                            {
                                var bulletLagComp = (long)(bullet.LagAmount + World.NET_INTERP_AMOUNT);

                                if (missile.CollidesWithNet(bullet, out D2DPoint pos, out GameObjectPacket? histState, now - bulletLagComp, dt))
                                {
                                    if (histState != null)
                                    {
                                        missile.SetPosition(histState.Position, histState.Rotation);
                                    }

                                    missile.IsExpired = true;
                                    bullet.IsExpired = true;
                                }
                            }
                            else
                            {
                                if (missile.CollidesWith(bullet, out D2DPoint posb, dt))
                                {
                                    missile.IsExpired = true;
                                    bullet.IsExpired = true;
                                }
                            }
                        }
                        else if (obj is Decoy decoy)
                        {
                            if (decoy.IsExpired)
                                continue;

                            if (missile.CollidesWith(decoy, out D2DPoint posb, dt))
                            {
                                missile.IsExpired = true;
                                decoy.IsExpired = true;
                            }
                        }
                    }

                    // Do missile particle pushes.
                    DoParticleImpulse(missile, obj, dt);
                }
            }

            // Handle bullet particle pushes.
            for (int b = 0; b < _objs.Bullets.Count; b++)
            {
                var bullet = _objs.Bullets[b];
                var nearObjs = _objs.GetNear(bullet);

                foreach (var obj in nearObjs)
                {
                    DoParticleImpulse(bullet, obj, dt);
                }
            }

            // Since the server runs at a higher FPS than clients,
            // we want to limit the rate of explosion impulses.
            // Otherwise we may be sending dozens of packets per second
            // for explosion splash damage.
            if (World.IsServer)
                _rateLimitedActions.DoActions();
            else
                HandleExplosionImpulse(dt);

            HandleGroundImpacts(dt);
            HandleFieldWrap();
        }

        private void DoParticleImpulse(GameObject pushObject, GameObject particleObject, float dt)
        {
            const float EFFECT_DIST_PLANE = 90f;
            const float EFFECT_DIST_MISSILE = 55f;
            const float EFFECT_DIST_BULLET = 15f;

            const float EFFECT_DIST_GROW = 20f;
            const float EFFECT_GROW_VELO = 800f;

            const float MIN_EFFECT_AGE = 2f;
            const float FORCE = 8000f;
            const float VELO_FACTOR = 50f;
            const float MIN_VELO = 10f;

            if (!particleObject.HasFlag(GameObjectFlags.AeroPushable))
                return;

            var pushVelo = pushObject.Velocity.Length();

            // Skip impulse when velo is low.
            if (pushVelo < MIN_VELO)
                return;

            // Increase effect distance with velo.            
            var effectDist = EFFECT_DIST_PLANE;

            if (pushObject is GuidedMissile)
                effectDist = EFFECT_DIST_MISSILE;
            else if (pushObject is Bullet)
                effectDist = EFFECT_DIST_BULLET;

            var veloFact = Utilities.Factor(pushVelo, EFFECT_GROW_VELO);
            effectDist += EFFECT_DIST_GROW * veloFact;

            var dist = pushObject.Position.DistanceTo(particleObject.Position) + float.Epsilon;

            // Skip if outside effect dist.
            if (dist > effectDist)
                return;

            var forceFact = 1f - Utilities.FactorWithEasing(dist, effectDist, EasingFunctions.In.EaseSine);

            float ageFact = 1f;

            // Ease in impulse for particles spawned by the pusher object.
            if (particleObject.Owner != null && particleObject.Owner.Equals(pushObject))
                ageFact = Utilities.FactorWithEasing(particleObject.Age, MIN_EFFECT_AGE, EasingFunctions.In.EaseQuintic);

            var dir = (particleObject.Position - pushObject.Position);
            var dirNorm = dir.Normalized();
            var forceVec = dirNorm * (FORCE * forceFact);
            var vdiff = (pushObject.Velocity - particleObject.Velocity).Length();

            // Wake sleeping particles.
            particleObject.IsAwake = true;

            if (vdiff > 0f)
            {
                // Add some velo from the pusher object.
                particleObject.Velocity += (((pushObject.Velocity * VELO_FACTOR) / particleObject.Mass * dt) * forceFact * ageFact * veloFact);
            }

            // Push particles away.
            particleObject.Velocity += (forceVec / particleObject.Mass * dt) * ageFact * veloFact;
        }

        private void HandleExplosionImpulse(float dt)
        {
            const float FORCE = 50000f;
            const float DAMAGE_AMT = 25f;

            for (int i = 0; i < _objs.Explosions.Count; i++)
            {
                var explosion = _objs.Explosions[i] as Explosion;

                if (explosion.IsExpired || explosion.Age > explosion.Duration)
                    continue;

                var nearObjs = _objs.GetNear(explosion);

                foreach (var obj in nearObjs)
                {
                    // TODO: How do we handle net objects?
                    if (World.IsClient && obj.IsNetObject)
                        continue;

                    var dist = explosion.Position.DistanceTo(obj.Position) + float.Epsilon;
                    var effectRadius = explosion.Radius * 1.2f;

                    if (dist <= effectRadius)
                    {
                        var forceFact = 1f - Utilities.FactorWithEasing(dist, effectRadius, EasingFunctions.Out.EaseCircle) + 0.1f;

                        if (obj is FighterPlane plane && explosion.Owner is GuidedMissile missile)
                        {
                            if (!missile.Owner.Equals(plane) && !plane.IsDisabled)
                            {
                                // Apply a small amount of damage to planes within the blast radius of the explosion.
                                var damageAmount = (forceFact * DAMAGE_AMT) * dt;

                                var impactResult = new PlaneImpactResult(ImpactType.Splash, plane.Position, 0f, damageAmount, wasHeadshot: false);
                                impactResult.TargetPlane = plane;
                                impactResult.ImpactorObject = missile;

                                plane.HandleImpactResult(impactResult, dt);

                                if (World.IsNetGame && World.IsServer)
                                    _netMan.SendNetImpact(impactResult);

                            }
                        }
                        else if (obj is GuidedMissile detMissile)
                        {
                            // Detonate any other missiles within the blast radius.
                            if (!detMissile.IsExpired)
                                detMissile.IsExpired = true;
                        }

                        // Impart an impulse on other nearby pushable objects.
                        if (obj.HasFlag(GameObjectFlags.ExplosionImpulse))
                        {
                            var dir = (obj.Position - explosion.Position);
                            var dirNorm = dir.Normalized();
                            var forceVec = dirNorm * (FORCE * forceFact);
                            obj.Velocity += forceVec / obj.Mass * dt;

                            if (!obj.IsAwake)
                                obj.IsAwake = true;
                        }
                    }
                }
            }
        }

        private void HandleGroundImpacts(float dt)
        {
            // Impact bullets & missiles just below ground level.
            // Lets the impacts kick up debris and planes.
            const float GROUND_LEVEL_OFFSET = 10f;

            // Planes.
            for (int a = 0; a < _objs.Planes.Count; a++)
            {
                var plane = _objs.Planes[a];

                // Check if the plane is going to hit the ground.
                if (!plane.InResetCooldown && plane.Altitude <= 500f)
                {
                    // TODO: Clients and server must both come to the same conclusion here.
                    // Otherwise the client plane may end up in a partially disabled/dead state.
                    // Making this server authoritative may be problematic for laggy clients.

                    if (Utilities.TryGetGroundCollisionPoint(plane, 0f, dt, out D2DPoint groundImpactPos))
                    {
                        // Run the hit ground logic.
                        if (!plane.HasCrashed)
                            plane.DoHitGround();
                    }
                }

                // Ease the plane rotation until it is flat on the ground.
                if (plane.IsDisabled && plane.Altitude <= 5f)
                {
                    float crashDir = 0f;
                    var pointingRight = Utilities.IsPointingRight(plane.Rotation);
                    if (pointingRight)
                        crashDir = 0f;
                    else
                        crashDir = 180f;

                    plane.Rotation = EaseRotation(crashDir, plane.Rotation, dt);
                    plane.RotationSpeed = 0f;
                }
            }

            // Bullets & missiles.
            for (int i = 0; i < _objs.Bullets.Count; i++)
            {
                var bullet = _objs.Bullets[i];
                if (!bullet.IsExpired && bullet.Altitude <= bullet.Velocity.Length() * dt)
                {
                    if (Utilities.TryGetGroundCollisionPoint(bullet, GROUND_LEVEL_OFFSET, dt, out D2DPoint groundImpactPos))
                    {
                        bullet.Position = groundImpactPos;
                        bullet.IsExpired = true;
                    }
                }
            }

            for (int i = 0; i < _objs.Missiles.Count; i++)
            {
                var missile = _objs.Missiles[i];

                // Skip net missiles. They should be expired by client packets if they hit the ground.
                if (missile.IsNetObject)
                    continue;

                if (!missile.IsExpired && missile.Altitude <= missile.Velocity.Length() * dt)
                {
                    if (Utilities.TryGetGroundCollisionPoint(missile, GROUND_LEVEL_OFFSET, dt, out D2DPoint groundImpactPos))
                    {
                        missile.Position = groundImpactPos;
                        missile.IsExpired = true;
                    }
                }
            }
        }

        private float EaseRotation(float target, float current, float dt)
        {
            const float RATE = 120f;
            if (current == target)
                return target;

            var diff = Utilities.ClampAngle180(target - current);
            var sign = Math.Sign(diff);
            var amt = RATE * sign * dt;

            if (Math.Abs(amt) > Math.Abs(diff))
                amt = diff;

            return current + amt;
        }

        // Quietly wrap any planes that try to leave the field.
        private void HandleFieldWrap()
        {
            for (int i = 0; i < _objs.Planes.Count; i++)
            {
                var plane = _objs.Planes[i];
                if (plane.Position.X > World.FieldPlaneXBounds.Y)
                {
                    plane.SetPosition(new D2DPoint(World.FieldPlaneXBounds.X, plane.Position.Y));
                }
                else if (plane.Position.X < World.FieldPlaneXBounds.X)
                {
                    plane.SetPosition(new D2DPoint(World.FieldPlaneXBounds.Y, plane.Position.Y));
                }
            }
        }
    }
}
