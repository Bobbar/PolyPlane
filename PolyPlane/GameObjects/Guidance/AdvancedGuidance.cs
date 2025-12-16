using PolyPlane.Helpers;

namespace PolyPlane.GameObjects.Guidance
{
    /// <summary>
    /// My own implementation of missile guidance. This logic trades computational complexity for accuracy, and tries to find a more efficient guidance solution.
    /// </summary>
    public class AdvancedGuidance : GuidanceBase
    {
        private D2DPoint _prevImpactPnt = D2DPoint.Zero;
        private D2DPoint _currentAimPnt = D2DPoint.Zero;
        private D2DPoint _prevTargVelo = D2DPoint.Zero;
        private float _prevVelo = 0f;
        private SmoothPoint _predictSmooth = new SmoothPoint(20);

        public AdvancedGuidance(GuidedMissile missile, GameObject target) : base(missile, target)
        {
            var targPos = GetTargetPosition();
            _currentAimPnt = targPos;
            _predictSmooth.Add(targPos);
        }

        public override float GetGuidanceDirection(float dt)
        {
            const float MIN_TRACK_SPEED = 1000f;
            const float MAX_TRACK_SPEED = 10000f;
            const float MAX_TRACK_SPD_DIST = 10000f;

            var targetPosition = GetTargetPosition();
            var targetVelo = this.Target.Velocity;
            var missileVelo = this.Missile.Velocity;
            var missileVeloMag = this.Missile.Velocity.Length();
            var missileVeloAngle = this.Missile.Velocity.Angle();
            var targDist = D2DPoint.Distance(this.Missile.Position, targetPosition);

            var deltaV = missileVeloMag - _prevVelo;
            _prevVelo = missileVeloMag;

            // Set initial impact point.
            var impactPnt = _predictSmooth.Current;

            // Refine the impact point when able.
            // Where will the target be when we arrive?
            if (Missile.Age > 0f)
            {
                var targVeloDelta = targetVelo - _prevTargVelo;
                _prevTargVelo = targetVelo;

                var relVelo = (missileVelo - targetVelo).Length();
                var arrivalTime = PredictArrivalTime(targDist, relVelo, deltaV, dt);

                if (arrivalTime > 0f)
                {
                    var predictedPoint = PredictTargetLocation(targetPosition, targetVelo, targVeloDelta, arrivalTime);
                    impactPnt = _predictSmooth.Add(predictedPoint);
                }
            }

            // Compute the speed (delta) of the impact point as it is refined.
            // Slower sleep = higher confidence.
            var impactPntDelta = D2DPoint.Distance(_prevImpactPnt, impactPnt);
            _prevImpactPnt = impactPnt;

            if (_currentAimPnt == D2DPoint.Zero)
                _currentAimPnt = impactPnt;

            // Increase tracking rate as the disparity increases.
            var trackDist = _currentAimPnt.DistanceTo(impactPnt);
            var trackSpdFactor = Utilities.Factor(trackDist, MAX_TRACK_SPD_DIST);
            var trackSpeed = Utilities.Lerp(MIN_TRACK_SPEED, MAX_TRACK_SPEED, trackSpdFactor);

            // Gradually move the current aim point towards the predicted impact point.
            // This is to smooth out large deviations in the prediction logic.
            _currentAimPnt = Utilities.MoveTowardsPoint(_currentAimPnt, impactPnt, trackSpeed * dt);

            // Compute the final aim direction vector.
            var aimDirectionVec = (_currentAimPnt - this.Missile.Position).Normalized();

            // Convert aim direction vector to angle.
            var aimAngle = aimDirectionVec.Angle();

            // Tracking info.
            ImpactPoint = impactPnt; // Red
            CurrentAimPoint = _currentAimPnt; // Green

            return aimAngle;
        }

        private float PredictArrivalTime(float dist, float velo, float accel, float dt)
        {
            velo *= dt;
            accel *= dt;
            var finalVelo = MathF.Sqrt(MathF.Abs(MathF.Pow(velo, 2f) + (2f * accel) * dist));
            var arrivalTime = ((finalVelo - velo) / accel) * dt;

            return arrivalTime;
        }

        private D2DPoint PredictTargetLocation(D2DPoint targetPos, D2DPoint targetVelo, D2DPoint targAngleDelta, float navTime)
        {
            // Compute the final target location given the current position, velocity, velocity delta and navigation time.
            var targLoc = targetPos;
            var deltaVec = targetVelo + (targAngleDelta * navTime);

            targLoc += deltaVec * navTime;

            return targLoc;
        }
    }
}
