namespace PolyPlane.AI_Behavior
{
    [Flags]
    public enum AIPersonality
    {
        Normal = 1, // No special behavior.
        MissileHappy = 2, // Shorter time between missile launches.
        LongBursts = 4, // Longer burst time.
        Cowardly = 8, // Runs away and keeps distance from target plane. (Slower too)
        Speedy = 16, // Has a little more thrust. (Faster)
        Vengeful = 32, // Targets the plane which killed it last.
        TargetTopPlanes = 64, // Targets the top scoring players.
    }
}
