using PolyPlane.GameObjects;

namespace PolyPlane.AI_Behavior
{
    // Target practice...
    public sealed class DummyAI : IAIBehavior
    {
        public FighterPlane Plane => _plane;
        public FighterPlane TargetPlane => throw new NotImplementedException();
        public AIPersonality Personality { get; set; }

        private FighterPlane _plane;
        private FighterPlane _targetPlane;

        public DummyAI(FighterPlane plane)
        {
            _plane = plane;
        }

        public void ChangeTarget(FighterPlane plane)
        {
        }

        public void ClearTarget()
        {
        }

        public float GetAIGuidanceDirection(float dt)
        {
            return 0f;
        }

        public void Update(float dt)
        {
            if (this.Plane.Position.X < -100f || this.Plane.Position.X > 5000f)
            {
                this.Plane.SetPosition(new D2DPoint(0f, -4000f), 0f);
                this.Plane.Velocity = new D2DPoint(500f, 0f);
            }

            if (this.Plane.IsDisabled)
            {
                this.Plane.SetPosition(new D2DPoint(0f, 0f), 0f);
                this.Plane.AIRespawnReady = true;
            }
        }
    }
}
