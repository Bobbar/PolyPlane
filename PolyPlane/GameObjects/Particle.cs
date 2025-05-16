using PolyPlane.GameObjects.Interfaces;
using PolyPlane.Helpers;
using PolyPlane.Rendering;
using System.Numerics;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public class Particle : GameObject, ICollidable, IPushable, INoGameID, ILightMapContributor
    {
        public ParticleType Type;

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

        private static readonly Vector3 Luminance = new Vector3(0.2126f, 0.7152f, 0.0722f); // For flame particle lighting amount.

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
            RiseRate.X = WIND_SPEED * Utilities.FactorWithEasing(this.Age, MaxAge, EasingFunctions.Out.EaseQuad);

            this.Ellipse.origin = this.Position;

            if (this.Age > MaxAge)
                this.IsExpired = true;
        }

        public override void Render(RenderContext ctx)
        {
            base.Render(ctx);

            var color = Color;

            ctx.FillEllipseWithLighting(Ellipse, color, 0.6f);
        }

        public override void RenderGL(GLRenderContext ctx)
        {
            base.RenderGL(ctx);


            ctx.DrawCircleWithLighting(Ellipse.origin, Ellipse.radiusX, Color.ToSKColor(), 0.6f);
            //ctx.DrawCircle(Ellipse.origin, Ellipse.radiusX, Color.ToSKColor());
        }

        public override void Dispose()
        {
            base.Dispose();

            World.ObjectManager.ReturnParticle(this);
        }

        public static void SpawnParticle(GameObject owner, D2DPoint pos, D2DPoint velo, float radius, D2DColor startColor, D2DColor endColor, ParticleType type = ParticleType.Flame)
        {
            // Rent a particle and set the new properties.
            var particle = World.ObjectManager.RentParticle();

            particle.Type = type;
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

        float ILightMapContributor.GetLightRadius()
        {
            var flickerScale = Utilities.Rnd.NextFloat(0.5f, 1f);
            return 300f * flickerScale;
        }

        D2DColor ILightMapContributor.GetLightColor()
        {
            return this.Color.WithBrightness(2.5f);
        }

        float ILightMapContributor.GetIntensityFactor()
        {
            return 0.3f;
        }

        D2DPoint ILightMapContributor.GetLightPosition()
        {
            return this.Position;
        }

        bool ILightMapContributor.IsLightEnabled()
        {
            const float MIN_LUM = 0.19f;
            const float MAX_LUM = 0.4f;

            if (this.Type != ParticleType.Flame)
                return false;

            // Compute and filter based on luminance.
            var c = this.Color;
            var cVec = new Vector3(c.r, c.g, c.b);
            var brightness = Vector3.Dot(cVec, Luminance);

            return brightness > MIN_LUM && brightness < MAX_LUM;
        }
    }

    public enum ParticleType
    {
        Flame,
        Dust
    }
}
