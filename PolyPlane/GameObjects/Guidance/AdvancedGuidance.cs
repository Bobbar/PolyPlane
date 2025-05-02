using PolyPlane.Helpers;

namespace PolyPlane.GameObjects.Guidance
{

    /// <summary>
    /// My own implementation of missile guidance. This logic trades computational complexity for accuracy, and tries to find a more efficient guidance solution.
    /// </summary>
    public class AdvancedGuidance : GuidanceBase
    {
        private D2DPoint _prevTargPos = D2DPoint.Zero;
        private D2DPoint _prevImpactPnt = D2DPoint.Zero;
        private SmoothFloat _closingRateSmooth = new SmoothFloat(5);
        private SmoothPoint _predictSmooth = new SmoothPoint(10);

        private float _prevVelo = 0f;
        private float _prevTargetDist = 0f;
        private float _prevTargVeloAngle = 0f;
        private const int MAX_FTI = 4000; // Max iterations allowed.

        public AdvancedGuidance(GuidedMissile missile, GameObject target) : base(missile, target)
        {
            StableAimPoint = GetTargetPosition();
        }

        public override float GetGuidanceDirection(float dt)
        {
            // Tweakables
            const float ROT_MOD_TIME = 30f; // Impact time to begin increasing rotation rate. (Get more aggro the closer we get)
            const float ROT_MOD_AMT = 1.2f;//0.95f; // Max amount to increase rot rate per above time.
            const float ROT_AMT_FACTOR = 1.4f; // Effects sensitivity and how much rotation is computed. (Higher value == more rotatation for a given aim direction)
            const float IMPACT_POINT_DELTA_THRESH = 10f; // Smaller value = target impact point later. (Waits until the point has stabilized more)
            const float MIN_CLOSE_RATE = 0.05f; // Min closing rate required to aim at predicted impact point.

            var targetPosition = GetTargetPosition();
            var targetVelo = this.Target.Velocity;
            var targetVeloAngle = this.Target.Velocity.Angle();

            var missileVelo = this.Missile.Velocity;
            var missileVeloMag = this.Missile.Velocity.Length();
            var missileVeloAngle = this.Missile.Velocity.Angle();

            var deltaV = missileVeloMag - _prevVelo;
            _prevVelo = missileVeloMag;

            if (_prevTargPos == D2DPoint.Zero)
            {
                _prevTargPos = targetPosition;
                return missileVeloAngle;
            }
            _prevTargPos = targetPosition;

            var targDist = D2DPoint.Distance(this.Missile.Position, targetPosition);
            var timeToImpact = Utilities.ImpactTime(this.Missile, this.Target);

            // Set initial impact point directly on the target.
            var impactPnt = targetPosition;

            // Refine the impact point when able.
            // Where will the target be when we arrive?
            if (Missile.Age > 0f)
            {
                var targAngleDelta = targetVeloAngle - _prevTargVeloAngle;
                _prevTargVeloAngle = targetVeloAngle;

                var relVelo = (missileVelo - targetVelo).Length();
                var framesToImpact = ImpactTime(targDist, (relVelo * dt), (deltaV * dt));
                var predictedPoint = RefineImpact(targetPosition, targetVelo, targAngleDelta, framesToImpact, dt);
                impactPnt = _predictSmooth.Add(predictedPoint);
            }

            // Compute the speed (delta) of the impact point as it is refined.
            // Slower sleep = higher confidence.
            var impactPntDelta = D2DPoint.Distance(_prevImpactPnt, impactPnt);
            _prevImpactPnt = impactPnt;

            // Only update the stable aim point when the predicted impact point is moving slowly.
            // If it begins to move quickly (when the target changes velo/direction) we keep targeting the previous point until it slows down again.
            var impactDeltaFact = Utilities.Factor(IMPACT_POINT_DELTA_THRESH, impactPntDelta);
            var stableAimPoint = D2DPoint.Lerp(StableAimPoint, impactPnt, impactDeltaFact);

            // Compute closing rate and lerp between the target and predicted locations.
            var closingRate = _closingRateSmooth.Add(_prevTargetDist - targDist);
            _prevTargetDist = targDist;

            // We gradually incorporate the predicted location as closing rate increases.
            var closeRateFact = Utilities.Factor(closingRate, MIN_CLOSE_RATE);
            var targetDir = (targetPosition - this.Missile.Position).Normalized();
            var predictedDir = (stableAimPoint - this.Missile.Position).Normalized();
            var aimDirection = D2DPoint.Lerp(targetDir, predictedDir, closeRateFact);

            // Compute rotation amount.
            var veloNorm = D2DPoint.Normalize(this.Missile.Velocity);
            var rotAmt = Utilities.RadsToDegrees(aimDirection.Cross(veloNorm * ROT_AMT_FACTOR));

            // Increase rotation rate modifier as we approach the target.
            var rotMod = 1f;

            if (timeToImpact > 0)
                rotMod = 1f + (1f - Utilities.FactorWithEasing(timeToImpact, ROT_MOD_TIME, EasingFunctions.Out.EaseCircle)) * ROT_MOD_AMT;

            // Offset our current rotation from our current velocity vector to compute the next rotation.
            var nextRot = missileVeloAngle + -(rotAmt * rotMod);

            // Tracking info.
            ImpactPoint = impactPnt; // Red
            StableAimPoint = stableAimPoint; // Blue
            CurrentAimPoint = D2DPoint.Lerp(targetPosition, stableAimPoint, closeRateFact); // Green

            return nextRot;
        }

        private float ImpactTime(float dist, float velo, float accel)
        {
            var finalVelo = MathF.Sqrt(MathF.Abs(MathF.Pow(velo, 2f) + (2f * accel) * dist));
            var impactTime = (finalVelo - velo) / accel;

            return impactTime;
        }

        private D2DPoint RefineImpact(D2DPoint targetPos, D2DPoint targetVelo, float targAngleDelta, float framesToImpact, float dt)
        {
            // To obtain a high order target position we basically run a small simulation here.
            // This considers the target velocity as well as the change in angular velocity.

            D2DPoint predicted = targetPos;

            if (framesToImpact > 5 && framesToImpact < MAX_FTI)
            {
                var targLoc = targetPos;
                var angle = targetVelo.Angle();

                // Advance the target position and velocity angle.
                for (int i = 0; i <= framesToImpact; i++)
                {
                    var avec = Utilities.AngleToVectorDegrees(angle) * targetVelo.Length();
                    targLoc += avec * dt;
                    angle += targAngleDelta * dt;
                    angle = Utilities.ClampAngle(angle);
                }

                // Include the remainder after the loop.
                var rem = framesToImpact % (int)framesToImpact;
                angle += targAngleDelta * rem * dt;
                angle = Utilities.ClampAngle(angle);
                targLoc += (Utilities.AngleToVectorDegrees(angle) * targetVelo.Length()) * rem * dt;

                predicted = targLoc;
            }

            return predicted;
        }
    }
}
