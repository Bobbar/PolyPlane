using PolyPlane.Net;

namespace PolyPlane.GameObjects.Managers
{
    public class PlaneImpactResult
    {
        public FighterPlane TargetPlane;
        public GameObject ImpactorObject;
        public ImpactType ImpactType;
        public D2DPoint ImpactPoint;
        public D2DPoint ImpactPointOrigin;
        public float ImpactAngle;
        public bool WasHeadshot => HasFlag(ImpactType.Headshot);
        public bool WasFlipped;
        public float DamageAmount = 0f;
        public float NewHealth = 0f;

        public PlaneImpactResult() { }

        public PlaneImpactResult(ImpactType type, D2DPoint impactPoint, float impactAngle, float damageAmount, bool wasHeadshot)
        {
            ImpactType = type;
            ImpactPoint = impactPoint;
            ImpactAngle = impactAngle;
            DamageAmount = damageAmount;

            if (wasHeadshot)
                ImpactType |= ImpactType.Headshot;
        }

        public PlaneImpactResult(ImpactPacket impactPacket)
        {
            ImpactType = impactPacket.ImpactType;
            ImpactPoint = impactPacket.ImpactPoint;
            ImpactPointOrigin = impactPacket.ImpactPointOrigin;
            ImpactAngle = impactPacket.ImpactAngle;
            DamageAmount = impactPacket.DamageAmount;
            WasFlipped = impactPacket.WasFlipped;

            if (impactPacket.WasHeadshot)
                ImpactType |= ImpactType.Headshot;
        }

        public bool HasFlag(ImpactType flag)
        {
            return (ImpactType & flag) == flag;
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
        Headshot = 8,
        DamagedEngine = 16,
        DamagedTailWing = 32,
        DamagedMainWing = 64,
        DamagedGun = 128,
        Existing = 256
    }
}
