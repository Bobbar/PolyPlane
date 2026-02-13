using PolyPlane.Helpers;

namespace PolyPlane.GameObjects.Guidance
{
    public sealed class SimplePNGuidance : GuidanceBase
    {
        public SimplePNGuidance(GuidedMissile missile, GameObject target) : base(missile, target)
        { }

        public override float GetGuidanceDirection(float dt)
        {
            const float pValue = 9f;

            var target = GetTargetPosition();
            var los = target - this.Missile.Position;
            var navigationTime = los.Length() / (this.Missile.Velocity.Length() * dt);
            var targRelInterceptPos = los + ((Target.Velocity * dt) * navigationTime);

            ImpactPoint = targRelInterceptPos;
            targRelInterceptPos *= pValue;

            var leadRotation = ((target + targRelInterceptPos) - this.Missile.Position).Angle();
            var targetRot = leadRotation;

            return targetRot;
        }
    }
}
