using PolyPlane.GameObjects.Interfaces;
using PolyPlane.Helpers;
using PolyPlane.Rendering;
using System.Numerics;
using unvell.D2DLib;

namespace PolyPlane.GameObjects.Particles
{
    public sealed class Particle : GameObject, INoGameID, ILightMapContributor
    {
        public int Idx;
        public ParticleType Type;

        public D2DEllipse Ellipse = new D2DEllipse();
        public D2DColor Color { get; set; }
        public D2DColor StartColor { get; set; }
        public D2DColor EndColor { get; set; }

        public float Radius
        {
            get
            {
                return Ellipse.radiusX;
            }

            set
            {
                Ellipse.radiusX = value;
                Ellipse.radiusY = value;
            }
        }

        public float MaxAge = 1f;
        public float TargetRadius = 0f;

        private D2DPoint RiseRate;

        const float DEFAULT_MAX_AGE = 30f;
        const float MIN_RISE_RATE = -50f;
        const float MAX_RISE_RATE = -70f;
        const float WIND_SPEED = 20f; // Fake wind effect amount.
        const float PARTICLE_MASS = 30f;
        const float PART_GROW_AGE = 0.3f; // Age at which particle will grow to its full size.

        private static readonly Vector3 Luminance = new Vector3(0.2126f, 0.7152f, 0.0722f); // For flame particle lighting amount.

        public Particle()
        {
            Ellipse = new D2DEllipse();
            RenderOrder = 0;
            Mass = PARTICLE_MASS;
            Flags = GameObjectFlags.SpatialGrid | GameObjectFlags.AeroPushable | GameObjectFlags.ExplosionImpulse | GameObjectFlags.BounceOffGround;
        }

        public override void DoUpdate(float dt)
        {
            base.DoUpdate(dt);

            switch (this.Type)
            {
                case ParticleType.Flame or ParticleType.Dust:

                    Velocity += -Velocity * 0.8f * dt;
                    Velocity += RiseRate * dt;

                    // Simulate the particles being blown by the wind.
                    RiseRate.X = WIND_SPEED * Utilities.FactorWithEasing(Age, MaxAge, EasingFunctions.Out.EaseQuad);

                    break;

                case ParticleType.Smoke or ParticleType.Vapor:

                    float dtFact = 2f;

                    if (Type == ParticleType.Smoke)
                        dtFact = 3f;

                    Velocity += -Velocity * (dt * dtFact);

                    break;
            }

            Ellipse.origin = Position;

            if (Age > MaxAge && !IsExpired)
            {
                // Add our index to the queue to be cleaned up later.
                World.ObjectManager.ExpiredParticleIdxs.Enqueue(Idx);

                IsExpired = true;
            }
        }

        public override void Render(RenderContext ctx)
        {
            base.Render(ctx);

            switch (this.Type)
            {
                case ParticleType.Flame or ParticleType.Dust:

                    var ageFactFade = 1f - Utilities.Factor(Age, MaxAge);
                    var alpha = StartColor.a * ageFactFade;
                    var ageFactSmoke = Utilities.Factor(Age * 4f, MaxAge);

                    // Gradually grow until we reach the target radius.
                    var ageFactGrow = Utilities.Factor(Age, PART_GROW_AGE);
                   
                    Radius = TargetRadius * ageFactGrow;
                    Color = Utilities.LerpColorWithAlpha(StartColor, EndColor, ageFactSmoke, alpha);

                    ctx.FillEllipseWithLighting(Ellipse, Color, maxIntensity: 0.6f);

                    break;

                case ParticleType.Smoke or ParticleType.Vapor:

                    var ageFact = 1f - Utilities.Factor(Age, MaxAge);
                    var radAmt = EasingFunctions.Out.EaseQuintic(ageFact);
                    var rad = (TargetRadius * radAmt);
                    var alphaSmoke = StartColor.a * ageFact;

                    Color = new D2DColor(alphaSmoke, StartColor);
                    Radius = rad;

                    ctx.FillEllipse(Ellipse, Color);

                    break;
            }
        }

        public override void Dispose()
        {
            base.Dispose();

            World.ObjectManager.ReturnParticle(this);
        }

        public static Particle SpawnParticle(GameObject owner, D2DPoint pos, D2DPoint velo, float radius, D2DColor startColor, D2DColor endColor, ParticleType type = ParticleType.Flame)
        {
            // Rent a particle and set the new properties.
            var particle = World.ObjectManager.RentParticle();

            particle.RenderOrder = 0;

            particle.Type = type;

            // Disable aero push effects for vapors.
            if (type == ParticleType.Vapor)
                particle.RemoveFlag(GameObjectFlags.AeroPushable);
            else
                particle.AddFlag(GameObjectFlags.AeroPushable);

            particle.Owner = owner;

            particle.MaxAge = DEFAULT_MAX_AGE + Utilities.Rnd.NextFloat(-5f, 5f);
            particle.Age = 0f;
            particle.IsExpired = false;

            particle.TargetRadius = radius;
            particle.Ellipse.origin = pos;
            particle.Ellipse.radiusX = radius;
            particle.Ellipse.radiusY = radius;

            particle.Color = startColor;
            particle.StartColor = startColor;
            particle.EndColor = endColor;

            particle.RiseRate = new D2DPoint(0f, Utilities.Rnd.NextFloat(MAX_RISE_RATE, MIN_RISE_RATE));

            particle.Position = particle.Ellipse.origin;
            particle.Velocity = velo;

            //World.ObjectManager.AddParticle(particle);
            World.ObjectManager.EnqueueParticle(particle);

            return particle;
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
        Dust,
        Smoke,
        Vapor
    }
}
