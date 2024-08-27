using PolyPlane.GameObjects;


namespace PolyPlane.AI_Behavior
{
    public interface IAIBehavior
    {
        FighterPlane Plane { get; }
        FighterPlane TargetPlane { get; }
        AIPersonality Personality { get; }

        float GetAIGuidance();
        void ClearTarget();
        void ChangeTarget(FighterPlane plane);
        void Update(float dt);

    }
}
