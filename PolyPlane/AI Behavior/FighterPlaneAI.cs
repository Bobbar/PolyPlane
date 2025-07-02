using PolyPlane.GameObjects;
using PolyPlane.GameObjects.Managers;
using PolyPlane.GameObjects.Tools;
using PolyPlane.Helpers;

namespace PolyPlane.AI_Behavior
{
    public class FighterPlaneAI : IAIBehavior
    {
        public FighterPlane Plane => _plane;
        public FighterPlane TargetPlane => _targetPlane;
        public GuidedMissile DefendingMissile = null;
        public AIPersonality Personality { get; set; }

        private FighterPlane _plane;
        private FighterPlane _targetPlane;
        private FighterPlane _killedByPlane;
        private D2DPoint _threatPosition = D2DPoint.Zero;
        private float _sineWavePos = 0f;
        private float _avoidingGroundAlt = 0f;
        private bool _avoidingGround = false;
        private bool _gainingVelo = false;
        private bool _reverseDirection = false;
        private bool _isDefending = false;

        private GameTimer _fireBurstTimer = new GameTimer(2f, 6f);
        private GameTimer _fireMissileCooldown = new GameTimer(6f);
        private GameTimer _dropDecoysTimer = new GameTimer(1.2f, 2.5f);
        private GameTimer _changeTargetCooldown = new GameTimer(10f);
        private GameTimer _defendMissileCooldown = new GameTimer(8f);
        private GameTimer _dodgeMissileCooldown = new GameTimer(4.5f);
        private RateLimiterAngle _defendAngleRate = new RateLimiterAngle(20f);

        private float MIN_MISSILE_TIME = 40f;
        private float MAX_MISSILE_TIME = 80f;
        private float MAX_SPEED = 1000f;
        private const float SPEED_HOLD_RANGE = 5f;
        private readonly float MAX_DECOY_DIST = 20000f; // Max distance between missile and plane before dropping decoys.

        private List<PlaneThreats> _threats = new List<PlaneThreats>();

        public FighterPlaneAI(FighterPlane plane, AIPersonality personality)
        {
            _plane = plane;
            Personality = personality;
            InitStuff();
        }

        private void InitStuff()
        {
            Plane.PlayerKilledCallback += HandlePlayerKilled;

            ConfigPersonality();

            _fireBurstTimer.StartCallback = () => this.Plane.FiringBurst = true;
            _fireBurstTimer.TriggerCallback = () => this.Plane.FiringBurst = false;

            _dropDecoysTimer.StartCallback = () => this.Plane.DroppingDecoy = true;
            _dropDecoysTimer.TriggerCallback = () => this.Plane.DroppingDecoy = false;

            _fireMissileCooldown = new GameTimer(Utilities.Rnd.NextFloat(MIN_MISSILE_TIME, MAX_MISSILE_TIME));
            _fireMissileCooldown.Start();

            _defendMissileCooldown.StartCallback = () => _isDefending = true;
            _defendMissileCooldown.TriggerCallback = () => _isDefending = false;
        }

        public void Update(float dt)
        {
            if (this.Plane.IsDisabled || this.Plane.HasCrashed)
            {
                _threats.Clear();
                _targetPlane = null;
                return;
            }

            _sineWavePos += 0.3f * dt;

            const float MAX_SIN_POS = 360f * Utilities.DEGREES_TO_RADS;
            if (_sineWavePos > MAX_SIN_POS)
                _sineWavePos = 0f;

            _fireBurstTimer.Update(dt);
            _fireMissileCooldown.Update(dt);
            _dropDecoysTimer.Update(dt);
            _changeTargetCooldown.Update(dt);
            _defendMissileCooldown.Update(dt);
            _dodgeMissileCooldown.Update(dt);
            _defendAngleRate.Update(dt);

            if (TargetPlane != null)
            {
                ConsiderFireBurstAtTarget();
                ConsiderFireMissileAtTarget();
            }

            ConsiderDefendMissile();
            ConsiderNewTarget();
            ConsiderDropDecoy();

            if (!this.Plane.IsDisabled)
            {
                // Adjust throttle to maintain max allowed speed.
                var velo = this.Plane.AirSpeedTrue;
                var thrustFact = Math.Clamp(1f - Utilities.Factor(velo - MAX_SPEED, SPEED_HOLD_RANGE), 0f, 1f);

                this.Plane.ThrustAmount = thrustFact;
            }
        }

        private void ConfigPersonality()
        {
            var types = Enum.GetValues(typeof(AIPersonality));
            foreach (AIPersonality type in types)
            {
                if ((this.Personality & type) == type)
                {
                    switch (type)
                    {
                        case AIPersonality.Normal:

                            break;

                        case AIPersonality.MissileHappy:
                            MIN_MISSILE_TIME = 20f;
                            MAX_MISSILE_TIME = 40f;

                            break;

                        case AIPersonality.LongBursts:
                            _fireBurstTimer.Interval = 3f;
                            break;

                        case AIPersonality.Cowardly:
                            // Don't allow both cowardly and speedy.
                            if (HasFlag(AIPersonality.Speedy) == false)
                            {
                                MAX_SPEED = 700f;
                                this.Plane.Thrust = 700f;
                            }
                            break;

                        case AIPersonality.Speedy:
                            // Don't allow both cowardly and speedy.
                            if (HasFlag(AIPersonality.Cowardly) == false)
                            {
                                MAX_SPEED = 2000f;
                                this.Plane.Thrust = 2000f;
                            }

                            break;
                    }
                }
            }
        }

        public bool HasFlag(AIPersonality flag)
        {
            return (this.Personality & flag) == flag;
        }

        private void HandlePlayerKilled(PlayerKilledEventArgs killedEvent)
        {
            _killedByPlane = killedEvent.AttackPlane;
        }

        private void ConsiderNewTarget()
        {
            if (_targetPlane != null && _targetPlane.IsDisabled)
                _targetPlane = null;

            var planes = World.ObjectManager.Planes;
            _threats.Clear();

            // Compute threat levels for other planes.
            foreach (var plane in planes)
            {
                if (plane.Equals(this.Plane))
                    continue;

                if (!plane.IsDisabled && !plane.HasCrashed)
                {
                    var threatLevel = ComputeThreatLevel(plane);

                    _threats.Add(new PlaneThreats(plane, threatLevel));
                }
            }

            // Sort by threat level.
            _threats.Sort();

            // Only proceed if we have a theat level greater than zero.
            if (_threats.Any(t => t.ThreatLevel > 0f))
            {
                var top = _threats.First();
                var topLevel = top.ThreatLevel;

                // Just pick the plane with the highest threat.
                if (_targetPlane == null)
                {
                    _targetPlane = top.Plane;
                }
                else
                {
                    // Otherwise only switch if the top threat is a higher level than the current target.
                    var curLevel = ComputeThreatLevel(_targetPlane);
                    if (topLevel > curLevel)
                        _targetPlane = top.Plane;
                }
            }

            // Run the regular AI behavior target selection if none was found in the threat-level logic.
            if (_targetPlane == null)
            {
                FighterPlane? newTarget = null;

                // Try to target killed-by plane for vengeful AI.
                if (HasFlag(AIPersonality.Vengeful))
                {
                    if (_killedByPlane != null && _killedByPlane.IsExpired)
                        _killedByPlane = null;

                    if (_killedByPlane != null && _killedByPlane.IsDisabled == false)
                    {
                        newTarget = _killedByPlane;
                    }
                }

                // Target one of the top scoring players.
                if (newTarget == null && HasFlag(AIPersonality.TargetTopPlanes))
                {
                    var targets = planes.Where(p => !p.Equals(this.Plane) && !p.IsDisabled).OrderByDescending(p => p.Kills).Take(Math.Min(planes.Count, 5)).ToArray();

                    if (targets.Any())
                        newTarget = targets[Random.Shared.Next(0, targets.Length)];
                }

                // If we still have no target, just pick the nearest one.
                if (newTarget == null)
                {
                    newTarget = this.Plane.Radar.FindNearestPlane();
                }

                _targetPlane = newTarget;
            }
        }

        /// <summary>
        /// Tries to compute a threat level for the specified plane. Higher threat levels for planes which are close, closing-in, locked on and pointing at our plane.
        /// </summary>
        /// <param name="plane"></param>
        /// <returns></returns>
        private float ComputeThreatLevel(FighterPlane plane)
        {
            const float CLOSE_DIST = 6000f; // Min distance to me considered "close"
            const float CLOSE_RATE = 400f; // Closing-rate for max threat level.
            const float CLOSE_MULTI = 3f;
            const float LOCKON_MULTI = 2f;
            const float MAX_FOV = 10f;
            const float FOV_MULTI = 3f;

            float level = 0f;

            // Increase threat level as distance decreases.
            var dist = this.Plane.Position.DistanceTo(plane.Position);
            level += CLOSE_MULTI * (1f - Utilities.Factor(dist, CLOSE_DIST));

            // Add locked-on multiplier.
            bool isLockedOn = plane.Radar.IsLockedOnTo(this.Plane);

            if (isLockedOn)
                level *= LOCKON_MULTI;

            // Add FOV multiplier.
            if (plane.FOVToObject(this.Plane) < MAX_FOV)
                level *= FOV_MULTI;

            // Closing-rate multiplier.
            var closingRate = Utilities.ClosingRate(this.Plane, plane);
            var closeRateFact = Utilities.Factor(closingRate, CLOSE_RATE);
            level *= closeRateFact + 0.1f;

            return level;
        }

        private void ConsiderFireMissileAtTarget()
        {
            if (_fireMissileCooldown.IsRunning)
                return;

            const float MAX_DIST = 50000f;
            const float MIN_DIST = 1500f;

            if (this.Plane.Radar.IsLockedOnTo(TargetPlane))
            {
                var dist = this.Plane.Position.DistanceTo(TargetPlane.Position);

                if (dist > MAX_DIST || dist < MIN_DIST)
                    return;

                var fov = this.Plane.FOVToObject(TargetPlane);

                if (fov > World.SENSOR_FOV * 0.5f)
                    return;

                this.Plane.FireMissile(TargetPlane);

                _fireMissileCooldown.Interval = Utilities.Rnd.NextFloat(MIN_MISSILE_TIME, MAX_MISSILE_TIME);
                _fireMissileCooldown.Restart();
            }
        }

        private void ConsiderFireBurstAtTarget()
        {
            if (_fireBurstTimer.IsRunning)
                return;

            // Give the burst cooldown a little bit of variation.
            _fireBurstTimer.Cooldown = Utilities.Rnd.NextFloat(4f, 6f);

            const float MIN_DIST = 2000f;
            const float MIN_OFFBORE = 10f;

            var plrDist = D2DPoint.Distance(TargetPlane.Position, this.Plane.Position);

            if (plrDist > MIN_DIST)
                return;

            var plrFOV = this.Plane.FOVToObject(TargetPlane);

            if (plrFOV <= MIN_OFFBORE)
            {
                _fireBurstTimer.Restart();
            }
        }

        private void ConsiderDefendMissile()
        {
            var threat = Plane.Radar.FindNearestThreat();

            DefendingMissile = threat;

            if (DefendingMissile != null)
            {
                _defendMissileCooldown.Restart();
                _threatPosition = DefendingMissile.Position;
            }
        }

        private void ConsiderDropDecoy()
        {
            if (!_isDefending)
                return;
            else
            {
                if (_threatPosition.DistanceTo(this.Plane.Position) < MAX_DECOY_DIST)
                {
                    if (!_dropDecoysTimer.IsRunning)
                    {
                        _dropDecoysTimer.Restart();
                    }
                }
            }
        }

        public void ClearTarget()
        {
            _changeTargetCooldown.Reset();
            _targetPlane = null;
            ConsiderNewTarget();
        }

        public void ChangeTarget(FighterPlane plane)
        {
            if (_changeTargetCooldown.IsRunning)
                return;

            if (plane != null)
            {
                _targetPlane = plane;
                _changeTargetCooldown.Restart();
            }
        }

        /// <summary>
        /// Computes the final guidance direction based on the current state.
        /// </summary>
        /// <param name="dt"></param>
        /// <returns></returns>
        public float GetAIGuidanceDirection(float dt)
        {
            const float MIN_IMPACT_TIME = 7.5f; // Min ground impact time to consider avoiding ground.
            const float BLOCK_PITCH_DOWN_ALT = 800f; // Do not allow pitch down angles below this altitude.
            const float EXTRA_AIM_AMT = 0.4f; // How much to pitch beyond the location of the target plane.  (Helps with dog-fighting)
            const float MIN_VELO = 110f; // Min velo before trying to gain velocity;
            const float OK_VELO = 210f; // Velo to stop trying to gain velocity.
            const float RUN_DISTANCE = 30000f; // Cowardly IA: How close before we run away from the target plane.
            const float FIGHT_DISTANCE = 4000f; // Cowardly IA: If the target is closer than this, engage and fight them.

            var finalAngle = 0f;

            // This logic is ordered by lowest to highest priority.
            // Flying towards (or away) from target is lowests, while
            // avoiding ground collisions and maintaining speed are highest priority.


            // *** Fly around in a sinusoidal path if there are no targets. ***
            var patrolDir = Utilities.ClampAngle(Utilities.RadsToDegrees(MathF.Sin(_sineWavePos)));

            // Flip directions if we are near the end of the play field.
            if (this.TargetPlane == null)
            {
                if (this.Plane.Position.X > World.PlaneSpawnRange + 10000f && !_reverseDirection)
                    _reverseDirection = true;

                if (this.Plane.Position.X < -World.PlaneSpawnRange - 10000f && _reverseDirection)
                    _reverseDirection = false;
            }

            if (_reverseDirection)
                patrolDir = Utilities.ClampAngle(patrolDir + 180f);

            finalAngle = patrolDir;

            // *** Fly towards or away from the target plane, depending on personality. ***
            if (TargetPlane != null)
            {
                var dirToTarget = TargetPlane.Position - this.Plane.Position;
                var distToTarget = this.Plane.Position.DistanceTo(TargetPlane.Position);

                if (HasFlag(AIPersonality.Cowardly) && distToTarget < RUN_DISTANCE && distToTarget > FIGHT_DISTANCE)
                {
                    // Fly away from target plane.
                    dirToTarget *= -1f;
                    finalAngle = dirToTarget.Angle();
                    finalAngle += patrolDir * 0.2f; // Incorporate a small amount of the sine wave so we 'bob & weave' a little bit.
                }
                else
                {
                    // Fly towards target plane.
                    finalAngle = dirToTarget.Angle();

                    // Add additional pitch. Helps increase agro while dog-fighting.
                    var rotAmt = Utilities.RadsToDegrees((this.Plane.Position - TargetPlane.Position).Normalized().Cross(Utilities.AngleToVectorDegrees(this.Plane.Rotation)));
                    finalAngle = Utilities.ClampAngle(finalAngle + rotAmt * EXTRA_AIM_AMT);
                }
            }

            // *** Defend against incoming missiles. ***
            if (_isDefending && DefendingMissile != null)
            {
                // Compute two tangential angles and choose the one which
                // should give us the best chance of gaining velo while also 
                // putting decoys into the FOV of the missile.

                // The idea is to lead the missile while gaining velo, then
                // perform a rapid pitch maneuver at the last second to force an overshoot.

                var angleAwayFromThreat = (this.Plane.Position - _threatPosition).Angle();
                var impactTime = Utilities.ImpactTime(this.Plane, DefendingMissile);
                var threatVeloAngle = DefendingMissile.Velocity.Angle();

                const float DEFEND_ANGLE = 25f; // Offset angle to threat slightly to try to put decoys in the flight path.
                const float DODGE_TIME = 2f; // Impact time to try to dodge the missile.

                var defendAngleOne = Utilities.ClampAngle(angleAwayFromThreat + DEFEND_ANGLE);
                var defendAngleTwo = Utilities.ClampAngle(angleAwayFromThreat - DEFEND_ANGLE);

                var diffOne = Utilities.AngleDiff(defendAngleOne, this.Plane.Rotation);
                var diffTwo = Utilities.AngleDiff(defendAngleTwo, this.Plane.Rotation);

                float defendAngle = finalAngle;

                // Try to select an angle which points down. (To maximize velo)
                if (Utilities.IsPointingDown(defendAngleOne))
                    defendAngle = defendAngleOne;
                else if (Utilities.IsPointingDown(defendAngleTwo))
                    defendAngle = defendAngleTwo;
                else // Otherwise pick the one closest to our current rotation.
                {
                    if (diffOne < diffTwo)
                        defendAngle = defendAngleOne;
                    else if (diffTwo < diffOne)
                        defendAngle = defendAngleTwo;
                }

                // Pass the defend angle through a rate limiter
                // to try to smooth out the movements.
                defendAngle = _defendAngleRate.SetTarget(defendAngle);

                // Try dodge when the missile gets close.
                // Since this condition can be fleeting
                // we start a short timer so we maintain the dodge
                // angle for a little while after it gets triggered.
                if (Math.Abs(impactTime) < DODGE_TIME)
                {
                    if (!_dodgeMissileCooldown.IsRunning)
                        _dodgeMissileCooldown.Restart();
                }

                // Perform a rapid pitch maneuver just before the missile impacts.
                if (_dodgeMissileCooldown.IsRunning)
                {
                    // Compute up & down tangents.
                    var defAngleTangentDown = Utilities.TangentAngle(defendAngle);
                    var defAngleTangentUp = Utilities.ReverseAngle(defAngleTangentDown);

                    // Compute diffs between threat velo angle and choose the smallest.p
                    // Try to choose the option which will not cross paths with the incoming missile.
                    var diffDown = Utilities.AngleDiff(defAngleTangentDown, threatVeloAngle);
                    var diffUp = Utilities.AngleDiff(defAngleTangentUp, threatVeloAngle);

                    if (diffDown < diffUp)
                        finalAngle = defAngleTangentDown;
                    else
                        finalAngle = defAngleTangentUp;
                }
                else
                {
                    finalAngle = defendAngle;
                }
            }

            // *** Apply lead when firing a burst at the target. ***
            if (_fireBurstTimer.IsRunning && TargetPlane != null)
            {
                var aimAmt = LeadTarget(TargetPlane, dt);
                finalAngle = aimAmt;
            }

            // *** Gain velocity as needed. ***
            var velo = this.Plane.AirSpeedIndicated;
            if (velo < MIN_VELO)
                _gainingVelo = true;
            else if (velo > OK_VELO)
                _gainingVelo = false;

            // Pitch down to gain velo.
            // Don't bother if we are dodging.
            if (_gainingVelo && !_dodgeMissileCooldown.IsRunning)
                finalAngle = Utilities.MaintainAltitudeAngle(this.Plane, this.Plane.Altitude - 200f);

            // *** Avoid the ground if we are close to impacting. ***
            var groundImpactTime = Utilities.GroundImpactTime(this.Plane);
            if (groundImpactTime > 0f && groundImpactTime < MIN_IMPACT_TIME && !_avoidingGround)
            {
                _avoidingGround = true;
                _avoidingGroundAlt = this.Plane.Altitude;

                if (_avoidingGroundAlt < 500f)
                    _avoidingGroundAlt = 500f;
            }

            if (groundImpactTime > MIN_IMPACT_TIME)
                _avoidingGround = false;

            // Climb until no longer in danger of ground collision.
            if (_avoidingGround)
                finalAngle = Utilities.MaintainAltitudeAngle(this.Plane, _avoidingGroundAlt);

            // Start reversing the angle at very low altitude.
            // Pitch down becomes a pitch up.
            if (this.Plane.Altitude < BLOCK_PITCH_DOWN_ALT)
            {
                if (finalAngle > 0f && finalAngle < 180f)
                    finalAngle = 360f - finalAngle;
            }

            // Clamp the final angle, just in case.
            finalAngle = Utilities.ClampAngle(finalAngle);
            return finalAngle;
        }

        private float LeadTarget(GameObject target, float dt)
        {
            const float pValue = 5f;

            var los = target.Position - this.Plane.Position;
            var navigationTime = los.Length() / (this.Plane.AirSpeedTrue * dt);
            var targRelInterceptPos = los + ((target.Velocity * dt) * navigationTime);

            targRelInterceptPos *= pValue;

            var leadRotation = ((target.Position + targRelInterceptPos) - this.Plane.Position).Angle();
            var targetRot = leadRotation;

            return targetRot;
        }


        private struct PlaneThreats : IComparable<PlaneThreats>
        {
            public FighterPlane Plane;
            public float ThreatLevel = 0;

            public PlaneThreats(FighterPlane plane, float threatLevel)
            {
                Plane = plane;
                ThreatLevel = threatLevel;
            }

            public int CompareTo(PlaneThreats other)
            {
                // Order by theat level descending.
                return other.ThreatLevel.CompareTo(ThreatLevel);
            }
        }
    }
}
