namespace PolyPlane.GameObjects.Manager
{
    public class PlaneImpactResult
    {
        public ImpactType Type;
        public D2DPoint ImpactPoint;
        public float ImpactAngle;
        public bool DoesDamage = false;
        public bool WasHeadshot = false;

        public PlaneImpactResult() { }

        public PlaneImpactResult(ImpactType type, D2DPoint impactPoint, float impactAngle, bool doesDamage, bool wasHeadshot)
        {
            Type = type;
            ImpactPoint = impactPoint;
            ImpactAngle = impactAngle;
            DoesDamage = doesDamage;
            WasHeadshot = wasHeadshot;
        }
    }

    public enum ImpactType
    {
        Bullet,
        Missile
    }
}
