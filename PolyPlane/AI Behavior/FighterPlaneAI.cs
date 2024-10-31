using PolyPlane.GameObjects;
using PolyPlane.GameObjects.Animations;
using PolyPlane.GameObjects.Tools;
using PolyPlane.Helpers;

namespace PolyPlane.AI_Behavior
{
    public class FighterPlaneAI : IAIBehavior
    {
        public FighterPlane Plane => _plane;
        public FighterPlane TargetPlane => _targetPlane;
        public Missile DefendingMissile = null;
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
        private GameTimer _dropDecoysTimer = new GameTimer(2f, 3f);
        private GameTimer _changeTargetCooldown = new GameTimer(10f);
        private GameTimer _defendMissileCooldown = new GameTimer(4f);

        private float MIN_MISSILE_TIME = 40f;
        private float MAX_MISSILE_TIME = 80f;
        private float MAX_SPEED = 1000f;
        private readonly float MAX_DECOY_DIST = 20000f; // Max distance between missile and plane before dropping decoys.


        public FighterPlaneAI(FighterPlane plane, AIPersonality personality)
        {
            _plane = plane;
            Personality = personality;
            InitStuff();
        }

        private void InitStuff()
        {
            Plane.PlayerKilledCallback += HandlePlayerKilled;

            Plane.ThrustOn = true;

            ConfigPersonality();

            _fireBurstTimer.StartCallback = () =>
            this.Plane.FiringBurst = true;

            _fireBurstTimer.TriggerCallback = () =>
            this.Plane.FiringBurst = false;

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
                return;

            _sineWavePos += 0.3f * dt;

            const float MAX_SIN_POS = 360f * Utilities.DEGREES_TO_RADS;
            if (_sineWavePos > MAX_SIN_POS)
                _sineWavePos = 0f;

            _fireBurstTimer.Update(dt);
            _fireMissileCooldown.Update(dt);
            _dropDecoysTimer.Update(dt);
            _changeTargetCooldown.Update(dt);
            _defendMissileCooldown.Update(dt);

            if (TargetPlane != null)
            {
                ConsiderFireBurstAtTarget();
                ConsiderFireMissileAtTarget();
            }

            ConsiderDefendMissile();
            ConsiderNewTarget();
            ConsiderDropDecoy();

            var velo = this.Plane.AirSpeedTrue;

            if (velo > MAX_SPEED)
                this.Plane.ThrustOn = false;
            else
                this.Plane.ThrustOn = true;
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
                            if (this.Personality.HasFlag(AIPersonality.Speedy) == false)
                            {
                                MAX_SPEED = 700f;
                                this.Plane.Thrust = 700f;
                            }
                            break;

                        case AIPersonality.Speedy:
                            // Don't allow both cowardly and speedy.
                            if (this.Personality.HasFlag(AIPersonality.Cowardly) == false)
                            {
                                MAX_SPEED = 2000f;
                                this.Plane.Thrust = 2000f;
                            }

                            break;
                    }
                }
            }
        }

        private void HandlePlayerKilled(FighterPlane plane, GameObject impactor)
        {
            if (impactor.Owner is FighterPlane attackerPlane)
                _killedByPlane = attackerPlane;
        }

        private void ConsiderNewTarget()
        {
            if (this.TargetPlane == null || this.TargetPlane.IsExpired || this.TargetPlane.HasCrashed || this.TargetPlane.IsDisabled)
            {
                var rndTarg = this.Plane.Radar.FindNearestPlane();

                if (_killedByPlane != null && _killedByPlane.IsExpired)
                    _killedByPlane = null;

                if ((this.Personality & AIPersonality.Vengeful) == AIPersonality.Vengeful && (_killedByPlane != null && _killedByPlane.IsDisabled == false))
                    rndTarg = _killedByPlane;

                _targetPlane = rndTarg;

                if (_targetPlane != null)
                    Log.Msg($"Picked new target: {this.Plane.ID} -> {this.TargetPlane.ID} ");
            }
        }

        private void ConsiderFireMissileAtTarget()
        {
            if (_fireMissileCooldown.IsRunning)
                return;

            const float MAX_DIST = 50000f;
            const float MIN_DIST = 1500f;

            if (this.Plane.Radar.HasLock && this.Plane.Radar.LockedObj != null && this.Plane.Radar.LockedObj.Equals(TargetPlane))
            {
                var dist = this.Plane.Position.DistanceTo(this.Plane.Radar.LockedObj.Position);

                if (dist > MAX_DIST || dist < MIN_DIST)
                    return;

                var fov = this.Plane.FOVToObject(TargetPlane);

                if (fov > World.SENSOR_FOV * 0.5f)
                    return;

                this.Plane.FireMissile(this.Plane.Radar.LockedObj);

                _fireMissileCooldown.Interval = Utilities.Rnd.NextFloat(MIN_MISSILE_TIME, MAX_MISSILE_TIME);
                _fireMissileCooldown.Restart();

                Log.Msg("Firing Missile");
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

        public float GetAIGuidance()
        {
            const float MIN_IMPACT_TIME = 7f; // Min ground impact time to consider avoiding ground.
            const float BLOCK_PITCH_DOWN_ALT = 800f; // Do not allow pitch down angles below this altitude.
            const float EXTRA_AIM_AMT = 0.4f; // How much to pitch beyond the location of the target plane.  (Helps with dog-fighting)
            const float MIN_VELO = 160f; // Min velo before trying to gain velocity;
            const float OK_VELO = 210f; // Velo to stop trying to gain velocity.
            const float RUN_DISTANCE = 30000f; // Cowardly IA: How close before we run away from the target plane.
            const float FIGHT_DISTANCE = 4000f; // Cowardly IA: If the target is closer than this, engage and fight them.

            var patrolDir = Utilities.ClampAngle(Utilities.RadsToDegrees((float)Math.Sin(_sineWavePos)));

            if (_reverseDirection)
                patrolDir = Utilities.ClampAngle(patrolDir + 180f);

            var groundImpactTime = Utilities.GroundImpactTime(this.Plane);
            var angle = patrolDir;

            if (TargetPlane != null)
            {
                var dirToTarget = TargetPlane.Position - this.Plane.Position;
                var distToTarget = this.Plane.Position.DistanceTo(TargetPlane.Position);

                if ((this.Personality & AIPersonality.Cowardly) == AIPersonality.Cowardly && distToTarget < RUN_DISTANCE && distToTarget > FIGHT_DISTANCE)
                {
                    // Fly away from target plane.
                    dirToTarget *= -1f;
                    angle = dirToTarget.Angle();
                    angle += patrolDir * 0.2f; // Incorporate a small amount of the sine wave so we 'bob & weave' a little bit.
                }
                else
                {
                    // Fly towards target plane.
                    angle = dirToTarget.Angle();

                    // Add additional pitch. Helps increase agro while dog-fighting.
                    var rotAmt = Utilities.RadsToDegrees((this.Plane.Position - TargetPlane.Position).Normalized().Cross(Utilities.AngleToVectorDegrees(this.Plane.Rotation)));
                    angle = Utilities.ClampAngle(angle + rotAmt * EXTRA_AIM_AMT);
                }
            }

            // Fly away from missile?
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
                const float DODGE_TIME = 3f; // Impact time to try to dodge the missile.

                var defendAngleOne = Utilities.ClampAngle(angleAwayFromThreat + DEFEND_ANGLE);
                var defendAngleTwo = Utilities.ClampAngle(angleAwayFromThreat - DEFEND_ANGLE);

                var diffOne = Utilities.AngleDiff(defendAngleOne, this.Plane.Rotation);
                var diffTwo = Utilities.AngleDiff(defendAngleTwo, this.Plane.Rotation);

                float defendAngle = angle;

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

                // Try dodge when the missile gets close.
                // Perform a rapid pitch maneuver just before the missile impacts.
                if (impactTime < DODGE_TIME)
                {
                    // Compute up & down tangents.
                    var defAngleTangentDown = Utilities.TangentAngle(defendAngle);
                    var defAngleTangentUp = Utilities.ReverseAngle(Utilities.TangentAngle(defendAngle));

                    // Try to pitch in a direction which will force the missile to overshoot.
                    // Flip the pitch directions as needed depending on our current direction.
                    if (Utilities.IsPointingDown(threatVeloAngle))
                    {
                        if (Utilities.IsPointingRight(this.Plane.Rotation))
                            angle = defAngleTangentUp;
                        else
                            angle = defAngleTangentDown;
                    }
                    else
                    {
                        if (Utilities.IsPointingRight(this.Plane.Rotation))
                            angle = defAngleTangentDown;
                        else
                            angle = defAngleTangentUp;
                    }
                }
                else
                {
                    angle = defendAngle;
                }
            }

            // Try to lead the target if we are firing a burst.
            if (_fireBurstTimer.IsRunning && TargetPlane != null)
            {
                var aimAmt = LeadTarget(TargetPlane);
                angle = aimAmt;
            }

            // Do we need to gain velocity?
            var velo = this.Plane.AirSpeedIndicated;
            if (velo < MIN_VELO)
                _gainingVelo = true;
            else if (velo > OK_VELO)
                _gainingVelo = false;

            // Pitch down to gain velo.
            if (_gainingVelo)
                angle = Utilities.MaintainAltitudeAngle(this.Plane, this.Plane.Altitude - 200f);

            // Pitch up if we about to impact with ground.
            if (groundImpactTime > 0f && groundImpactTime < MIN_IMPACT_TIME && !_avoidingGround)
            {
                _avoidingGround = true;
                _avoidingGroundAlt = this.Plane.Altitude;

                if (_avoidingGroundAlt < 500f)
                    _avoidingGroundAlt = 500f;
            }

            if (groundImpactTime < 0f)
            {
                _avoidingGround = false;
            }

            // Climb until no longer in danger of ground collision.
            if (_avoidingGround)
                angle = Utilities.MaintainAltitudeAngle(this.Plane, _avoidingGroundAlt);

            // Stay within the spawn area when not actively targeting another plane.
            if (this.TargetPlane == null)
            {
                if (this.Plane.Position.X > World.PlaneSpawnRange.Y + 10000f && !_reverseDirection)
                    _reverseDirection = true;

                if (this.Plane.Position.X < World.PlaneSpawnRange.X - 10000f && _reverseDirection)
                    _reverseDirection = false;
            }

            // Start reversing the angle at very low altitude.
            // Pitch down becomes a pitch up.
            if (this.Plane.Altitude < BLOCK_PITCH_DOWN_ALT)
            {
                if (angle > 0f && angle < 180f)
                    angle = 360f - angle;
            }

            var finalAngle = angle;
            finalAngle = Utilities.ClampAngle(finalAngle);
            return finalAngle;
        }

        private float LeadTarget(GameObject target)
        {
            const float pValue = 5f;

            var los = target.Position - this.Plane.Position;
            var navigationTime = los.Length() / (this.Plane.AirSpeedTrue * World.DT);
            var targRelInterceptPos = los + ((target.Velocity * World.DT) * navigationTime);

            targRelInterceptPos *= pValue;

            var leadRotation = ((target.Position + targRelInterceptPos) - this.Plane.Position).Angle();
            var targetRot = leadRotation;

            return targetRot;
        }
    }
}
