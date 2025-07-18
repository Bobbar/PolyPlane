﻿using PolyPlane.AI_Behavior;
using PolyPlane.GameObjects.Animations;
using PolyPlane.GameObjects.Fixtures;
using PolyPlane.GameObjects.Guidance;
using PolyPlane.GameObjects.Interfaces;
using PolyPlane.GameObjects.Managers;
using PolyPlane.GameObjects.Particles;
using PolyPlane.GameObjects.Tools;
using PolyPlane.Helpers;
using PolyPlane.Rendering;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public sealed class FighterPlane : GameObjectNet, IPolygon
    {
        public RenderPoly Polygon { get; set; }
        public Gun Gun => _gun;
        public bool IsAI => _isAIPlane;
        public D2DPoint GunPosition => _gun.Position;
        public D2DPoint ExhaustPosition => _centerOfThrust.Position;
        public Direction FlipDirection => _currentDir;
        public float GForce => _gForce;
        public double LastBurstTime => _gun.LastBurstTime;

        public AIPersonality Personality
        {
            get
            {
                if (_aiBehavior != null)
                    return _aiBehavior.Personality;
                else
                    return AIPersonality.Normal;
            }
        }

        public IAIBehavior AIBehavior => _aiBehavior;


        public bool InResetCooldown
        {
            get { return !_easePhysicsComplete; }
        }

        public D2DColor PlaneColor
        {
            get { return _planeColor; }
            set { _planeColor = value; }
        }

        public float ThrustAmount
        {
            get { return _thrustAmt.Value; }

            set
            {
                if (!IsDisabled && !EngineDamaged)
                {
                    _thrustAmt.Target = Math.Clamp(value, 0.0001f, 1f);
                }
            }

        }

        public float Deflection
        {
            get { return _controlWing.Deflection; }

            set
            {
                _controlWing.Deflection = value;
                NozzleDeflection = value;
            }
        }

        private float NozzleDeflection
        {
            get { return _nozzleDefLimiter.Value; }

            set
            {
                if (value >= -MAX_NOZZLE_DEF && value <= MAX_NOZZLE_DEF)
                    _nozzleDefLimiter.Target = value;
                else
                    _nozzleDefLimiter.Target = Math.Sign(value) * MAX_NOZZLE_DEF;
            }
        }

        public int NumMissiles
        {
            get { return _numMissiles; }
            set { _numMissiles = Math.Clamp(value, 0, MAX_MISSILES); }
        }

        public int NumBullets
        {
            get { return _numBullets; }
            set { _numBullets = Math.Clamp(value, 0, MaxBullets); }
        }

        public int NumDecoys
        {
            get { return _numDecoys; }
            set { _numDecoys = Math.Clamp(value, 0, MAX_DECOYS); }
        }

        public float Health
        {
            get { return _health; }
            set { _health = Math.Clamp(value, 0, MAX_HEALTH); }
        }

        public Radar Radar { get; set; }
        public float Thrust = 2000f;

        public bool FiringBurst
        {
            get { return _firingBurst; }
            set
            {
                if (_firingBurst != value)
                {
                    // Trigger gun burst as soon as this changes state.
                    if (value)
                        _gun.StartBurst();
                    else
                        _gun.StopBurst();

                    _firingBurst = value;
                }
            }
        }

        private int MaxBullets
        {
            get
            {
                if (!GunDamaged)
                    return MAX_BULLETS;
                else
                    return MAX_BULLETS_DAMAGED;
            }
        }

        public bool DroppingDecoy { get; set; } = false;
        public bool EngineDamaged { get; set; } = false;
        public bool GunDamaged { get; set; } = false;
        public bool HasCrashed { get; set; } = false;
        public bool WasHeadshot { get; set; } = false;
        public bool IsDisabled { get; set; } = false;

        public bool RespawnQueued = false;
        public bool HasRadarLock = false;
        public bool AIRespawnReady = false;
        public string PlayerName = "Player";
        public float VerticalSpeed = 0f;
        public float PlayerGuideAngle = 0;
        public float DeathTime = 0;
        public int BulletsFired = 0;
        public int MissilesFired = 0;
        public int DecoysDropped = 0;
        public int Kills = 0;
        public int Headshots = 0;
        public int Deaths = 0;

        private const float MAX_NOZZLE_DEF = 30f;
        public const int MAX_DECOYS = 15;
        public const int MAX_BULLETS = 30;
        public const int MAX_BULLETS_DAMAGED = 15;
        public const int MAX_MISSILES = 6;
        public const float MAX_HEALTH = 32f;
        public const float MISSILE_DAMAGE = 32;
        public const float BULLET_DAMAGE = 4;
        public const float THRUST_BOOST_AMT = 1000f;

        public Action<Bullet> FireBulletCallback
        {
            get { return _gun.FireBulletCallback; }
            set
            {
                _gun.FireBulletCallback = value;
            }
        }

        public Action<Decoy> DropDecoyCallback
        {
            get { return _decoyDispenser.DropDecoyCallback; }

            set
            {
                _decoyDispenser.DropDecoyCallback = value;
            }
        }

        public Action<GuidedMissile> FireMissileCallback { get; set; }
        public Action<PlayerKilledEventArgs> PlayerKilledCallback { get; set; }
        public Action<FighterPlane> PlayerCrashedCallback { get; set; }
        public Action<ImpactEvent> PlayerHitCallback { get; set; }

        private IAIBehavior _aiBehavior;
        private List<Wing> _wings = new List<Wing>();
        private Wing? _controlWing = null;
        private RateLimiter _thrustAmt = new RateLimiter(0.2f);
        private RateLimiter _nozzleDefLimiter = new RateLimiter(30f);
        private Direction _currentDir = Direction.Right;
        private Direction _queuedDir = Direction.Right;
        private bool _isAIPlane = false;

        private GameTimer _flipTimer;
        private GameTimer _expireTimeout;
        private GameTimer _isLockOntoTimeout;
        private GameTimer _bulletRegenTimer;
        private GameTimer _decoyRegenTimer;
        private GameTimer _missileRegenTimer;
        private GameTimer _easePhysicsTimer;
        private GameTimer _groundDustSpawnTimer;
        private FloatAnimation _engineOutSpoolDown;

        private bool _firingBurst = false;
        private bool _easePhysicsComplete = false;
        private float _damageDeflection = 0f;
        private float _gForce = 0f;
        private float _gForceDirection = 0f;
        private float _health = MAX_HEALTH;
        private float _prevAlt = 0;
        private int _throttlePos = 0;
        private int _numMissiles = MAX_MISSILES;
        private int _numBullets = MAX_BULLETS;
        private int _numDecoys = MAX_DECOYS;
        private SmoothFloat _gforceSmooth = new SmoothFloat(8);
        private SmoothFloat _vsSmooth = new SmoothFloat(16);

        private const float POLY_TESSELLATE_DIST = 2f; // Tessellation amount. Smaller = higher resolution.
        private const float BULLET_DISTORT_AMT = 4f;
        private const float MISSILE_DISTORT_AMT = 7f;
        private const float PILOT_HEAD_SIZE = 3.5f; // Radius of the ellipse within the cockpit, representing the pilot, for headshot checking.

        private RenderPoly FlamePoly;
        private D2DLayer _polyClipLayer = null;
        private D2DColor _flameFillColor = new D2DColor(0.6f, D2DColor.Yellow);
        private D2DColor _planeColor = D2DColor.White;
        private static readonly D2DColor _cockpitColor = new D2DColor(0.5f, D2DColor.LightBlue);
        private static readonly D2DColor _groundDustColor = new D2DColor(1f, 0.35f, 0.2f, 0.1f);
        private static readonly D2DSize _cockpitSize = new D2DSize(9f, 6f);
        private D2DEllipse _cockpitEllipse = new D2DEllipse(D2DPoint.Zero, _cockpitSize);
        private D2DEllipse _pilotHeadEllipse = new D2DEllipse(D2DPoint.Zero, new D2DSize(PILOT_HEAD_SIZE, PILOT_HEAD_SIZE));

        private Gun _gun;
        private DecoyDispenser _decoyDispenser;
        private FixturePoint _flamePos;
        private FixturePoint _cockpitPosition;
        private FixturePoint _centerOfThrust;
        private FixturePoint _centerOfMass;
        private FlameEmitter _engineFireFlame;

        private List<BulletHole> _bulletHoles = new List<BulletHole>();

        const GameObjectFlags DefaultFlags = GameObjectFlags.ClampToGround | GameObjectFlags.SpatialGrid | GameObjectFlags.ExplosionImpulse;

        private static readonly D2DPoint[] _planePoly =
        [
            new D2DPoint(28,0),
            new D2DPoint(25,-2),
            new D2DPoint(20,-3),
            new D2DPoint(16,-5.3f),
            new D2DPoint(13,-6),
            new D2DPoint(10,-5.3f),
            new D2DPoint(7,-4),
            new D2DPoint(4,-3),
            new D2DPoint(-13,-3),
            new D2DPoint(-17,-10),
            new D2DPoint(-21,-10),
            new D2DPoint(-25,-3),
            new D2DPoint(-28,-1),
            new D2DPoint(-28,2),
            new D2DPoint(-19,3),
            new D2DPoint(21,3),
            new D2DPoint(25,2),
        ];

        private static readonly D2DPoint[] _flamePoly =
        [
            new D2DPoint(-7.5f, 1.2f),
            new D2DPoint(-10, 0),
            new D2DPoint(-7.5f, -1.2f),
            new D2DPoint(-7, 0),

        ];

        public FighterPlane(D2DPoint pos, D2DColor color) : base(pos, DefaultFlags)
        {
            this.PlayerID = World.GetNextPlayerId();
            _isAIPlane = false;
            ThrustAmount = 1f;
            _planeColor = color;

            InitStuff();
        }

        public FighterPlane(D2DPoint pos, D2DColor color, AIPersonality personality) : this(pos, color)
        {
            _aiBehavior = new FighterPlaneAI(this, personality);
            _isAIPlane = true;
        }

        public FighterPlane(D2DPoint pos, D2DColor color, GameID id) : this(pos, color)
        {
            this.ID = id;
            IsNetObject = true;
            _isAIPlane = false;
        }

        private void InitStuff()
        {
            this.Radar = new Radar(this);

            this.Mass = 90f;
            this.RenderScale = 1.5f;
            this.RenderLayer = 5;

            this.Polygon = new RenderPoly(this, _planePoly, this.RenderScale, POLY_TESSELLATE_DIST);
            this.FlamePoly = new RenderPoly(this, _flamePoly, new D2DPoint(12f, 0), this.RenderScale);

            _prevAlt = this.Altitude;

            InitWings();

            var centerOfThrustPos = new D2DPoint(-26.6f, 0.46f) * this.RenderScale;

            _centerOfThrust = AddAttachment(new FixturePoint(this, centerOfThrustPos), highFrequencyPhysics: true);
            _engineFireFlame = AddAttachment(new FlameEmitter(this, centerOfThrustPos, 7f, 13f, false));
            _flamePos = AddAttachment(new FixturePoint(this, new D2DPoint(-27.3f, 0.46f) * this.RenderScale));
            _cockpitPosition = AddAttachment(new FixturePoint(this, new D2DPoint(19.5f, -5f)));
            _gun = AddAttachment(new Gun(this, new D2DPoint(35f, 0)));
            _decoyDispenser = AddAttachment(new DecoyDispenser(this, new D2DPoint(-24f, 0f)));

            _engineOutSpoolDown = AddAttachment(new FloatAnimation(1f, 0f, 20f, EasingFunctions.EaseLinear, v =>
            {
                if (!this.IsDisabled)
                    _thrustAmt.Target = v;
            }));

            _flipTimer = AddTimer(2f);
            _expireTimeout = AddTimer(40f);
            _isLockOntoTimeout = AddTimer(3f);
            _bulletRegenTimer = AddTimer(0.2f, true);
            _decoyRegenTimer = AddTimer(0.6f, true);
            _missileRegenTimer = AddTimer(60f, true);
            _easePhysicsTimer = AddTimer(5f, true);
            _groundDustSpawnTimer = AddTimer(0.03f, true);

            _expireTimeout.TriggerCallback = () =>
            {
                if (!World.RespawnAIPlanes)
                    this.IsExpired = true;
                else
                    this.AIRespawnReady = true;
            };

            _bulletRegenTimer.TriggerCallback = () =>
            {
                if (!this.IsNetObject && !this.FiringBurst && NumBullets < MaxBullets)
                    NumBullets++;
            };


            _missileRegenTimer.TriggerCallback = () =>
            {
                if (!this.IsNetObject && World.MissileRegen)
                    if (NumMissiles < MAX_MISSILES)
                        NumMissiles++;
            };


            _decoyRegenTimer.TriggerCallback = () =>
            {
                if (!this.IsNetObject && !this.DroppingDecoy && NumDecoys < MAX_DECOYS)
                    NumDecoys++;
            };

            _groundDustSpawnTimer.TriggerCallback = DoGroundDustEffect;

            _groundDustSpawnTimer.Start();
            _bulletRegenTimer.Start();
            _missileRegenTimer.Start();
            _decoyRegenTimer.Start();
            _easePhysicsTimer.Start();

            _isLockOntoTimeout.TriggerCallback = () => HasRadarLock = false;
            _easePhysicsTimer.TriggerCallback = () => _easePhysicsComplete = true;
        }

        private void InitWings()
        {
            const float DEFLECT_RATE = 45f;
            const float MIN_VELO = 300f;

            // Main wing.
            AddWing(new Wing(this, new WingParameters()
            {
                RenderLength = 10f * this.RenderScale,
                RenderWidth = 3f,
                Area = 0.318f,
                MaxLiftForce = 15000f,
                MaxDragForce = 12000f,
                VeloLiftFactor = 2f,
                DragFactor = 0.225f,
                MaxAOA = 25f,
                Position = new D2DPoint(-5f, 0.6f) * this.RenderScale,
                MinVelo = MIN_VELO
            }));

            // Tail wing. (Control wing)
            AddWing(new Wing(this, new WingParameters()
            {
                RenderLength = 5f * this.RenderScale,
                RenderWidth = 3f,
                Area = 0.1275f,
                MaxDeflection = 40f,
                MaxLiftForce = 7000f,
                MaxDragForce = 7000f,
                VeloLiftFactor = 2f,
                DragFactor = 0.15f,
                MaxAOA = 35f,
                DeflectionRate = DEFLECT_RATE,
                PivotPoint = new D2DPoint(-25f, 0.6f) * this.RenderScale,
                Position = new D2DPoint(-27.5f, 0.6f) * this.RenderScale,
                MinVelo = MIN_VELO
            }), isControl: true);

            // Center of mass location.
            _centerOfMass = AddAttachment(new FixturePoint(this, new D2DPoint(-5f, 0f)), highFrequencyPhysics: true);
        }

        public override void DoPhysicsUpdate(float dt)
        {
            base.DoPhysicsUpdate(dt);

            var wingForce = D2DPoint.Zero;
            var wingTorque = 0f;
            var guideRot = this.Rotation;

            // Get guidance direction.
            if (_isAIPlane)
                guideRot = GetAPGuidanceDirection(_aiBehavior.GetAIGuidanceDirection(dt));
            else
                guideRot = GetAPGuidanceDirection(PlayerGuideAngle);

            // Deflection direction.
            var deflection = Utilities.ClampAngle180(guideRot);

            if (!IsNetObject)
            {
                if (!this.IsDisabled)
                    this.Deflection = deflection;
                else
                    this.Deflection = _damageDeflection;
            }

            // Wing force and torque.
            for (int i = 0; i < _wings.Count; i++)
            {
                var wing = _wings[i];

                // How much force a damaged wing contributes.
                const float DAMAGED_FACTOR = 0.2f;

                var forces = wing.GetForces(_centerOfMass.Position);

                if (wing.Visible)
                {
                    wingForce += forces.LiftAndDrag;
                    wingTorque += forces.Torque;
                }
                else
                {
                    wingForce += forces.LiftAndDrag * DAMAGED_FACTOR;
                    wingTorque += forces.Torque * DAMAGED_FACTOR;
                }
            }

            // Apply small amount of drag and torque for bullet holes.
            // Decreases handling and max speed with damage.
            float damageTorque = 0f;
            D2DPoint damageForce = D2DPoint.Zero;

            if (!IsNetObject && World.BulletHoleDrag)
            {
                for (int i = 0; i < _bulletHoles.Count; i++)
                {
                    var hole = _bulletHoles[i];

                    GetBulletHoleDrag(hole, dt, out D2DPoint dVec, out float dTq);
                    damageTorque += dTq;
                    damageForce += dVec;
                }
            }

            // Apply disabled effects.
            if (IsDisabled)
            {
                const float DISABLED_FACTOR = 0.1f;

                wingForce *= DISABLED_FACTOR;
                wingTorque *= DISABLED_FACTOR;

                damageTorque *= DISABLED_FACTOR;
                damageForce *= DISABLED_FACTOR;
            }

            var thrust = GetThrust(true);

            if (!IsNetObject)
            {
                // Ease in physics.
                var easeFact = 1f;

                if (!_easePhysicsComplete)
                    _easePhysicsTimer.Start();

                if (!_easePhysicsComplete && _easePhysicsTimer.IsRunning)
                    easeFact = _easePhysicsTimer.Position;

                // Integrate torque, thrust and wing force.
                var thrustTorque = Utilities.GetTorque(_centerOfMass.Position, _centerOfThrust.Position, thrust);
                var rotAmt = ((wingTorque + thrustTorque + damageTorque) * easeFact) / this.GetInertia(this.Mass);
                var forces = (thrust + wingForce + damageForce) * easeFact;

                this.RotationSpeed += rotAmt * dt;
                this.Velocity += (forces / this.Mass) * dt;
                this.Velocity += (World.Gravity * dt);
            }

            // Compute g-force.
            var totForce = (thrust / this.Mass * dt) + (wingForce / this.Mass * dt);
            var gforce = totForce.Length() / dt / World.Gravity.Y;
            _gForce = _gforceSmooth.Add(gforce);
            _gForceDirection = totForce.Angle();
        }

        public override void DoUpdate(float dt)
        {
            base.DoUpdate(dt);

            // Update all the low frequency stuff.
            _thrustAmt.Update(dt);
            _nozzleDefLimiter.Update(dt);

            if (!this.IsNetObject)
                CheckForFlip();

            this.Radar?.Update(dt);

            if (_aiBehavior != null)
                _aiBehavior.Update(dt);

            if (this.IsDisabled)
            {
                DeathTime += dt;
                _thrustAmt.SetNow(0f);
            }

            if (!this.IsDisabled && !this.EngineDamaged)
                _engineFireFlame.StopSpawning();

            // Update vertical speed.
            var diff = Altitude - _prevAlt;
            _prevAlt = Altitude;

            VerticalSpeed = _vsSmooth.Add(diff * (1000f / (float)World.LastFrameTimeMs / World.GameSpeed));
        }

        private void UpdateFlame()
        {
            const float MIN_LEN = 4f;
            const float MAX_LEN = 50f;
            const float MAX_VELO_LEN = 30f;
            const float MAX_VELO = 1500f;
            const float MAX_THRUST_LEN = 20f;

            var thrust = GetThrust(true);
            var maxThrust = Thrust + THRUST_BOOST_AMT;

            // Fiddle with flame angle, length and color.
            var veloFact = Utilities.Factor(this.AirSpeedTrue, MAX_VELO);
            var veloLen = MAX_VELO_LEN * veloFact;

            var thrustFact = Utilities.Factor(thrust.Length(), maxThrust);
            var thrustLen = thrustFact * MAX_THRUST_LEN;

            var flameAngle = thrust.Angle();
            var len = Math.Clamp((veloLen + thrustLen), MIN_LEN, MAX_LEN);

            FlamePoly.SourcePoly[1].X = -Utilities.Rnd.NextFloat(9f + len, 11f + len);
            _flameFillColor.g = Utilities.Rnd.NextFloat(0.6f, 0.86f);

            FlamePoly.Update(_flamePos.Position, flameAngle, this.RenderScale);
        }

        private void DoGroundDustEffect()
        {
            // Spawn dust particles when very near the ground.
            const float DUST_ALT = 400f;
            const float MIN_VELO = 100f;

            if (this.Altitude < DUST_ALT && this.Velocity.Length() > MIN_VELO)
            {
                var altFact = 1f - Utilities.FactorWithEasing(this.Altitude, DUST_ALT, EasingFunctions.EaseLinear);
                var alpha = Math.Clamp(altFact, 0f, 0.5f);
                var radius = 15f * (altFact) + Utilities.Rnd.NextFloat(1f, 3f);
                var velo = (this.Velocity + (this.Velocity * 0.4f)) + new D2DPoint(Utilities.Rnd.NextFloat(-150f, 150f), 0f);
                var groundPos = new D2DPoint(this.Position.X - (this.Velocity.X * 0.2f) + Utilities.Rnd.NextFloat(-100f, 100f), 0f);

                Particle.SpawnParticle(this, groundPos, velo, radius, _groundDustColor.WithAlpha(alpha), _groundDustColor, ParticleType.Dust);
            }
        }

        public override void Render(RenderContext ctx)
        {
            base.Render(ctx);

            if (GetThrust(true).Length() > 0f)
            {
                UpdateFlame();

                ctx.DrawPolygon(this.FlamePoly, _flameFillColor, 1f, _flameFillColor);
            }

            if (!this.IsDisabled)
                DrawShockwave(ctx);

            ctx.DrawPolygonWithLighting(this.Polygon, this.Position, D2DColor.Black.WithAlpha(0.3f), 0.5f, _planeColor, maxIntensity: 0.6f);

            DrawClippedObjects(ctx);

            foreach (var wing in _wings)
                wing.Render(ctx);


            //foreach (var b in _bulletHoles)
            //    ctx.DrawArrow(b.Position, b.Position + Utilities.AngleToVectorDegrees(b.Rotation, 10), D2DColor.Blue, 1f, 3f);

            //foreach (var pnt in this.Polygon.Poly)
            //    ctx.FillEllipseSimple(pnt, 0.5f, D2DColor.Red);

            //DrawFOVCone(gfx);
            //_cockpitPosition.Render(ctx);
            //_centerOfThrust.Render(ctx);
        }

        private void DrawShockwave(RenderContext ctx)
        {
            const float MIN_VELO = 600f;
            const int NUM_SEGS = 30;

            // Compute speed factor and fiddle with dimensions, positions, line weight and color alpha.
            var speedFact = Utilities.FactorWithEasing(this.AirSpeedIndicated - MIN_VELO, MIN_VELO, EasingFunctions.In.EaseSine);
            if (speedFact < 0.01f)
                return;

            var turbulence = World.GetTurbulenceForPosition(this.Position);

            // Increase width and height with speed,
            // and add some wiggle from turbulence.
            var width = 40f;
            width += 30f * speedFact;
            width += 30f * turbulence;

            var height = 100f;
            height += 300f * speedFact;
            height += 30f * turbulence;

            // Increase the initial line thiccness
            // and color alpha with speed and turbs.
            var lineWeight = 8f;
            lineWeight += 9f * speedFact * turbulence;

            var alpha = 0.8f;
            alpha *= speedFact * turbulence;

            // ## Control points for beziers. ##

            // Move center start point backwards with speed.
            var startCenter = this.Position + Utilities.AngleToVectorDegrees(this.Rotation, 30f - (30f * speedFact));

            // Reference angle for beziers.
            var veloNorm = this.Velocity.Normalized();

            // Compute the width vector and center end point.
            var widthVec = veloNorm * width;
            var endCenter = startCenter - widthVec;

            // Computer the height/tangent vector and top/bot points.
            var heightVec = veloNorm.Tangent() * height;
            var endTop = endCenter - heightVec;
            var endBot = endCenter + heightVec;

            // Initial color.
            var color = D2DColor.White.WithAlpha(alpha);

            for (int i = 0; i < NUM_SEGS - 1; i++)
            {
                // Current positions of curve segment.
                var t = (float)i / (float)NUM_SEGS;
                var t2 = (float)(i + 1) / (float)NUM_SEGS;

                // Simple linear curve for line width, with some padding.
                // It just looks better compared to any of the easing funcs.
                var w = (lineWeight * (1f - t)) + 0.2f;

                // Decrease alpha with position.
                var a = alpha * EasingFunctions.In.EaseSine(1f - t);

                // Color of this segment.
                var lineColor = color.WithAlpha(a);

                // Control points for top and bottom beziers.
                var p0 = startCenter;
                var p1 = endCenter;
                var p2Top = endTop;
                var p2Bot = endBot;

                // Plot top and bottom line segment points.
                var B1Top = Utilities.LerpBezierCurve(p0, p1, p2Top, t);
                var B2Top = Utilities.LerpBezierCurve(p0, p1, p2Top, t2);

                var B1Bot = Utilities.LerpBezierCurve(p0, p1, p2Bot, t);
                var B2Bot = Utilities.LerpBezierCurve(p0, p1, p2Bot, t2);

                if (i > 0) // Skip the first segment.
                {
                    // Get clamped noise for segment alpha.
                    var noiseT = Math.Clamp(World.SampleNoise(B1Top), 0.1f, 1f);
                    var noiseB = Math.Clamp(World.SampleNoise(B1Bot), 0.1f, 1f);

                    ctx.DrawLine(B1Top, B2Top, lineColor.WithAlpha(a * noiseT), w);
                    ctx.DrawLine(B1Bot, B2Bot, lineColor.WithAlpha(a * noiseB), w);
                }
            }
        }

        private void DrawClippedObjects(RenderContext ctx)
        {
            if (_polyClipLayer == null)
                _polyClipLayer = ctx.Device.CreateLayer();

            // Clip with the polygon.
            using (var polyClipGeo = ctx.Device.CreatePathGeometry())
            {
                polyClipGeo.AddLines(this.Polygon.Poly);
                polyClipGeo.ClosePath();

                ctx.Gfx.PushLayer(_polyClipLayer, ctx.Viewport, polyClipGeo);

                DrawCockpit(ctx);
                DrawBulletHoles(ctx);

                ctx.Gfx.PopLayer();
            }
        }

        private void DrawBulletHoles(RenderContext ctx)
        {
            for (int i = 0; i < _bulletHoles.Count; i++)
            {
                var hole = _bulletHoles[i];
                if (!ctx.Viewport.Contains(hole.Position))
                    return;

                ctx.PushTransform();
                ctx.RotateTransform(hole.Rotation + hole.Angle, hole.Position);

                hole.Render(ctx);

                ctx.PopTransform();
            }
        }

        private void DrawCockpit(RenderContext ctx)
        {
            if (!ctx.Viewport.Contains(_cockpitPosition.Position))
                return;

            ctx.PushTransform();
            ctx.RotateTransform(_cockpitPosition.Rotation, _cockpitPosition.Position);

            _cockpitEllipse.origin = _cockpitPosition.Position;

            ctx.FillEllipseWithLighting(_cockpitEllipse, WasHeadshot ? D2DColor.DarkRed : _cockpitColor, maxIntensity: 0.6f);
            ctx.DrawEllipse(_cockpitEllipse, D2DColor.Black.WithAlpha(0.5f), 0.5f);

            ctx.PopTransform();
        }

        private void DrawFOVCone(D2DGraphics gfx)
        {
            const float LEN = 300f;
            const float FOV = 40f;
            var color = D2DColor.Red;

            var centerLine = Utilities.AngleToVectorDegrees(this.Rotation, LEN);
            var cone1 = Utilities.AngleToVectorDegrees(this.Rotation + (FOV * 0.5f), LEN);
            var cone2 = Utilities.AngleToVectorDegrees(this.Rotation - (FOV * 0.5f), LEN);


            gfx.DrawLine(this.Position, this.Position + cone1, color);
            gfx.DrawLine(this.Position, this.Position + cone2, color);
        }


        public void IsLockedOnto()
        {
            if (!_isLockOntoTimeout.IsRunning || !HasRadarLock)
                _isLockOntoTimeout.Restart();

            HasRadarLock = true;
        }

        public void FireMissile(GameObject target)
        {
            if (World.GunsOnly)
                return;

            if (this.NumMissiles <= 0 || this.IsDisabled)
            {
                Log.Msg("Click...");
                return;
            }

            var missile = new GuidedMissile(this, target, GuidanceType.Advanced);

            FireMissileCallback(missile);

            this.MissilesFired++;
            this.NumMissiles--;
        }

        private float GetAPGuidanceDirection(float dir)
        {
            const float SENSITIVITY = 1.7f; // How aggressively we try to point in the specified direction.
            const float MIN_VELO = 400f; // Minimum velo before using rotation based calculation.

            // Compute two rotation amounts, and lerp between them as velocity changes.
            // One amount is based on velocity angle, the other is based on the current rotation.
            // The velocity angle is much better at rotating quickly and accurately to the specified direction.
            // The rotation angle works better when velocities are very low and the velocity angle becomes unreliable.
            var dirVec = Utilities.AngleToVectorDegrees(dir);
            var dirVecVelo = dirVec * SENSITIVITY;
            var amtVelo = Utilities.RadsToDegrees(dirVecVelo.Cross(this.Velocity.Normalized()));
            var amtRot = Utilities.RadsToDegrees(dirVec.Cross(Utilities.AngleToVectorDegrees(this.Rotation)));
            var amt = Utilities.Lerp(amtVelo, amtRot, Utilities.Factor(MIN_VELO, this.Velocity.Length()));

            var rot = amt;

            rot = Utilities.ClampAngle(rot);

            return rot;
        }

        public void SetGuidanceAngle(float angle)
        {
            PlayerGuideAngle = angle;
        }

        public void AddWing(Wing wing, bool isControl = false)
        {
            if (isControl && _controlWing == null)
                _controlWing = wing;

            const float VAPOR_TRAIL_GS = 9f; // How many Gs before vapor trail is visible.
            const float VAPOR_TRAIL_VELO = 1000f; // Velocity before vapor trail is visible.
            const float MAX_GS = 15f; // Gs for max vapor trail intensity.

            wing.AddAttachment(new VaporEmitter(wing, this, D2DPoint.Zero, 8f, VAPOR_TRAIL_GS, VAPOR_TRAIL_VELO, MAX_GS));

            AddAttachment(wing, highFrequencyPhysics: true);

            _wings.Add(wing);
        }

        public void AddBulletHole(D2DPoint pos, float angle, float distortAmt = 3f)
        {
            var distortVec = Utilities.AngleToVectorDegrees(angle, distortAmt);

            this.Polygon.Distort(pos, distortVec);

            var bulletHole = AddAttachment(World.ObjectManager.RentBulletHole(this, pos + distortVec, angle));
            bulletHole.IsNetObject = this.IsNetObject;
            _bulletHoles.Add(bulletHole);
        }

        private void AddPolyDamageEffectsResult(RenderPoly poly, PlaneImpactResult result)
        {
            var mainWing = _wings.First();
            var tailWing = _wings.Last();

            if (mainWing.Visible && !Utilities.PointInPoly(mainWing.PivotPoint.Position, poly.Poly))
                result.ImpactType |= ImpactType.DamagedMainWing;

            if (tailWing.Visible && !Utilities.PointInPoly(tailWing.PivotPoint.Position, poly.Poly))
                result.ImpactType |= ImpactType.DamagedTailWing;

            // Check for engine damage.
            if (!this.EngineDamaged && !Utilities.PointInPoly(_centerOfThrust.Position, poly.Poly))
                result.ImpactType |= ImpactType.DamagedEngine;

            // Check for gun damage.
            if (!this.GunDamaged && !Utilities.PointInPoly(GunPosition, poly.Poly))
                result.ImpactType |= ImpactType.DamagedGun;
        }

        private void ApplyPolyDamageEffects(PlaneImpactResult result)
        {
            var mainWing = _wings.First();
            var tailWing = _wings.Last();

            if (mainWing.Visible && result.HasFlag(ImpactType.DamagedMainWing))
            {
                mainWing.Visible = false;
                SpawnDebris(1, mainWing.Position, D2DColor.Gray);
            }

            if (tailWing.Visible && result.HasFlag(ImpactType.DamagedTailWing))
            {
                tailWing.Visible = false;
                SpawnDebris(1, tailWing.Position, D2DColor.Gray);
            }

            if (!this.EngineDamaged && result.HasFlag(ImpactType.DamagedEngine))
            {
                _engineFireFlame.StartSpawning();
                this.EngineDamaged = true;

                // Spool down thrust gradually.
                _engineOutSpoolDown.Start();
            }

            // Set gun damaged and trim ammo.
            if (!this.GunDamaged && result.HasFlag(ImpactType.DamagedGun))
            {
                this.GunDamaged = true;

                if (NumBullets > MAX_BULLETS_DAMAGED)
                    NumBullets = MAX_BULLETS_DAMAGED;
            }
        }

        public void HandleImpactResult(PlaneImpactResult result, float dt)
        {
            var impactor = result.ImpactorObject;
            var attackPlane = impactor.Owner as FighterPlane;

            // Always change target to attacking plane?
            if (this.IsAI)
                _aiBehavior.ChangeTarget(attackPlane);

            if (result.ImpactType != ImpactType.Splash)
            {
                var distortAmt = BULLET_DISTORT_AMT;

                if (result.HasFlag(ImpactType.Missile))
                    distortAmt = MISSILE_DISTORT_AMT;

                var angle = result.ImpactAngle;

                // Add bullet hole and polygon distortion.
                AddBulletHole(result.ImpactPointOrigin, angle, distortAmt);

                // Apply polygon distortion related damage effects.
                ApplyPolyDamageEffects(result);

                // Push and twist the plane accordingly.
                DoImpactImpulse(impactor, result.ImpactPoint);
            }

            if (result.DamageAmount > 0f)
            {
                if (result.WasHeadshot)
                {
                    SpawnDebris(8, result.ImpactPoint, D2DColor.Red);
                    WasHeadshot = true;
                    Health = 0;
                    attackPlane.Headshots++;
                }
                else
                {
                    Health -= result.DamageAmount;

                    if (result.HasFlag(ImpactType.Missile))
                        SpawnDebris(4, result.ImpactPoint, this.PlaneColor);
                    else if (result.HasFlag(ImpactType.Bullet))
                        SpawnDebris(1, result.ImpactPoint, this.PlaneColor);
                }

                if (this.Health <= 0 && !this.IsDisabled)
                {
                    // Only do this for local games or from the server during net play.
                    if (!World.IsNetGame || (World.IsNetGame && World.IsServer))
                        DoPlayerKilled(attackPlane, result.ImpactType);
                }
            }

            PlayerHitCallback?.Invoke(new ImpactEvent(this, attackPlane, result.DamageAmount > 0f));
        }

        public PlaneImpactResult GetImpactResult(GameObject impactor, D2DPoint impactPos)
        {
            // Get impact angle, damage and distortion results.
            var angle = Utilities.ClampAngle((impactor.Velocity - this.Velocity).Angle() - this.Rotation);
            var isMissile = impactor is GuidedMissile;
            var distortAmt = 0f;
            var damageAmt = 0f;

            var result = new PlaneImpactResult();
            result.TargetPlane = this;
            result.ImpactorObject = impactor;
            result.ImpactPointOrigin = Utilities.ScaleToOrigin(this, impactPos);
            result.ImpactPoint = impactPos;
            result.ImpactAngle = angle;
            result.WasFlipped = this.Polygon.IsFlipped;

            if (isMissile)
            {
                result.ImpactType |= ImpactType.Missile;
                damageAmt = MISSILE_DAMAGE;
                distortAmt = MISSILE_DISTORT_AMT;
            }
            else
            {
                result.ImpactType |= ImpactType.Bullet;
                damageAmt = BULLET_DAMAGE;
                distortAmt = BULLET_DISTORT_AMT;
            }

            // Distortion vector for poly damage and headshot checks.
            var distortVec = Utilities.AngleToVectorDegrees(angle, distortAmt);

            // Copy the polygon, distort it, then check for distortion related damage effects.
            var polyCopy = new RenderPoly(this.Polygon, this.Position, this.Rotation);
            polyCopy.Distort(result.ImpactPointOrigin, distortVec);


            // Check for headshots.
            _pilotHeadEllipse.origin = _cockpitPosition.Position;
            var cockpitImpactPos = (result.ImpactPointOrigin + distortVec).Translate(this.Rotation, this.Position, this.RenderScale);

            // Headshot if the impact is inside the head ellipse or if the origin of the ellipse is outside the polygon due to distortion/damage.
            var hitCockpit = _pilotHeadEllipse.Contains(_cockpitPosition.Rotation, cockpitImpactPos) || !Utilities.PointInPoly(_cockpitPosition.Position, polyCopy.Poly);

            if (hitCockpit)
            {
                result.ImpactType |= ImpactType.Headshot;
                damageAmt = this.Health;
            }

            AddPolyDamageEffectsResult(polyCopy, result);

            // Set the damage amount.
            if (!IsDisabled && this.Health > 0f)
            {
                result.DamageAmount = damageAmt;
                result.NewHealth = this.Health - result.DamageAmount;

                if (result.WasHeadshot)
                    result.NewHealth = 0f;
            }

            return result;
        }

        public void DoPlayerKilled(FighterPlane attackPlane, ImpactType impactType)
        {
            if (IsDisabled)
                return;

            _thrustAmt.SetNow(0f);
            IsDisabled = true;
            DeathTime = World.CurrentDT;
            FiringBurst = false;
            DroppingDecoy = false;
            _engineFireFlame.StartSpawning();

            _damageDeflection = _controlWing.Deflection;

            if (!World.IsClient)
            {
                attackPlane.Kills++;
                Deaths++;
            }

            var killedEvent = new PlayerKilledEventArgs(this, attackPlane, impactType);
            PlayerKilledCallback?.Invoke(killedEvent);
        }

        private void DoImpactImpulse(GameObject impactor, D2DPoint impactPos)
        {
            if (this.IsNetObject)
                return;

            const float IMPACT_MASS_BULLET = 2f;
            const float IMPACT_MASS_MISSILE = 10f;

            float mass = IMPACT_MASS_BULLET;

            if (impactor is GuidedMissile)
                mass = IMPACT_MASS_MISSILE;

            var velo = impactor.Velocity - this.Velocity;
            var force = (mass * velo.Length());
            var forceVec = (velo.Normalized() * force);
            var impactTq = Utilities.GetTorque(_centerOfMass.Position, impactPos, forceVec);

            this.RotationSpeed += impactTq / this.GetInertia(this.Mass);
            this.Velocity += forceVec / this.Mass;
        }

        /// <summary>
        /// Adds cosmetic impact (bullet holes/flames) without doing damage or impulse.
        /// </summary>
        /// <param name="impactPos"></param>
        public void AddImpact(D2DPoint impactPos, float angle)
        {
            var ogPos = Utilities.ScaleToOrigin(this, impactPos);
            AddBulletHole(ogPos, angle);
        }

        private void SpawnDebris(int num, D2DPoint pos, D2DColor color)
        {
            for (int i = 0; i < num; i++)
            {
                var debris = World.ObjectManager.RentDebris();
                debris.ReInit(this, pos, this.Velocity, color);

                World.ObjectManager.EnqueueDebris(debris);
            }
        }

        public void DoHitGround()
        {
            if (!_easePhysicsComplete)
                return;

            if (_isAIPlane && !_expireTimeout.IsRunning)
                _expireTimeout.Restart();

            if (!IsDisabled)
            {
                PlayerCrashedCallback?.Invoke(this);
                Deaths++;
            }

            _thrustAmt.SetNow(0f);
            HasCrashed = true;
            IsDisabled = true;
            _flipTimer.Stop();
            Health = 0;
        }

        public void RespawnPlane(D2DPoint location)
        {
            this.SetPosition(location);

            RespawnQueued = false;
            IsDisabled = false;
            HasCrashed = false;
            EngineDamaged = false;
            GunDamaged = false;
            _expireTimeout.Stop();
            _flipTimer.Restart();
            _bulletHoles.ForEach(b => b.Dispose());
            _bulletHoles.Clear();
            WasHeadshot = false;
            _easePhysicsComplete = false;
            _easePhysicsTimer.Restart();
            AIRespawnReady = false;
            _engineFireFlame.StopSpawning();
            _engineOutSpoolDown.Stop();
            _engineOutSpoolDown.Reset();
            _thrustAmt.SetTarget(1f);
            DeathTime = 0f;
            Health = MAX_HEALTH;
            NumBullets = MAX_BULLETS;
            NumMissiles = MAX_MISSILES;
            NumDecoys = MAX_DECOYS;


            _wings.ForEach(w => w.Visible = true);

            this.Polygon.Restore();

            if (IsAI)
                _aiBehavior.ClearTarget();

            // Spawn the plane pointing in the desired direction.
            if (Utilities.IsPointingRight(PlayerGuideAngle))
                Rotation = 0f;
            else
                Rotation = 180f;

            Velocity = Utilities.AngleToVectorDegrees(Rotation, World.PlaneSpawnVelo);

            this.UpdateAllAttachments(0f);
        }

        public void MoveThrottle(bool up)
        {
            const int DETENTS = 6;


            if (up)
            {
                _throttlePos += 1;
            }
            else
            {
                _throttlePos -= 1;
            }

            _throttlePos = Math.Clamp(_throttlePos, 0, DETENTS);
            var amt = (1f / (float)DETENTS) * _throttlePos;
            ThrustAmount = amt;
        }

        private void GetBulletHoleDrag(BulletHole hole, float dt, out D2DPoint force, out float torque)
        {
            const float DAMAGE_DRAG_AMT = 0.002f;
            const float DAMAGE_TQ_FACTOR = 2f;

            var hDens = World.GetAltitudeDensity(hole.Position);
            var hVelo = Utilities.PointVelocity(this, hole.Position);

            if (hVelo.Length() == 0f)
            {
                force = D2DPoint.Zero;
                torque = 0f;
                return;
            }

            // Apply turbulence.
            hVelo *= World.GetTurbulenceForPosition(hole.Position);

            var hVeloNorm = hVelo.Normalized();
            var hVeloMag = hVelo.Length();
            var hVeloMagSq = MathF.Pow(hVeloMag, 2f);
            var dAmt = DAMAGE_DRAG_AMT * 0.5f * hDens * hVeloMagSq;

            var dVec = -hVeloNorm * dAmt;
            var dTq = Utilities.GetTorque(_centerOfMass.Position, hole.Position, dVec);

            dTq *= DAMAGE_TQ_FACTOR;

            force = dVec;
            torque = dTq;
            return;
        }

        private void FlipPoly(Direction direction)
        {
            if (_queuedDir != direction)
            {
                _flipTimer.Reset();
                _flipTimer.TriggerCallback = () => FlipPoly();
                _flipTimer.Start();
                _queuedDir = direction;
            }
        }

        public void FlipPoly(bool force = false)
        {
            if (!force && (_currentDir == _queuedDir || this.HasCrashed || this.IsDisabled))
                return;

            this.FlipY();

            if (_currentDir == Direction.Right)
                _currentDir = Direction.Left;
            else if (_currentDir == Direction.Left)
                _currentDir = Direction.Right;
        }

        private void CheckForFlip()
        {
            var pointingRight = Utilities.ClampAngle180(_gForceDirection - this.Rotation) < 0f;
            FlipPoly(pointingRight ? Direction.Right : Direction.Left);
        }

        private D2DPoint GetThrust(bool thrustVector = false)
        {
            var thrust = D2DPoint.Zero;

            const float thrustVectorAmt = 1f;
            const float thrustBoostMaxSpd = 600f;
            const float MAX_VELO = 2500f;

            if (thrustVector)
                thrust = Utilities.AngleToVectorDegrees(this.Rotation + (NozzleDeflection * thrustVectorAmt));
            else
                thrust = Utilities.AngleToVectorDegrees(this.Rotation);

            // Add a boost effect as speed increases. Jet engines make more power at higher speeds right?
            var velo = this.Velocity.Length();
            var boostFact = Utilities.Factor(velo, thrustBoostMaxSpd);
            var maxVeloFact = 1f - Utilities.Factor(velo, MAX_VELO);

            var densAlt = World.GetAltitudeDensity(this.Position);
            thrust *= _thrustAmt.Value * ((this.Thrust + (THRUST_BOOST_AMT * boostFact)) * densAlt);
            thrust *= maxVeloFact; // Reduce thrust as we approach max velo.

            return thrust;
        }

        public override void Dispose()
        {
            base.Dispose();

            _polyClipLayer?.Dispose();
            _bulletHoles.ForEach(b => b.Dispose());
            _bulletHoles.Clear();
            _engineFireFlame?.Dispose();
        }
    }
}
