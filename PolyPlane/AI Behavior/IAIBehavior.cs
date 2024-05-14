using PolyPlane.GameObjects;


namespace PolyPlane.AI_Behavior
{
    public interface IAIBehavior
    {
        FighterPlane Plane { get; }
        FighterPlane TargetPlane { get; }
        float GetAIGuidance();
        void ChangeTarget(FighterPlane plane);
        void Update(float dt);

    }
}
