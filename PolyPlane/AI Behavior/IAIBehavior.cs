using PolyPlane.GameObjects;


namespace PolyPlane.AI_Behavior
{
    public interface IAIBehavior : ISkipFramesUpdate
    {
        Plane Plane { get; }
        Plane TargetPlane { get; }
        float GetAIGuidance();


    }
}
