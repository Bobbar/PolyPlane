using PolyPlane.Helpers;

namespace PolyPlane.GameObjects.Guidance
{
    public class SimplePNGuidance : GuidanceBase
    {
        public SimplePNGuidance(Missile missile, GameObject target) : base(missile, target)
        { }

        public override float GetGuidanceDirection(float dt)
        {
            const float pValue = 5f;

            var target = GetTargetPosition();
            var los = target - this.Missile.Position;
            var navigationTime = los.Length() / (this.Missile.Velocity.Length() * dt);
            var targRelInterceptPos = los + ((Target.Velocity * dt) * navigationTime);

            ImpactPoint = targRelInterceptPos;
            targRelInterceptPos *= pValue;

            var leadRotation = ((target + targRelInterceptPos) - this.Missile.Position).Angle(true);
            var targetRot = leadRotation;

            return targetRot;
        }
    }
}
