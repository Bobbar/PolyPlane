namespace PolyPlane.GameObjects
{
    public abstract class Missile : GameObjectPoly, ICollidable
    {
        public float DistTraveled = 0f;
        private D2DPoint _prevPos = D2DPoint.Zero;
        public GameObject Target { get; set; }

        public Missile() { }

        public Missile(D2DPoint pos) : base(pos)
        {
            _prevPos = pos;
        }

        public Missile(D2DPoint pos, D2DPoint velo) : base(pos, velo)
        {
            _prevPos = pos;
        }

        public Missile(D2DPoint pos, D2DPoint velo, float rotation, GameObject owner, GameObject target) : base(pos, velo, rotation)
        {
            Owner = owner;
            Target = target;
            _prevPos = pos;
        }

        public override void Update(float dt, float renderScale)
        {
            base.Update(dt, renderScale);

            var dist = D2DPoint.Distance(this.Position, _prevPos);
            _prevPos = this.Position;
            DistTraveled += dist;
        }
    }
}
