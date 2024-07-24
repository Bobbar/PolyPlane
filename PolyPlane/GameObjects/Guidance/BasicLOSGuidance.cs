using PolyPlane.Helpers;

namespace PolyPlane.GameObjects.Guidance
{
    public class BasicLOSGuidance : GuidanceBase
    {
        public BasicLOSGuidance(GuidedMissile missile, GameObject target) : base(missile, target)
        { }

        public override float GetGuidanceDirection(float dt)
        {
            const float pValue = 10f;

            var targetPos = GetTargetPosition();
            var targDist = D2DPoint.Distance(targetPos, this.Missile.Position);

            var navigationTime = targDist / (this.Missile.Velocity.Length() * dt);
            var los = (targetPos + ((Target.Velocity * dt) * navigationTime)) - this.Missile.Position;

            var angle = this.Missile.Velocity.AngleBetween(los, true);
            var adjustment = pValue * angle * los;

            var leadRotation = adjustment.Angle(true);
            var targetRot = leadRotation;

            ImpactPoint = (targetPos + Target.Velocity * navigationTime);

            return targetRot;
        }
    }
}
