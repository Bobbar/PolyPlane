using PolyPlane.Helpers;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public class BulletHole : Flame
    {
        public D2DSize HoleSize { get; set; }
        public override float Rotation
        {
            get => base.Rotation + _rotOffset;
            set => base.Rotation = value;
        }

        private const float MIN_HOLE_SZ = 2f;
        private const float MAX_HOLE_SZ = 6f;
        private float _rotOffset = 0f;

        public BulletHole(GameObject obj, D2DPoint offset, bool hasFlame = true) : base(obj, offset, hasFlame)
        {
            HoleSize = new D2DSize(Utilities.Rnd.NextFloat(MIN_HOLE_SZ, MAX_HOLE_SZ), Utilities.Rnd.NextFloat(MIN_HOLE_SZ, MAX_HOLE_SZ));
            _rotOffset = Utilities.Rnd.NextFloat(0f, 180f);
        }

        public BulletHole(GameObject obj, D2DPoint offset, float angle, bool hasFlame = true) : base(obj, offset, hasFlame)
        {
            // Fudge the hole size to ensure it's elongated in the Y direction.
            HoleSize = new D2DSize(Utilities.Rnd.NextFloat(MIN_HOLE_SZ + 2, MAX_HOLE_SZ + 2), Utilities.Rnd.NextFloat(MIN_HOLE_SZ, MAX_HOLE_SZ - 3));
            _rotOffset = angle;
        }

        public void FlipY()
        {
            base.FlipY();
            _rotOffset = Utilities.ClampAngle(_rotOffset * -1f);
        }
    }
}
