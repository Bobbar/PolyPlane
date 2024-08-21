namespace PolyPlane.GameObjects.Manager
{
    public interface IImpactEvent
    {
        event EventHandler<ImpactEvent> ImpactEvent;
    }

    public class ImpactEvent
    {
        public GameObject Target;
        public GameObject Impactor;
        public bool DoesDamage = false;

        public ImpactEvent(GameObject target, GameObject impactor, bool doesDamage)
        {
            Target = target;
            Impactor = impactor;
            DoesDamage = doesDamage;
        }
    }
}
