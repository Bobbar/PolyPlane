using PolyPlane.GameObjects.Interfaces;
using PolyPlane.Helpers;
using PolyPlane.Net;

namespace PolyPlane.GameObjects.Manager
{
    public class CollisionManager : IImpactEvent
    {
        private GameObjectManager _objs = World.ObjectManager;
        private NetEventManager _netMan;

        private bool _isNetGame = true;

        public event EventHandler<ImpactEvent> ImpactEvent;

        public CollisionManager(NetEventManager netMan)
        {
            _netMan = netMan;
        }

        public CollisionManager()
        {
            _isNetGame = false;
        }

        public void DoCollisions()
        {
            if (_netMan != null && !_netMan.IsServer)
            {
                HandleExplosionImpulse();
                HandleGroundImpacts();
                HandleFieldWrap();
                return;
            }

            var now = World.CurrentTime();

            // Targets/AI Planes vs missiles and bullets.
            for (int r = 0; r < _objs.Planes.Count; r++)
            {
                var plane = _objs.Planes[r];

                if (plane == null)
                    continue;

                uint planeRTT = 0;

                if (_isNetGame)
                    planeRTT = _netMan.Host.GetPlayerRTT(plane.PlayerID);

                var nearObjs = _objs.GetNear(plane);

                foreach (var obj in nearObjs)
                {
                    if (obj is GuidedMissile missile)
                    {
                        var missileOwner = missile.Owner as FighterPlane;

                        if (missile.Owner.Equals(plane))
                            continue;

                        if (missile.IsExpired)
                            continue;

                        if (_isNetGame)
                        {
                            var missileLagComp = missile.LagAmount / 2f;

                            // Don't compensate as much for AI planes?
                            if (missileOwner.IsAI)
                                missileLagComp = planeRTT;

                            if (plane.CollidesWithNet(missile, out D2DPoint pos, out GameObjectPacket? histState, now - missileLagComp))
                            {

                                if (histState != null)
                                {
                                    var ogState = new GameObjectPacket(plane);

                                    plane.Position = histState.Position;
                                    plane.Rotation = histState.Rotation;
                                    plane.SyncFixtures();

                                    var impactResultM = plane.GetImpactResult(missile, pos);
                                    _netMan.SendNetImpact(missile, plane, impactResultM, histState);

                                    plane.Position = ogState.Position;
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
                                    var result = plane.GetImpactResult(missile, pos);

                                    ImpactEvent?.Invoke(this, new ImpactEvent(plane, missile, result.DoesDamage));

                                    plane.HandleImpactResult(missile, result);

                                    missile.Position = pos;
                                    missile.IsExpired = true;
                                }
                            }
                        }
                    }

                    if (obj is Bullet bullet)
                    {
                        var bulletOwner = bullet.Owner as FighterPlane;

                        if (bullet.IsExpired)
                            continue;

                        if (bullet.Owner.Equals(plane))
                            continue;


                        if (_isNetGame)
                        {
                            var bulletLagComp = bullet.LagAmount / 2f;

                            if (bulletOwner.IsAI)
                                bulletLagComp = planeRTT;

                            if (plane.CollidesWithNet(bullet, out D2DPoint pos, out GameObjectPacket? histState, now - bulletLagComp))
                            {
                                if (histState != null)
                                {
                                    var ogState = new GameObjectPacket(plane);

                                    plane.Position = histState.Position;
                                    plane.Rotation = histState.Rotation;
                                    plane.SyncFixtures();

                                    var impactResult = plane.GetImpactResult(bullet, pos);
                                    _netMan.SendNetImpact(bullet, plane, impactResult, histState);

                                    plane.Position = ogState.Position;
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
                                    var result = plane.GetImpactResult(bullet, pos);

                                    ImpactEvent?.Invoke(this, new ImpactEvent(plane, bullet, result.DoesDamage));

                                    plane.HandleImpactResult(bullet, result);

                                    bullet.Position = pos;
                                    bullet.IsExpired = true;
                                }
                            }
                        }
                    }

                    DoParticleImpulse(plane, obj);
                }
            }

            // Handle missiles hit by bullets and missiles hitting decoys.
            for (int m = 0; m < _objs.Missiles.Count; m++)
            {
                var missile = _objs.Missiles[m] as Missile;

                if (missile.IsExpired)
                    continue;

                var nearObjs = _objs.GetNear(missile);

                foreach (var obj in nearObjs)
                {
                    if (obj is Bullet bullet)
                    {
                        if (bullet.IsExpired)
                            continue;

                        if (bullet.Owner == missile.Owner)
                            continue;

                        if (missile.CollidesWith(bullet, out D2DPoint posb))
                        {
                            missile.IsExpired = true;
                            bullet.IsExpired = true;
                        }
                    }
                    else if (obj is Decoy decoy)
                    {
                        if (decoy.IsExpired)
                            continue;

                        if (missile.CollidesWith(decoy, out D2DPoint posb))
                        {
                            missile.IsExpired = true;
                            decoy.IsExpired = true;
                        }
                    }

                    DoParticleImpulse(missile, obj);
                }
            }

            HandleExplosionImpulse();
            HandleGroundImpacts();
            HandleFieldWrap();
        }

        private void DoParticleImpulse(GameObject pushObject, GameObject particleObject)
        {
            const float EFFECT_DIST_PLANE = 90f;
            const float EFFECT_DIST_MISSILE = 55f;
            const float EFFECT_DIST_GROW = 20f;
            const float EFFECT_GROW_VELO = 800f;

            const float MIN_EFFECT_AGE = 2f;
            const float FORCE = 8000f;
            const float VELO_FACTOR = 0.1f;
            const float MIN_VELO = 10f;

            if (particleObject.IsNetObject)
                return;

            if (particleObject is not IPushable)
                return;

            // Don't push planes.
            if (particleObject is FighterPlane)
                return;

            if (particleObject.Equals(pushObject))
                return;

            var pushVelo = pushObject.Velocity.Length();

            // Skip impulse when velo is low.
            if (pushVelo < MIN_VELO)
                return;

            // Increase effect distance with velo.            
            var effectDist = EFFECT_DIST_PLANE;

            if (pushObject is GuidedMissile)
                effectDist = EFFECT_DIST_MISSILE;

            var veloFact = Utilities.Factor(pushVelo, EFFECT_GROW_VELO);
            effectDist += EFFECT_DIST_GROW * veloFact;

            var dist = pushObject.Position.DistanceTo(particleObject.Position) + float.Epsilon;

            // Skip if outside effect dist.
            if (dist > effectDist)
                return;

            var forceFact = 1f - Utilities.FactorWithEasing(dist, effectDist, EasingFunctions.EaseInSine);

            float ageFact = 1f;

            // Ease in impulse for particles spawned by the pusher object.
            if (particleObject.Owner != null && particleObject.Owner.Equals(pushObject))
                ageFact = Utilities.FactorWithEasing(particleObject.Age, MIN_EFFECT_AGE, EasingFunctions.EaseInQuintic);

            var dir = (particleObject.Position - pushObject.Position);
            var dirNorm = dir.Normalized();
            var forceVec = dirNorm * (FORCE * forceFact);

            // Push particles away.
            particleObject.Velocity += (forceVec / particleObject.Mass * World.DT) * ageFact;

            // Add some velo from the pusher object.
            particleObject.Velocity += ((pushObject.Velocity * VELO_FACTOR) * forceFact * ageFact * veloFact);
        }

        private void HandleExplosionImpulse()
        {
            const float FORCE = 50000f;
            const float DAMAGE_AMT = 25f;

            foreach (Explosion explosion in _objs.Explosions)
            {
                if (explosion.IsExpired || explosion.Age > explosion.Duration)
                    continue;

                var nearObjs = _objs.GetNear(explosion);

                foreach (var obj in nearObjs)
                {
                    if (obj.IsNetObject)
                        continue;

                    if (obj is not IPushable)
                        continue;

                    var dist = explosion.Position.DistanceTo(obj.Position) + float.Epsilon;
                    var effectRadius = explosion.Radius * 1.2f;

                    if (dist <= effectRadius)
                    {
                        // Impart an impulse on other nearby objects.
                        var forceFact = 1f - Utilities.FactorWithEasing(dist, effectRadius, EasingFunctions.EaseOutCirc) + 0.1f;

                        var dir = (obj.Position - explosion.Position);
                        var dirNorm = dir.Normalized();
                        var forceVec = dirNorm * (FORCE * forceFact);
                        obj.Velocity += forceVec / obj.Mass * World.DT;

                        if (!obj.IsAwake)
                            obj.IsAwake = true;

                        if (obj is FighterPlane plane && explosion.Owner is GuidedMissile missile)
                        {
                            if (!missile.Owner.Equals(plane) && !plane.IsDisabled)
                            {
                                // Apply a small amount of damage to planes within the blast radius of the explosion.
                                plane.Health -= (forceFact * DAMAGE_AMT) * World.DT;

                                // Handle planes killed by blast.
                                if (plane.Health <= 0 && !plane.IsDisabled)
                                {
                                    plane.DoPlayerKilled(missile);
                                    ImpactEvent?.Invoke(this, new ImpactEvent(plane, missile, true));
                                }
                                else
                                {
                                    ImpactEvent?.Invoke(this, new ImpactEvent(plane, missile, true));
                                }
                            }
                        }
                        else if (obj is GuidedMissile detMissile) // Detonate any other missiles within the blast radius.
                            if (!detMissile.IsExpired)
                                detMissile.IsExpired = true;
                    }
                }
            }
        }

        private void HandleGroundImpacts()
        {
            // Planes.
            for (int a = 0; a < _objs.Planes.Count; a++)
            {
                var plane = _objs.Planes[a];

                if (plane.Altitude <= 1f && !plane.InResetCooldown)
                {
                    if (!plane.HasCrashed)
                        plane.DoHitGround();

                    float crashDir = 0f;
                    var pointingRight = Utilities.IsPointingRight(plane.Rotation);
                    if (pointingRight)
                        crashDir = 0f;
                    else
                        crashDir = 180f;

                    // Ease the plane rotation until it is flat on the ground.
                    plane.Rotation = EaseRotation(crashDir, plane.Rotation);
                    plane.RotationSpeed = 0f;
                }
            }

            // Bullets & missiles.
            if (!World.IsNetGame || World.IsClient)
            {
                foreach (var bullet in _objs.Bullets)
                {
                    if (bullet.Altitude <= 1f && !bullet.IsExpired)
                        bullet.IsExpired = true;
                }
            }

            foreach (var missile in _objs.Missiles)
            {
                if (missile.Altitude <= 1f && !missile.IsExpired)
                    missile.IsExpired = true;
            }
        }

        private float EaseRotation(float target, float current)
        {
            const float RATE = 120f;
            if (current == target)
                return target;

            var diff = Utilities.ClampAngle180(target - current);
            var sign = Math.Sign(diff);
            var amt = RATE * sign * World.DT;

            if (Math.Abs(amt) > Math.Abs(diff))
                amt = diff;

            return current + amt;
        }

        // Quietly wrap any planes that try to leave the field.
        private void HandleFieldWrap()
        {
            foreach (var plane in _objs.Planes)
            {
                if (plane.Position.X > World.FieldXBounds.Y)
                {
                    plane.Position = new D2DPoint(World.FieldXBounds.X, plane.Position.Y);
                    plane.SyncFixtures();
                }
                else if (plane.Position.X < World.FieldXBounds.X)
                {
                    plane.Position = new D2DPoint(World.FieldXBounds.Y, plane.Position.Y);
                    plane.SyncFixtures();
                }
            }
        }
    }
}
