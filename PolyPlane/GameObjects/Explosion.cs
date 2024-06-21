using PolyPlane.Helpers;
using PolyPlane.Rendering;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public class Explosion : GameObject
    {
        public float MaxRadius { get; set; } = 100f;
        public float Duration { get; set; } = 1f;

        public float Radius => _currentRadius;

        private float _currentRadius = 0f;
        private D2DColor _color = new D2DColor(0.4f, D2DColor.Orange);
        private List<Flame> _flames = new List<Flame>();

        public Explosion(D2DPoint pos, float maxRadius, float duration) : base(pos)
        {
            this.MaxRadius = maxRadius;
            this.Duration = duration;
            this.PlayerID = 0;

            _color.r = _rnd.NextFloat(0.8f, 1f);

            int NUM_FLAME = (int)(maxRadius / 6f);

            for (int i = 0; i < NUM_FLAME; i++)
            {
                var pnt = Utilities.RandomPointInCircle(this.Position, 5f);
                var velo = Utilities.AngleToVectorDegrees(Utilities.RandomDirection(), Utilities.Rnd.NextFloat(maxRadius, maxRadius * 2f));
                var radius = NUM_FLAME + Utilities.Rnd.NextFloat(-10f, 10f);
                _flames.Add(new Flame(this, pnt, velo, radius));
            }

            _flames.ForEach(f => f.StopSpawning());
        }

        public override void Update(float dt, float renderScale)
        {
            base.Update(dt, renderScale);

            if (_currentRadius < MaxRadius)
                _currentRadius = MaxRadius * EasingFunctions.EaseOutBack(this.Age / Duration);

            if (this.Age >= Flame.MAX_AGE)
                this.IsExpired = true;

            _flames.ForEach(f => f.Update(dt, renderScale));
        }

        public override void Render(RenderContext ctx)
        {
            base.Render(ctx);

            if (this.Age < Duration)
                ctx.FillEllipse(new D2DEllipse(this.Position, new D2DSize(_currentRadius, _currentRadius)), _color);

            _flames.ForEach(f => f.Render(ctx));
        }

        public override void Dispose()
        {
            base.Dispose();

            _flames.ForEach(f => f.Dispose());

            _flames.Clear();
            _flames = null;
        }
    }
}
