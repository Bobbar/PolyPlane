using PolyPlane.GameObjects.Interfaces;
using PolyPlane.GameObjects.Particles;
using PolyPlane.Helpers;
using PolyPlane.Rendering;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public sealed class Explosion : GameObject, INoGameID, ILightMapContributor
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
        private static readonly D2DColor _lightMapColor = new D2DColor(1f, 0.96f, 0.67f, 0.26f);

        public Explosion() : base(GameObjectFlags.SpatialGrid)
        {
            this.RenderLayer = 7;
        }

        public void ReInit(GameObject owner, float maxRadius, float duration)
        {
            this.IsExpired = false;
            this.Age = 0f;
            this.Position = owner.Position;
            this.Owner = owner;
            this.MaxRadius = maxRadius;
            this.MaxShockwaveRadius = maxRadius * 6f;
            this.Duration = duration;
            this.PlayerID = owner.PlayerID;
            _hasShockWave = false;

            if (this.Owner == null || this.Owner is not Bullet)
                _hasShockWave = true;

            _color.r = Utilities.Rnd.NextFloat(0.8f, 1f);

            int NUM_FLAME = (int)(maxRadius / 6f);

            for (int i = 0; i < NUM_FLAME; i++)
            {
                var pnt = this.Position + Utilities.RandomPointInCircle(5f);
                var velo = Utilities.AngleToVectorDegrees(Utilities.RandomDirection(), Utilities.Rnd.NextFloat(maxRadius, maxRadius * 2f));
                var radius = NUM_FLAME + Utilities.Rnd.NextFloat(-20f, 10f);
                radius = Math.Clamp(radius, 8f, 100f);

                // Add a small amount of velocity from the owner object.
                velo += owner.Velocity * 0.25f;

                Particle.SpawnParticle(this, pnt, velo, radius, World.GetRandomFlameColor(), World.BlackSmokeColor);
            }
        }

        public override void DoUpdate(float dt)
        {
            base.DoUpdate(dt);

            _currentRadius = MaxRadius * Utilities.FactorWithEasing(this.Age, Duration, EasingFunctions.Out.EaseElastic);
            _color.a = 1f - Utilities.FactorWithEasing(this.Age, Duration, EasingFunctions.Out.EaseQuintic);

            if (_hasShockWave)
            {
                _currentShockWaveRadius = MaxShockwaveRadius * Utilities.FactorWithEasing(this.Age, Duration, EasingFunctions.Out.EaseCircle);
                _showckWaveColor.a = 1f - Utilities.FactorWithEasing(this.Age * 1.5f, Duration, EasingFunctions.Out.EaseExpo);
            }

            if (this.Age >= Duration)
                this.IsExpired = true;
        }

        public override void Render(RenderContext ctx)
        {
            base.Render(ctx);

            if (!this.ContainedBy(ctx.Viewport))
                return;

            ctx.LightMap.AddContribution(this);

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
                   || rect.Contains(new D2DEllipse(this.Position, new D2DSize(_currentRadius * 10f, _currentRadius * 10f)));

            return ret;
        }

        public override void Dispose()
        {
            base.Dispose();

            World.ObjectManager.ReturnExplosion(this);
        }

        float ILightMapContributor.GetLightRadius()
        {
            if (this.Owner is not GuidedMissile)
                return _currentRadius * 12f;
            else
                return _currentRadius * 7f;
        }

        float ILightMapContributor.GetIntensityFactor()
        {
            return 4f - (4f * Utilities.FactorWithEasing(this.Age, Duration, EasingFunctions.Out.EaseSine));
        }

        bool ILightMapContributor.IsLightEnabled()
        {
            return !this.IsExpired;
        }

        D2DPoint ILightMapContributor.GetLightPosition()
        {
            return this.Position;
        }

        D2DColor ILightMapContributor.GetLightColor()
        {
            var color = Utilities.LerpColor(D2DColor.White, _lightMapColor, Utilities.FactorWithEasing(this.Age, this.Duration, EasingFunctions.Out.EaseCubic));
            return color;
        }
    }
}
