using PolyPlane.GameObjects;


namespace PolyPlane.AI_Behavior
{
    public interface IAIBehavior
    {
        Plane Plane { get; }
        Plane PlayerPlane { get; }
        void Update(float dt);
        float GetAIGuidance();


    }
}
