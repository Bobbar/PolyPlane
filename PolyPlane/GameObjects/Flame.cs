﻿using PolyPlane.GameObjects.Fixtures;
using PolyPlane.GameObjects.Interfaces;
using PolyPlane.GameObjects.Tools;
using PolyPlane.Helpers;
using PolyPlane.Rendering;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public class Flame : GameObject
    {
        public float Radius { get; set; }

        public const int MAX_PARTS = 50;
        public const float MAX_AGE = 20f;

        private List<FlamePart> _parts = new List<FlamePart>();
        private readonly D2DColor _flameColor = new D2DColor(0.6f, D2DColor.Yellow);
        private readonly D2DColor _blackSmoke = new D2DColor(0.6f, D2DColor.Black);
        private readonly D2DColor _graySmoke = new D2DColor(0.6f, D2DColor.Gray);
        private FixturePoint _refPos = null;
        private GameTimer _spawnTimer = new GameTimer(0.1f, true);

        public Flame(GameObject obj, D2DPoint offset, float radius = 10f) : base(obj.Position, obj.Velocity)
        {
            _spawnTimer.Interval = MAX_AGE / MAX_PARTS;

            this.Owner = obj;
            this.PlayerID = obj.PlayerID;

            Radius = radius;
            _refPos = new FixturePoint(obj, offset);

            _spawnTimer.TriggerCallback = () => SpawnPart();
            _spawnTimer.Start();

            Update(World.DT);

            SpawnPart();
        }

        public Flame(GameObject obj, D2DPoint offset, D2DPoint velo, float radius = 10f) : base(obj.Position, velo)
        {
            _spawnTimer.Interval = MAX_AGE / MAX_PARTS;

            this.Owner = obj;
            this.PlayerID = obj.PlayerID;

            Radius = radius;
            _refPos = new FixturePoint(obj, offset);

            _spawnTimer.TriggerCallback = () => SpawnPart();
            _spawnTimer.Start();

            Update(World.DT);

            SpawnPart();
        }

        public Flame(GameObject obj, D2DPoint offset, bool hasFlame = true) : base(obj.Position, obj.Velocity)
        {
            _spawnTimer.Interval = MAX_AGE / MAX_PARTS;

            this.Owner = obj;
            this.PlayerID = obj.PlayerID;

            Radius = Utilities.Rnd.NextFloat(4f, 15f);
            _refPos = new FixturePoint(obj, offset);

            _spawnTimer.TriggerCallback = () => SpawnPart();

            Update(World.DT);

            if (hasFlame)
            {
                _spawnTimer.Start();
                SpawnPart();
            }
        }

        /// <summary>
        /// Stop spawning new flame parts.
        /// </summary>
        public void StopSpawning()
        {
            _spawnTimer.Stop();
        }

        /// <summary>
        /// Start spawning new flame parts.
        /// </summary>
        public void StartSpawning()
        {
            _spawnTimer.Start();
        }

        public override void Update(float dt)
        {
            base.Update(dt);
            _spawnTimer.Update(dt);

            if (_refPos != null)
            {
                _refPos.Update(dt);
                this.Position = _refPos.Position;
                this.Rotation = _refPos.Rotation;
            }

            if (this.Owner != null && this.Owner.IsExpired)
                _spawnTimer.Stop();

            if (_parts.Count == 0 && !_spawnTimer.IsRunning)
                this.IsExpired = true;

        }

        public override void Render(RenderContext ctx)
        {
            base.Render(ctx);

            _parts.ForEach(p => p.Render(ctx));
        }

        public override void FlipY()
        {
            base.FlipY();
            this._refPos.FlipY();
        }

        private void SpawnPart()
        {
            if (World.IsNetGame && World.IsServer)
                return;

            D2DPoint newPos = this.Position;
            D2DPoint newVelo = this.Velocity;

            if (_refPos != null)
            {
                _refPos.Update(World.DT);
                newPos = _refPos.Position;
            }

            if (this.Owner != null && this.Owner is not Explosion)
                newVelo = this.Owner.Velocity;

            newVelo += Utilities.RandOPoint(10f);

            var endColor = _blackSmoke;

            if (_refPos.GameObject is FighterPlane plane && !plane.IsDisabled)
                endColor = _graySmoke;

            var newRad = this.Radius + Utilities.Rnd.NextFloat(-3f, 3f);
            var newColor = new D2DColor(_flameColor.a, 1f, Utilities.Rnd.NextFloat(0f, 0.86f), _flameColor.b);

            var newPart = World.ObjectManager.RentFlamePart(this.PlayerID);
            newPart.ReInit(newPos, newRad, newColor, endColor, newVelo);
            newPart.Owner = this;

            if (_parts.Count < MAX_PARTS)
            {
                _parts.Add(newPart);
                World.ObjectManager.EnqueueFlame(newPart);
            }
            else
            {
                _parts[0].IsExpired = true;
                World.ObjectManager.ReturnFlamePart(_parts[0]);
                _parts.RemoveAt(0);

                _parts.Add(newPart);
                World.ObjectManager.EnqueueFlame(newPart);
            }
        }

        public override void Dispose()
        {
            base.Dispose();

            _spawnTimer.Stop();

            _parts.ForEach(p =>
            {
                p.IsExpired = true;
                World.ObjectManager.ReturnFlamePart(p);
            });

            _parts.Clear();
        }
    }

    public class FlamePart : GameObject, ICollidable
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

        public void ReInit(D2DPoint pos, float radius, D2DColor color, D2DColor endColor, D2DPoint velo)
        {
            this.Age = 0f;
            this.IsExpired = false;

            _ellipse.origin = pos;
            _ellipse.radiusX = radius;
            _ellipse.radiusY = radius;

            Color = color;
            StartColor = color;
            EndColor = endColor;

            _riseRate = new D2DPoint(0f, Utilities.Rnd.NextFloat(MAX_RISE_RATE, MIN_RISE_RATE));

            this.Position = _ellipse.origin;
            this.Velocity = velo;
        }

        public override void Update(float dt)
        {
            base.Update(dt);

            var ageFactFade = 1f - Utilities.Factor(this.Age, Flame.MAX_AGE);
            var ageFactSmoke = Utilities.Factor(this.Age, Flame.MAX_AGE * 3f);
            var alpha = StartColor.a * ageFactFade;

            this.Color = new D2DColor(alpha, Utilities.LerpColor(this.Color, this.EndColor, ageFactSmoke));

            if (this.Age > Flame.MAX_AGE)
            {
                this.IsExpired = true;
            }


            this.Velocity += -this.Velocity * 0.9f * dt;
            this.Velocity += _riseRate * dt;

            // Simulate the particles being blown by the wind.
            _riseRate.X = WIND_SPEED * Utilities.FactorWithEasing(this.Age, Flame.MAX_AGE, EasingFunctions.EaseOutSine);

            _ellipse.origin = this.Position;
        }

        public override void Render(RenderContext ctx)
        {
            base.Render(ctx);

            ctx.FillEllipse(Ellipse, Color);
        }
    }
}
