using PolyPlane.GameObjects.Interfaces;
using PolyPlane.Helpers;
using PolyPlane.Rendering;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public class Explosion : GameObject, ICollidable, INoGameID
    {
        public float MaxRadius { get; set; } = 100f;
        public float MaxShockwaveRadius { get; set; } = 100f;
        public float Duration { get; set; } = 1f;
        public float Radius => _currentRadius;

        private float _currentRadius = 0f;
        private float _currentShockWaveRadius = 0f;
        private bool _hasShockWave = false;

        private D2DColor _color = new D2DColor(0.4f, D2DColor.Orange);
        private D2DColor _showckWaveColor = new D2DColor(1f, D2DColor.White);

        public Explosion(GameObject owner, float maxRadius, float duration) : base(owner.Position)
        {
            this.Owner = owner;
            this.MaxRadius = maxRadius;
            this.MaxShockwaveRadius = maxRadius * 6f;
            this.Duration = duration;
            this.PlayerID = owner.PlayerID;

            // If are right at ground level, spawn just below it.
            // Allows explosions to kick around debris and planes more.
            if (this.Position.Y == 0f)
                this.Position += new D2DPoint(0f, 10f);

            if (this.Owner == null || this.Owner is not Bullet)
                _hasShockWave = true;

            _color.r = _rnd.NextFloat(0.8f, 1f);

            int NUM_FLAME = (int)(maxRadius / 6f);

            for (int i = 0; i < NUM_FLAME; i++)
            {
                var pnt = this.Position + Utilities.RandomPointInCircle(5f);
                var velo = Utilities.AngleToVectorDegrees(Utilities.RandomDirection(), Utilities.Rnd.NextFloat(maxRadius, maxRadius * 2f));
                var radius = NUM_FLAME + Utilities.Rnd.NextFloat(-20f, 10f);
                radius = Math.Clamp(radius, 8f, 100f);
                var flame = World.ObjectManager.RentFlamePart();
                flame.ReInit(pnt, radius, FlameEmitter.BlackSmokeColor, velo);
                flame.Owner = this;

                World.ObjectManager.EnqueueFlame(flame);
            }
        }

        public override void Update(float dt)
        {
            base.Update(dt);

            _currentRadius = MaxRadius * Utilities.FactorWithEasing(this.Age, Duration, EasingFunctions.EaseOutElastic);
            _color.a = 1f - Utilities.FactorWithEasing(this.Age, Duration, EasingFunctions.EaseOutQuintic);

            if (_hasShockWave)
            {
                _currentShockWaveRadius = MaxShockwaveRadius * Utilities.FactorWithEasing(this.Age * 1f, Duration, EasingFunctions.EaseOutCirc);
                _showckWaveColor.a = 1f - Utilities.FactorWithEasing(this.Age * 1.5f, Duration, EasingFunctions.EaseOutExpo);
            }

            if (this.Age >= Duration)
                this.IsExpired = true;
        }

        public override void Render(RenderContext ctx)
        {
            base.Render(ctx);

            if (this.Age < Duration)
            {
                ctx.FillEllipse(new D2DEllipse(this.Position, new D2DSize(_currentRadius, _currentRadius)), _color);

                if (_hasShockWave)
                    ctx.DrawEllipse(new D2DEllipse(this.Position, new D2DSize(_currentShockWaveRadius, _currentShockWaveRadius)), _showckWaveColor, 20f);
            }

        }

        public override bool ContainedBy(D2DRect rect)
        {
            var ret = rect.Contains(new D2DEllipse(this.Position, new D2DSize(_currentShockWaveRadius, _currentShockWaveRadius)))
                   || rect.Contains(new D2DEllipse(this.Position, new D2DSize(_currentRadius, _currentRadius)));

            return ret;
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}
