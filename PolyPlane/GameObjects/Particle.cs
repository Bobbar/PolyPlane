using PolyPlane.GameObjects.Interfaces;
using PolyPlane.Helpers;
using PolyPlane.Rendering;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public class Particle : GameObject, ICollidable, IPushable, INoGameID
    {
        public D2DEllipse Ellipse = new D2DEllipse();
        public D2DColor Color { get; set; }
        public D2DColor StartColor { get; set; }
        public D2DColor EndColor { get; set; }

        protected float MaxAge = 1f;
        protected D2DPoint RiseRate;

        protected const float DEFAULT_MAX_AGE = 30f;
        protected const float MIN_RISE_RATE = -50f;
        protected const float MAX_RISE_RATE = -70f;
        protected const float WIND_SPEED = 20f; // Fake wind effect amount.
        protected const float PARTICLE_MASS = 30f;

        public Particle()
        {
            Ellipse = new D2DEllipse();
            this.RenderOrder = 0;
            this.Mass = PARTICLE_MASS;
        }

        public override void Update(float dt)
        {
            base.Update(dt);

            var ageFactFade = 1f - Utilities.Factor(this.Age, MaxAge);
            var ageFactSmoke = Utilities.Factor(this.Age, MaxAge * 3f);
            var alpha = StartColor.a * ageFactFade;

            this.Color = Utilities.LerpColorWithAlpha(this.Color, this.EndColor, ageFactSmoke, alpha);
            this.Velocity += -this.Velocity * 0.8f * dt;
            this.Velocity += RiseRate * dt;

            // Simulate the particles being blown by the wind.
            RiseRate.X = WIND_SPEED * Utilities.FactorWithEasing(this.Age, MaxAge, EasingFunctions.EaseOutSine);

            this.Ellipse.origin = this.Position;

            if (this.Age > MaxAge)
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

            World.ObjectManager.ReturnParticle(this);
        }

        public static void SpawnParticle(GameObject owner, D2DPoint pos, D2DPoint velo, float radius, D2DColor startColor, D2DColor endColor)
        {
            // Rent a particle and set the new properties.
            var particle = World.ObjectManager.RentParticle();

            particle.Owner = owner;

            particle.MaxAge = DEFAULT_MAX_AGE + Utilities.Rnd.NextFloat(-5f, 5f);
            particle.Age = 0f;
            particle.IsExpired = false;

            particle.Ellipse.origin = pos;
            particle.Ellipse.radiusX = radius;
            particle.Ellipse.radiusY = radius;

            particle.Color = startColor;
            particle.StartColor = startColor;
            particle.EndColor = endColor;

            particle.RiseRate = new D2DPoint(0f, Utilities.Rnd.NextFloat(MAX_RISE_RATE, MIN_RISE_RATE));

            particle.Position = particle.Ellipse.origin;
            particle.Velocity = velo;

            World.ObjectManager.EnqueueParticle(particle);
        }
    }
}
