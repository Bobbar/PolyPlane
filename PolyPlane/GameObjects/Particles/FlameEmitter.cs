using PolyPlane.GameObjects.Interfaces;
using unvell.D2DLib;

namespace PolyPlane.GameObjects.Particles
{
    public class FlameEmitter : ParticleEmitter, INoGameID
    {
        public FlameEmitter(GameObject obj, D2DPoint offset, float minRadius, float maxRadius, bool startImmediately = true) : base(obj, offset, minRadius, maxRadius, startImmediately)
        { }

        public override D2DColor GetEndColor()
        {
            if (Owner is FighterPlane plane && plane.IsDisabled)
                return World.BlackSmokeColor;
            else
                return World.GraySmokeColor;
        }

        public override D2DColor GetStartColor()
        {
            return World.GetRandomFlameColor();
        }
    }
}
