using PolyPlane.GameObjects;


namespace PolyPlane.AI_Behavior
{
    public interface IAIBehavior
    {
        FighterPlane Plane { get; }
        FighterPlane? TargetPlane { get; }
        AIPersonality Personality { get; }

        float GetAIGuidanceDirection(float dt);
        void ClearTarget();
        void ChangeTarget(FighterPlane plane);
        void Update(float dt);

    }
}
