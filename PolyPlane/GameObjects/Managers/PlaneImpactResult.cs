﻿namespace PolyPlane.GameObjects.Manager
{
    public class PlaneImpactResult
    {
        public ImpactType Type;
        public D2DPoint ImpactPoint;
        public float ImpactAngle;
        public bool WasHeadshot = false;
        public float DamageAmount = 0f;

        public PlaneImpactResult() { }

        public PlaneImpactResult(ImpactType type, D2DPoint impactPoint, float impactAngle, float damageAmount, bool wasHeadshot)
        {
            Type = type;
            ImpactPoint = impactPoint;
            ImpactAngle = impactAngle;
            DamageAmount = damageAmount;
            WasHeadshot = wasHeadshot;
        }
    }

    public enum ImpactType
    {
        Bullet,
        Missile,
        Splash
    }
}
