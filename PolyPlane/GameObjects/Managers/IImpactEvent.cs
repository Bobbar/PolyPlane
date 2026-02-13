namespace PolyPlane.GameObjects.Managers
{
    public interface IImpactEvent
    {
        event EventHandler<ImpactEvent> ImpactEvent;
    }

    public sealed class ImpactEvent
    {
        public GameObject Attacker;
        public GameObject Target;
        public bool DidDamage = false;

        public ImpactEvent(GameObject target, GameObject attacker, bool didDamage)
        {
            Target = target;
            Attacker = attacker;
            DidDamage = didDamage;
        }
    }
}
