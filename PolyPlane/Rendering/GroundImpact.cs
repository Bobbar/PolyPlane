using unvell.D2DLib;

namespace PolyPlane.Rendering
{
    public sealed class GroundImpact
    {
        public const float MAX_AGE = 1600f;
        public const float START_FADE_AGE = MAX_AGE - (MAX_AGE * 0.125f);

        public D2DPoint Position;
        public D2DSize Size;
        public float Angle;
        public float Age = 0f;

        public GroundImpact() { }

        public void ReInit(D2DPoint pos, D2DSize size, float angle)
        {
            Age = 0f;
            Position = pos;
            Size = size;
            Angle = angle;
        }
    }
}
