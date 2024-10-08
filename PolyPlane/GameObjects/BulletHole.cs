﻿using PolyPlane.Helpers;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public class BulletHole : FlameEmitter
    {
        public D2DSize HoleSize { get; set; }
        public D2DSize OuterHoleSize { get; set; }
        public D2DColor Color { get; set; }

        public override float Rotation
        {
            get => base.Rotation + _rotOffset;
            set => base.Rotation = value;
        }

        private const float MIN_HOLE_SZ = 2f;
        private const float MAX_HOLE_SZ = 6f;
        private float _rotOffset = 0f;

        public BulletHole(GameObject obj, D2DPoint offset, float angle, bool hasFlame = true) : base(obj, offset, hasFlame)
        {
            // Fudge the hole size to ensure it's elongated in the Y direction.
            HoleSize = new D2DSize(Utilities.Rnd.NextFloat(MIN_HOLE_SZ + 2, MAX_HOLE_SZ + 2), Utilities.Rnd.NextFloat(MIN_HOLE_SZ, MAX_HOLE_SZ - 3));

            var outerDiff = Utilities.Rnd.NextFloat(0.7f, 3f);
            OuterHoleSize = new D2DSize(HoleSize.width + outerDiff, HoleSize.height + outerDiff);

            Color = GetColor();

            _rotOffset = angle;
        }

        public override void FlipY()
        {
            base.FlipY();
            _rotOffset = Utilities.ClampAngle(_rotOffset * -1f);
            this.Update(0f);
        }

        private D2DColor GetColor()
        {
            var color = D2DColor.White.WithBrightness(Utilities.Rnd.NextFloat(0.3f, 0.6f));
            return color;
        }
    }
}
