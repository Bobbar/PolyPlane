using PolyPlane.GameObjects.Interfaces;
using PolyPlane.Helpers;
using PolyPlane.Rendering;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public class FlamePart : GameObject, ICollidable, INoGameID
    {
        public D2DEllipse Ellipse => _ellipse;
        public D2DColor Color { get; set; }
        public D2DColor StartColor { get; set; }
        public D2DColor EndColor { get; set; }

        private D2DEllipse _ellipse;

        private const float MIN_RISE_RATE = -50f;
        private const float MAX_RISE_RATE = -70f;
        private const float WIND_SPEED = 20f; // Fake wind effect amount.

        private D2DPoint _riseRate;

        public FlamePart()
        {
            _ellipse = new D2DEllipse();
            this.RenderOrder = 0;
        }

        public void ReInit(D2DPoint pos, float radius, D2DColor endColor, D2DPoint velo)
        {
            this.Age = 0f;
            this.IsExpired = false;

            _ellipse.origin = pos;
            _ellipse.radiusX = radius;
            _ellipse.radiusY = radius;

            var newColor = new D2DColor(FlameEmitter.DefaultFlameColor.a, 1f, Utilities.Rnd.NextFloat(0f, 0.86f), FlameEmitter.DefaultFlameColor.b);

            Color = newColor;
            StartColor = newColor;
            EndColor = endColor;

            _riseRate = new D2DPoint(0f, Utilities.Rnd.NextFloat(MAX_RISE_RATE, MIN_RISE_RATE));

            this.Position = _ellipse.origin;
            this.Velocity = velo;
        }

        public override void Update(float dt)
        {
            base.Update(dt);

            var ageFactFade = 1f - Utilities.Factor(this.Age, FlameEmitter.MAX_AGE);
            var ageFactSmoke = Utilities.Factor(this.Age, FlameEmitter.MAX_AGE * 3f);
            var alpha = StartColor.a * ageFactFade;

            this.Color = Utilities.LerpColorWithAlpha(this.Color, this.EndColor, ageFactSmoke, alpha);
            this.Velocity += -this.Velocity * 0.9f * dt;
            this.Velocity += _riseRate * dt;

            // Simulate the particles being blown by the wind.
            _riseRate.X = WIND_SPEED * Utilities.FactorWithEasing(this.Age, FlameEmitter.MAX_AGE, EasingFunctions.EaseOutSine);

            _ellipse.origin = this.Position;

            if (this.Age > FlameEmitter.MAX_AGE)
                this.IsExpired = true;
        }

        public override void Render(RenderContext ctx)
        {
            base.Render(ctx);

            ctx.FillEllipse(Ellipse, Color);
        }

        public override void Dispose()
        {
            base.Dispose();

            World.ObjectManager.ReturnFlamePart(this);
        }
    }
}
