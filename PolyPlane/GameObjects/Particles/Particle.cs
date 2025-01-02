using PolyPlane.GameObjects.Interfaces;
using PolyPlane.Helpers;
using PolyPlane.Rendering;
using System.Numerics;
using unvell.D2DLib;

namespace PolyPlane.GameObjects.Particles
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
            RenderOrder = 0;
            Mass = PARTICLE_MASS;
        }

        public override void Update(float dt)
        {
            base.Update(dt);

            var ageFactFade = 1f - Utilities.Factor(Age, MaxAge);
            var ageFactSmoke = Utilities.Factor(Age, MaxAge * 3f);
            var alpha = StartColor.a * ageFactFade;

            Color = Utilities.LerpColorWithAlpha(Color, EndColor, ageFactSmoke, alpha);
            Velocity += -Velocity * 0.8f * dt;
            Velocity += RiseRate * dt;

            // Simulate the particles being blown by the wind.
            RiseRate.X = WIND_SPEED * Utilities.FactorWithEasing(Age, MaxAge, EasingFunctions.Out.EaseQuad);

            Ellipse.origin = Position;

            if (Age > MaxAge)
                IsExpired = true;
        }

        public override void Render(RenderContext ctx)
        {
            base.Render(ctx);

            var color = Color;

            ctx.FillEllipseWithLighting(Ellipse, color, maxIntensity: 0.6f);
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
            return Color.WithBrightness(2.5f);
        }

        float ILightMapContributor.GetIntensityFactor()
        {
            return 0.3f;
        }

        D2DPoint ILightMapContributor.GetLightPosition()
        {
            return Position;
        }

        bool ILightMapContributor.IsLightEnabled()
        {
            const float MIN_LUM = 0.19f;
            const float MAX_LUM = 0.4f;

            if (Type != ParticleType.Flame)
                return false;

            // Compute and filter based on luminance.
            var c = Color;
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
