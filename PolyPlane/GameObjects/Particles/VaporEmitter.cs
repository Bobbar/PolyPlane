﻿using PolyPlane.GameObjects.Fixtures;
using PolyPlane.GameObjects.Interfaces;
using PolyPlane.GameObjects.Tools;
using PolyPlane.Helpers;
using unvell.D2DLib;

namespace PolyPlane.GameObjects.Particles
{
    public class VaporEmitter : FixturePoint, INoGameID
    {
        private GameTimer _spawnTimer;
        private float _radius = 5f;
        private D2DColor _vaporColor = new D2DColor(0.3f, D2DColor.White);
        private const int MAX_PARTS = 40;
        private const float MAX_AGE = 3f;
        private float _visibleGs = 0f;
        private float _visibleVelo = 0f;
        private float _maxGs = 0f;
        private GameObject _parentObject = null;

        public VaporEmitter(GameObject obj, GameObject owner, D2DPoint offset, float radius, float visibleGs, float visibleVelo, float maxGs) : base(obj, offset)
        {
            _spawnTimer = AddTimer(0.05f, true);

            _spawnTimer.Interval = MAX_AGE / MAX_PARTS;

            _parentObject = owner;
            _radius = radius;
            _visibleGs = visibleGs;
            _visibleVelo = visibleVelo;
            _maxGs = maxGs;

            _spawnTimer.TriggerCallback = () => SpawnPart();

            _spawnTimer.Start();
        }

        public override void DoUpdate(float dt)
        {
            base.DoUpdate(dt);

            if (_parentObject.IsExpired)
                _spawnTimer.Stop();

            if (_parentObject is FighterPlane plane)
            {
                if (plane.IsDisabled)
                    _spawnTimer.Stop();
                else
                    _spawnTimer.Start();
            }
        }

        private void SpawnPart()
        {
            const float MIN_VELO = 800f;
            const float VELO_MOVE_AMT = 45f;

            D2DPoint newPos = Position;
            D2DPoint newVelo = Velocity;

            float gforce = 0f;
            var veloMag = newVelo.Length();

            if (_parentObject != null)
            {
                if (_parentObject is FighterPlane plane)
                    gforce = plane.GForce;
            }

            var sVisFact = Utilities.Factor(veloMag - _visibleVelo, _visibleVelo);
            var gVisFact = Utilities.FactorWithEasing(gforce - _visibleGs * 0.5f, _visibleGs, EasingFunctions.In.EaseCircle);
            var visFact = sVisFact + gVisFact;

            // Move the ellipse backwards as velo increases.
            var veloFact = Utilities.Factor(this.Owner.Velocity.Length() - MIN_VELO, MIN_VELO);
            newPos = Position - (this.Owner.Velocity.Normalized() * (VELO_MOVE_AMT * veloFact));

            // Only spawn if the particle will actually be visible.
            if (visFact >= 0.02f)
            {
                var sRadFact = Utilities.Factor(veloMag, _visibleVelo);
                var gRadFact = Utilities.Factor(gforce, _maxGs);

                var radFact = sRadFact + gRadFact;
                var newRad = _radius + Utilities.Rnd.NextFloat(-2f, 2f) + radFact * 20f;

                var newColor = _vaporColor;
                newColor.a = newColor.a * visFact * Math.Clamp(World.SampleNoise(newPos), 0.3f, 1f);

                var newPart = Particle.SpawnParticle(Owner, newPos, newVelo, newRad, newColor, newColor, ParticleType.Vapor);
                newPart.MaxAge = MAX_AGE;
            }
        }
    }
}
