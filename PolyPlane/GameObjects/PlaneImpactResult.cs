using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PolyPlane.GameObjects
{
    public class PlaneImpactResult
    {
        public ImpactType Type;
        public D2DPoint ImpactPoint;
        public bool DoesDamage = false;
        public bool WasHeadshot = false;

        public PlaneImpactResult() { }

        public PlaneImpactResult(ImpactType type, D2DPoint impactPoint, bool doesDamage) 
        { 
            Type = type;
            ImpactPoint = impactPoint;
            DoesDamage = doesDamage;
        }
    }

    public enum ImpactType
    {
        Bullet,
        Missile
    }
}
