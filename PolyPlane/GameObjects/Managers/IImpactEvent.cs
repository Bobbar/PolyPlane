namespace PolyPlane.GameObjects.Manager
{
    public interface IImpactEvent
    {
        event EventHandler<ImpactEvent> ImpactEvent;
    }

    public class ImpactEvent
    {
        public GameObject Attacker;
        public GameObject Target;

        public ImpactEvent(GameObject target, GameObject attacker)
        {
            Target = target;
            Attacker = attacker;
        }
    }
}
