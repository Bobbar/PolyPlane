using PolyPlane.GameObjects;


namespace PolyPlane.AI_Behavior
{
    public interface IAIBehavior : ISkipFramesUpdate
    {
        FighterPlane Plane { get; }
        FighterPlane TargetPlane { get; }
        float GetAIGuidance();
        void ChangeTarget(FighterPlane plane);

    }
}
