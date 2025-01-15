namespace PolyPlane.GameObjects.Manager
{
    public class PlaneImpactResult
    {
        public FighterPlane TargetPlane;
        public GameObject ImpactorObject;
        public ImpactType Type;
        public D2DPoint ImpactPoint;
        public float ImpactAngle;
        public bool WasHeadshot => (Type & ImpactType.Headshot) == ImpactType.Headshot;
        public bool WasFlipped;
        public float DamageAmount = 0f;
        public float NewHealth = 0f;

        public PlaneImpactResult() { }

        public PlaneImpactResult(ImpactType type, D2DPoint impactPoint, float impactAngle, float damageAmount, bool wasHeadshot)
        {
            Type = type;
            ImpactPoint = impactPoint;
            ImpactAngle = impactAngle;
            DamageAmount = damageAmount;

            if (wasHeadshot)
                Type |= ImpactType.Headshot;
        }

        public PlaneImpactResult(ImpactType type, D2DPoint impactPoint, float impactAngle, float damageAmount, bool wasHeadshot, bool wasFlipped)
        {
            Type = type;
            ImpactPoint = impactPoint;
            ImpactAngle = impactAngle;
            DamageAmount = damageAmount;
            WasFlipped = wasFlipped;

            if (wasHeadshot)
                Type |= ImpactType.Headshot;
        }
    }

    public class PlayerKilledEventArgs
    {
        public FighterPlane KilledPlane;
        public FighterPlane AttackPlane;
        public ImpactType ImpactType;

        public PlayerKilledEventArgs(FighterPlane killedPlane, FighterPlane attackPlane, ImpactType impactType)
        {
            KilledPlane = killedPlane;
            AttackPlane = attackPlane;
            ImpactType = impactType;
        }
    }

    [Flags]
    public enum ImpactType
    {
        Bullet = 1,
        Missile = 2,
        Splash = 4,
        Headshot = 8
    }
}
