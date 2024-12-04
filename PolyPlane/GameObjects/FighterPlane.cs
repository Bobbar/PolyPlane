using PolyPlane.AI_Behavior;
using PolyPlane.GameObjects.Animations;
using PolyPlane.GameObjects.Fixtures;
using PolyPlane.GameObjects.Guidance;
using PolyPlane.GameObjects.Interfaces;
using PolyPlane.GameObjects.Manager;
using PolyPlane.GameObjects.Tools;
using PolyPlane.Helpers;
using PolyPlane.Rendering;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public class FighterPlane : GameObjectPoly, ICollidable, IPushable
    {
        public Gun Gun => _gun;
        public bool IsAI => _isAIPlane;
        public D2DPoint GunPosition => _gun.Position;
        public D2DPoint ExhaustPosition => _centerOfThrust.Position;
        public Direction FlipDirection => _currentDir;
        public float GForce => _gForce;

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
            set { _thrustAmt.Set(value); }
        }

        public int NumMissiles
        {
            get { return _numMissiles; }
            set { _numMissiles = Math.Clamp(value, 0, MAX_MISSILES); }
        }

        public int NumBullets
        {
            get { return _numBullets; }
            set { _numBullets = Math.Clamp(value, 0, MAX_BULLETS); }
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
        public float Thrust { get; set; } = 2000f;
        public bool FiringBurst { get; set; } = false;
        public bool DroppingDecoy { get; set; } = false;
        public bool ThrustOn { get; set; } = true;
        public bool EngineDamaged { get; set; } = false;
        public bool HasCrashed { get; set; } = false;
        public bool WasHeadshot { get; set; } = false;
        public bool IsDisabled { get; set; } = false;

        public bool HasRadarLock = false;
        public bool AIRespawnReady = false;
        public string PlayerName = "Player";
        public float PlayerGuideAngle = 0;
        public float Deflection = 0f;
        public int BulletsFired = 0;
        public int MissilesFired = 0;
        public int DecoysDropped = 0;
        public int BulletsHit = 0;
        public int MissilesHit = 0;
        public int Kills = 0;
        public int Headshots = 0;
        public int Deaths = 0;

        public const int MAX_DECOYS = 15;
        public const int MAX_BULLETS = 30;
        public const int MAX_MISSILES = 6;
        public const float MAX_HEALTH = 32f;
        public const float MISSILE_DAMAGE = 32;
        public const float BULLET_DAMAGE = 4;

        public Action<Bullet> FireBulletCallback
        {
            get { return _fireBulletCallback; }
            set
            {
                _fireBulletCallback = value;
                _gun.FireBulletCallback = value;
            }
        }

        public Action<Decoy> DropDecoyCallback
        {
            get { return _dropDecoyCallback; }

            set
            {
                _dropDecoyCallback = value;
                _decoyDispenser.DropDecoyCallback = value;
            }
        }

        public Action<GuidedMissile> FireMissileCallback { get; set; }
        public Action<FighterPlane, GameObject> PlayerKilledCallback { get; set; }
        public Action<FighterPlane> PlayerCrashedCallback { get; set; }
        public Action<ImpactEvent> PlayerHitCallback { get; set; }

        private Action<Decoy> _dropDecoyCallback;
        private Action<Bullet> _fireBulletCallback;

        private IAIBehavior _aiBehavior;
        private List<Wing> _wings = new List<Wing>();
        private Wing? _controlWing = null;
        private RateLimiter _thrustAmt = new RateLimiter(0.5f);
        private Direction _currentDir = Direction.Right;
        private Direction _queuedDir = Direction.Right;
        private bool _isAIPlane = false;

        private GameTimer _flipTimer = new GameTimer(2f);
        private GameTimer _expireTimeout = new GameTimer(40f);
        private GameTimer _isLockOntoTimeout = new GameTimer(3f);
        private GameTimer _bulletRegenTimer = new GameTimer(0.2f, true);
        private GameTimer _decoyRegenTimer = new GameTimer(0.6f, true);
        private GameTimer _missileRegenTimer = new GameTimer(60f, true);
        private GameTimer _easePhysicsTimer = new GameTimer(5f, true);
        private GameTimer _groundDustSpawnTimer = new GameTimer(0.03f, true);
        private FloatAnimation _engineOutSpoolDown;

        private bool _easePhysicsComplete = false;
        private float _damageDeflection = 0f;
        private float _gForce = 0f;
        private float _gForceDirection = 0f;
        private float _health = MAX_HEALTH;
        private int _throttlePos = 0;
        private int _numMissiles = MAX_MISSILES;
        private int _numBullets = MAX_BULLETS;
        private int _numDecoys = MAX_DECOYS;
        private SmoothFloat _gforceAvg = new SmoothFloat(8);

        private const float POLY_TESSELLATE_DIST = 2f; // Tessellation amount. Smaller = higher resolution.
        private const float BULLET_DISTORT_AMT = 4f;
        private const float MISSILE_DISTORT_AMT = 7f;

        private RenderPoly FlamePoly;
        private D2DLayer _polyClipLayer = null;
        private D2DColor _flameFillColor = new D2DColor(0.6f, D2DColor.Yellow);
        private D2DColor _planeColor = D2DColor.White;
        private D2DColor _cockpitColor = new D2DColor(0.5f, D2DColor.LightBlue);
        private D2DSize _cockpitSize = new D2DSize(9f, 6f);

        private Gun _gun;
        private DecoyDispenser _decoyDispenser;
        private FixturePoint _flamePos;
        private FixturePoint _cockpitPosition;
        private FixturePoint _centerOfThrust;
        private FixturePoint _centerOfMass;
        private FlameEmitter _engineFireFlame;

        private List<BulletHole> _bulletHoles = new List<BulletHole>();
        private List<Vapor> _vaporTrails = new List<Vapor>();

        private readonly D2DPoint[] _planePoly =
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

        private readonly D2DPoint[] _flamePoly =
        [
            new D2DPoint(-8, 1),
            new D2DPoint(-10, 0),
            new D2DPoint(-8, -1),
        ];

        public FighterPlane(D2DPoint pos, AIPersonality personality, int playerId) : base(pos)
        {
            this.PlayerID = playerId;
            _aiBehavior = new FighterPlaneAI(this, personality);
            _isAIPlane = true;
            _thrustAmt.Target = 1f;
            _planeColor = D2DColor.Randomly();

            InitStuff();
        }

        public FighterPlane(D2DPoint pos, D2DColor color, int playerId, bool isAI = false, bool isNetPlane = false) : base(pos)
        {
            this.PlayerID = playerId;
            _thrustAmt.Target = 1f;
            IsNetObject = isNetPlane;
            _isAIPlane = isAI;
            _planeColor = color;

            if (isAI)
            {
                const int NUM_PERS = 2;
                var personality = Utilities.GetRandomPersonalities(NUM_PERS);

                _aiBehavior = new FighterPlaneAI(this, personality);
            }

            InitStuff();
        }

        public FighterPlane(D2DPoint pos, D2DColor color, GameID id, bool isAI = false, bool isNetPlane = false) : base(pos)
        {
            this.ID = id;
            _thrustAmt.Target = 1f;
            IsNetObject = isNetPlane;
            _isAIPlane = isAI;
            _planeColor = color;

            if (isAI)
            {
                const int NUM_PERS = 2;
                var personality = Utilities.GetRandomPersonalities(NUM_PERS);

                _aiBehavior = new FighterPlaneAI(this, personality);
            }

            InitStuff();
        }

        private void InitStuff()
        {
            this.Radar = new Radar(this);

            this.Mass = 90f;
            this.RenderScale = 1.5f;
            this.RenderOrder = 5;

            this.Polygon = new RenderPoly(this, _planePoly, this.RenderScale, POLY_TESSELLATE_DIST);
            this.FlamePoly = new RenderPoly(this, _flamePoly, new D2DPoint(12f, 0), this.RenderScale);

            InitWings();

            _centerOfThrust = new FixturePoint(this, new D2DPoint(-26.6f * this.RenderScale, 0.7f));
            _flamePos = new FixturePoint(this, new D2DPoint(-41f, 0.7f));
            _cockpitPosition = new FixturePoint(this, new D2DPoint(19.5f, -5f));
            _gun = new Gun(this, new D2DPoint(35f, 0), FireBulletCallback);
            _decoyDispenser = new DecoyDispenser(this, new D2DPoint(-24f, 0f));
            _engineFireFlame = new FlameEmitter(_centerOfThrust, D2DPoint.Zero, false);
            _engineFireFlame.Owner = this;
            _engineFireFlame.StopSpawning();

            _flamePos.IsNetObject = this.IsNetObject;
            _cockpitPosition.IsNetObject = this.IsNetObject;

            _expireTimeout.TriggerCallback = () =>
            {
                if (!World.RespawnAIPlanes)
                    this.IsExpired = true;
                else
                    this.AIRespawnReady = true;
            };

            _bulletRegenTimer.TriggerCallback = () =>
            {
                if (NumBullets < MAX_BULLETS)
                    NumBullets++;
            };


            _missileRegenTimer.TriggerCallback = () =>
            {
                if (World.MissileRegen)
                    if (NumMissiles < MAX_MISSILES)
                        NumMissiles++;
            };

            _missileRegenTimer.Start();

            _decoyRegenTimer.TriggerCallback = () =>
            {
                if (NumDecoys < MAX_DECOYS)
                    NumDecoys++;
            };

            _groundDustSpawnTimer.TriggerCallback = DoGroundDustEffect;

            _groundDustSpawnTimer.Start();
            _bulletRegenTimer.Start();
            _decoyRegenTimer.Start();
            _easePhysicsTimer.Start();

            _isLockOntoTimeout.TriggerCallback = () => HasRadarLock = false;
            _easePhysicsTimer.TriggerCallback = () => _easePhysicsComplete = true;

            _engineOutSpoolDown = new FloatAnimation(1f, 0f, 20f, EasingFunctions.EaseLinear, v =>
            {
                if (!this.IsDisabled)
                    _thrustAmt.Set(v);
            });
        }

        private void InitWings()
        {
            const float DEFLECT_RATE = 55f;
            const float MIN_VELO = 300f;

            // Main wing.
            AddWing(new Wing(this, new WingParameters()
            {
                RenderLength = 10f * this.RenderScale,
                RenderWidth = 3f,
                Area = 0.5f,
                MaxLiftForce = 15000f,
                MaxDragForce = 12000f,
                AOAFactor = 0.6f,
                MaxAOA = 25f,
                Position = new D2DPoint(-5f * this.RenderScale, 0.6f * this.RenderScale),
                MinVelo = MIN_VELO
            }));

            // Tail wing. (Control wing)
            AddWing(new Wing(this, new WingParameters()
            {
                RenderLength = 5f * this.RenderScale,
                RenderWidth = 3f,
                Area = 0.2f,
                MaxDeflection = 40f,
                MaxLiftForce = 7000f,
                MaxDragForce = 7000f,
                AOAFactor = 0.4f,
                MaxAOA = 30f,
                DeflectionRate = DEFLECT_RATE,
                PivotPoint = new D2DPoint(-25f * this.RenderScale, 0.6f * this.RenderScale),
                Position = new D2DPoint(-27.5f * this.RenderScale, 0.6f * this.RenderScale),
                MinVelo = MIN_VELO
            }), isControl: true);

            // Center of mass location.
            _centerOfMass = new FixturePoint(this, new D2DPoint(-5f, 0f));
        }

        public override void Update(float dt)
        {
            for (int i = 0; i < World.PHYSICS_SUB_STEPS; i++)
            {
                var partialDT = World.SUB_DT;

                var wingForce = D2DPoint.Zero;
                var wingTorque = 0f;
                var guideRot = this.Rotation;

                // Get guidance direction.
                if (_isAIPlane)
                    guideRot = GetAPGuidanceDirection(_aiBehavior.GetAIGuidance());
                else
                    guideRot = GetAPGuidanceDirection(PlayerGuideAngle);

                // Deflection direction.
                var deflection = Utilities.ClampAngle180(guideRot);

                if (!this.IsNetObject)
                {
                    if (!this.IsDisabled)
                        _controlWing.Deflection = deflection;
                    else
                        _controlWing.Deflection = _damageDeflection;
                }

                // Update
                base.Update(partialDT);
                _wings.ForEach(w => w.Update(partialDT));
                _centerOfThrust.Update(partialDT);
                _centerOfMass.Update(partialDT);

                // Wing force and torque.
                foreach (var wing in _wings)
                {
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

                if (World.BulletHoleDrag)
                {
                    foreach (var hole in _bulletHoles)
                    {
                        GetBulletHoleDrag(hole, partialDT, out D2DPoint dVec, out float dTq);
                        damageTorque += dTq;
                        damageForce += dVec;
                    }
                }

                // Apply disabled effects.
                if (IsDisabled)
                {
                    wingForce *= 0.1f;
                    wingTorque *= 0.1f;

                    damageTorque *= 0.1f;
                    damageForce *= 0.1f;

                    ThrustOn = false;
                    _thrustAmt.Set(0f);
                    FiringBurst = false;
                    _engineFireFlame.StartSpawning();
                }

                var thrust = GetThrust(true);

                if (!this.IsNetObject)
                {
                    Deflection = _controlWing.Deflection;

                    // Ease in physics.
                    var easeFact = 1f;

                    if (!_easePhysicsComplete)
                        _easePhysicsTimer.Start();

                    if (!_easePhysicsComplete && _easePhysicsTimer.IsRunning)
                        easeFact = Utilities.Factor(_easePhysicsTimer.Value, _easePhysicsTimer.Interval);

                    // Integrate torque, thrust and wing force.
                    var thrustTorque = Utilities.GetTorque(_centerOfMass.Position, _centerOfThrust.Position, thrust);
                    var rotAmt = ((wingTorque + thrustTorque + damageTorque) * easeFact) / this.GetInertia(this.Mass);

                    this.RotationSpeed += rotAmt * partialDT;
                    this.Velocity += (thrust * easeFact) / this.Mass * partialDT;
                    this.Velocity += (wingForce * easeFact) / this.Mass * partialDT;
                    this.Velocity += (damageForce * easeFact) / this.Mass * partialDT;
                    this.Velocity += (World.Gravity * partialDT);
                }

                // Compute g-force.
                var totForce = (thrust / this.Mass * partialDT) + (wingForce / this.Mass * partialDT);
                var gforce = totForce.Length() / partialDT / World.Gravity.Y;
                _gforceAvg.Add(gforce);
                _gForceDirection = totForce.Angle();
            }

            // Check for wing and engine damage.
            // If the plane polygon gets distorted to the point
            // that a wing attachment or engine are no longer
            // within the polygon, we consider them damaged.
            foreach (var wing in _wings)
            {
                if (wing.Visible && !Utilities.PointInPoly(wing.PivotPoint.Position, this.Polygon.Poly))
                {
                    wing.Visible = false;

                    SpawnDebris(1, wing.Position, D2DColor.Gray);
                }
            }

            // Check for engine damage.
            if (this.ThrustOn && !this.EngineDamaged && !Utilities.PointInPoly(_centerOfThrust.Position, this.Polygon.Poly))
            {
                _engineFireFlame.StartSpawning();
                this.EngineDamaged = true;

                // Spool down thrust gradually.
                _engineOutSpoolDown.Start();
            }

            _gForce = _gforceAvg.Current;

            // Update all the low frequency stuff.
            _flamePos.Update(dt);
            _cockpitPosition.Update(dt);
            _thrustAmt.Update(dt);
            _gun.Update(dt);
            _decoyDispenser.Update(dt);
            _engineFireFlame.Update(dt);
            _bulletHoles.ForEach(f => f.Update(dt));
            _vaporTrails.ForEach(v => v.Update(dt));

            if (!this.IsNetObject)
                CheckForFlip();

            UpdateFlame();

            _easePhysicsTimer.Update(dt);
            _flipTimer.Update(dt);
            _isLockOntoTimeout.Update(dt);
            _expireTimeout.Update(dt);
            _groundDustSpawnTimer.Update(dt);
            _engineOutSpoolDown.Update(dt);

            this.Radar?.Update(dt);

            if (_aiBehavior != null)
                _aiBehavior.Update(dt);

            if (!this.FiringBurst)
                _bulletRegenTimer.Update(dt);

            _missileRegenTimer.Update(dt);

            if (!this.DroppingDecoy)
                _decoyRegenTimer.Update(dt);

            if (_thrustAmt.Value < 0.01f)
                this.ThrustOn = false;

            this.RecordHistory();
        }

        public override void NetUpdate(float dt, D2DPoint position, D2DPoint velocity, float rotation, double frameTime)
        {
            base.NetUpdate(dt, position, velocity, rotation, frameTime);

            _controlWing.Deflection = this.Deflection;
        }

        private void UpdateFlame()
        {
            // Fiddle with flame angle, length and color.
            var thrust = GetThrust(true);
            var thrustMag = thrust.Length();
            var flameAngle = thrust.Angle();
            var len = this.Velocity.Length() * 0.05f;
            len += thrustMag * 0.01f;
            len *= 0.6f;
            FlamePoly.SourcePoly[1].X = -_rnd.NextFloat(9f + len, 11f + len);
            _flameFillColor.g = _rnd.NextFloat(0.6f, 0.86f);

            FlamePoly.Update(_flamePos.Position, flameAngle, this.RenderScale);
        }

        private void DoGroundDustEffect()
        {
            // Spawn dust particles when very near the ground.
            const float DUST_ALT = 400f;
            const float MIN_VELO = 100f;

            if (this.Altitude < DUST_ALT && this.Velocity.Length() > MIN_VELO)
            {
                var dustColor = new D2DColor(1f, 0.35f, 0.2f, 0.1f);
                var altFact = 1f - Utilities.FactorWithEasing(this.Altitude, DUST_ALT, EasingFunctions.EaseLinear);
                var alpha = Math.Clamp(altFact, 0f, 0.5f);
                var radius = 15f * (altFact) + Utilities.Rnd.NextFloat(1f, 3f);
                var velo = (this.Velocity + (this.Velocity * 0.4f)) + new D2DPoint(Utilities.Rnd.NextFloat(-150f, 150f), 0f);
                var groundPos = new D2DPoint(this.Position.X - (this.Velocity.X * 0.2f) + Utilities.Rnd.NextFloat(-100f, 100f), 0f);

                Particle.SpawnParticle(this, groundPos, velo, radius, dustColor.WithAlpha(alpha), dustColor.WithAlpha(1f));
            }
        }

        public override void Render(RenderContext ctx)
        {
            base.Render(ctx);

            _vaporTrails.ForEach(v => v.Render(ctx));


            if (_thrustAmt.Value > 0f && GetThrust(true).Length() > 0f)
                ctx.DrawPolygon(this.FlamePoly.Poly, _flameFillColor, 1f, D2DDashStyle.Solid, _flameFillColor);

            if (!this.IsDisabled)
                DrawShockwave(ctx);

            ctx.DrawPolygon(this.Polygon.Poly, D2DColor.Black.WithAlpha(0.3f), 0.5f, D2DDashStyle.Solid, _planeColor);
            DrawClippedObjects(ctx);
            _wings.ForEach(w => w.Render(ctx));
            _gun.Render(ctx);


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
            const float MIN_VELO = 850f;
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
            foreach (var hole in _bulletHoles)
            {
                if (!ctx.Viewport.Contains(hole.Position))
                    return;

                ctx.Gfx.PushTransform();
                ctx.Gfx.RotateTransform(hole.Rotation, hole.Position);

                hole.Render(ctx);

                ctx.Gfx.PopTransform();
            }
        }

        private void DrawCockpit(RenderContext ctx)
        {
            if (!ctx.Viewport.Contains(_cockpitPosition.Position))
                return;

            ctx.Gfx.PushTransform();
            ctx.Gfx.RotateTransform(_cockpitPosition.Rotation, _cockpitPosition.Position);

            var cockpitEllipse = new D2DEllipse(_cockpitPosition.Position, _cockpitSize);
            ctx.Gfx.FillEllipse(cockpitEllipse, WasHeadshot ? D2DColor.DarkRed : _cockpitColor);
            ctx.Gfx.DrawEllipse(cockpitEllipse, D2DColor.Black, 0.5f);

            ctx.Gfx.PopTransform();
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

            var missile = new GuidedMissile(this, target, GuidanceType.Advanced, useControlSurfaces: true, useThrustVectoring: true);

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

            _vaporTrails.Add(new Vapor(wing, this, D2DPoint.Zero, 8f, VAPOR_TRAIL_GS, VAPOR_TRAIL_VELO, MAX_GS));

            _wings.Add(wing);
        }

        /// <summary>
        /// Update FixturePoint objects to move them to the current position.
        /// </summary>
        public void SyncFixtures()
        {
            this.Polygon.Update();
            _flamePos.Update(0f);
            _bulletHoles.ForEach(f => f.Update(0f));
            _centerOfThrust.Update(0f);
            _centerOfMass.Update(0f);
            _cockpitPosition.Update(0f);
            _gun.Update(0f);
            this._wings.ForEach(w => w.Update(0f));
        }

        public void AddBulletHole(D2DPoint pos, float angle, float distortAmt = 3f)
        {
            // Find the closest poly point to the impact and distort the polygon.
            // Adds a "dent" basically.
            var distortVec = Utilities.AngleToVectorDegrees(angle, distortAmt);
            var closestIdx = this.Polygon.ClosestIdx(pos);

            // Distort the closest point and the two surrounding points.
            var prevIdx = Utilities.WrapIndex(closestIdx - 1, this.Polygon.Poly.Length);
            var nextIdx = Utilities.WrapIndex(closestIdx + 1, this.Polygon.Poly.Length);

            this.Polygon.SourcePoly[prevIdx] += distortVec * 0.6f;
            this.Polygon.SourcePoly[closestIdx] += distortVec;
            this.Polygon.SourcePoly[nextIdx] += distortVec * 0.6f;

            this.Polygon.Update();

            var bulletHole = new BulletHole(this, pos + distortVec, angle);
            bulletHole.IsNetObject = this.IsNetObject;
            _bulletHoles.Add(bulletHole);
        }

        public void HandleImpactResult(GameObject impactor, PlaneImpactResult result)
        {
            var attackPlane = impactor.Owner as FighterPlane;

            // Always change target to attacking plane?
            if (this.IsAI)
                _aiBehavior.ChangeTarget(attackPlane);

            if (result.Type != ImpactType.Splash)
            {
                if (impactor is Bullet)
                    attackPlane.BulletsHit++;
                else if (impactor is GuidedMissile)
                    attackPlane.MissilesHit++;

                // Scale the impact position back to the origin of the polygon.
                var ogPos = Utilities.ScaleToOrigin(this, result.ImpactPoint);
                var angle = result.ImpactAngle;

                var distortAmt = BULLET_DISTORT_AMT;
                if (impactor is GuidedMissile)
                    distortAmt = MISSILE_DISTORT_AMT;

                AddBulletHole(ogPos, angle, distortAmt);

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

                    if (result.Type == ImpactType.Missile)
                        SpawnDebris(4, result.ImpactPoint, this.PlaneColor);
                    else if (result.Type == ImpactType.Bullet)
                        SpawnDebris(1, result.ImpactPoint, this.PlaneColor);
                }

                // TODO: How to handle this during net games?
                // It's possible for a delayed packet to reset the
                // Health and IsDisabled flags after a kill has been recorded,
                // which can cause duplicate kill events.
                if (this.Health <= 0 && !this.IsDisabled)
                {
                    DoPlayerKilled(impactor);
                }
            }

            PlayerHitCallback?.Invoke(new ImpactEvent(this, attackPlane));

        }

        public PlaneImpactResult GetImpactResult(GameObject impactor, D2DPoint impactPos)
        {
            // Make sure cockpit position is up-to-date.
            _cockpitPosition.Update(0f);

            var angle = Utilities.ClampAngle((impactor.Velocity - this.Velocity).Angle() - this.Rotation);
            var result = new PlaneImpactResult();
            result.ImpactPoint = impactPos;
            result.ImpactAngle = angle;

            if (!IsDisabled)
            {
                if (this.Health > 0)
                {
                    var distortAmt = BULLET_DISTORT_AMT;
                    if (impactor is GuidedMissile)
                        distortAmt = MISSILE_DISTORT_AMT;

                    var distortVec = Utilities.AngleToVectorDegrees(angle + this.Rotation, distortAmt);
                    var cockpitEllipse = new D2DEllipse(_cockpitPosition.Position, _cockpitSize);

                    var hitCockpit = CollisionHelpers.EllipseContains(cockpitEllipse, _cockpitPosition.Rotation, impactPos + distortVec);
                    if (hitCockpit)
                    {
                        result.WasHeadshot = true;
                    }

                    if (impactor is GuidedMissile)
                    {
                        result.Type = ImpactType.Missile;
                        result.DamageAmount = MISSILE_DAMAGE;
                    }
                    else
                    {
                        result.Type = ImpactType.Bullet;
                        result.DamageAmount = BULLET_DAMAGE;
                    }
                }
            }

            return result;
        }

        public void DoPlayerKilled(GameObject impactor)
        {
            if (IsDisabled)
                return;

            IsDisabled = true;
            _damageDeflection = _controlWing.Deflection;

            if (impactor.Owner is FighterPlane attackPlane)
                attackPlane.Kills++;

            Deaths++;

            PlayerKilledCallback?.Invoke(this, impactor);
        }

        private void DoImpactImpulse(GameObject impactor, D2DPoint impactPos)
        {
            if (this.IsNetObject)
                return;

            const float IMPACT_MASS = 160f;

            var velo = impactor.Velocity - this.Velocity;
            var force = (IMPACT_MASS * velo.Length()) / 4f;
            var forceVec = (velo.Normalized() * force);
            var impactTq = Utilities.GetTorque(_centerOfMass.Position, impactPos, forceVec);

            this.RotationSpeed += (float)(impactTq / this.GetInertia(this.Mass) * World.DT);
            this.Velocity += forceVec / this.Mass * World.DT;
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
                var debris = new Debris(this, pos, this.Velocity, color);
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

            HasCrashed = true;
            IsDisabled = true;
            _flipTimer.Stop();
            Health = 0;
        }

        public void FixPlane()
        {
            Health = MAX_HEALTH;
            NumBullets = MAX_BULLETS;
            NumMissiles = MAX_MISSILES;
            NumDecoys = MAX_DECOYS;
            IsDisabled = false;
            HasCrashed = false;
            ThrustOn = true;
            EngineDamaged = false;
            _expireTimeout.Stop();
            _flipTimer.Restart();
            _bulletHoles.Clear();
            WasHeadshot = false;
            PlayerGuideAngle = 0f;
            _easePhysicsComplete = false;
            _easePhysicsTimer.Restart();
            AIRespawnReady = false;
            _engineFireFlame.StopSpawning();
            _engineOutSpoolDown.Stop();
            _engineOutSpoolDown.Reset();
            _thrustAmt.Target = 1f;

            _wings.ForEach(w => w.Visible = true);

            var flipped = this.Polygon.IsFlipped;

            this.Polygon = new RenderPoly(this, _planePoly, this.RenderScale, POLY_TESSELLATE_DIST);

            if (flipped)
                this.Polygon.FlipY();

            if (IsAI)
                _aiBehavior.ClearTarget();

            this.SyncFixtures();
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
            _thrustAmt.Target = amt;
        }

        private void GetBulletHoleDrag(BulletHole hole, float dt, out D2DPoint force, out float torque)
        {
            const float DAMAGE_DRAG_AMT = 0.002f;
            const float DAMAGE_TQ_FACTOR = 2f;

            var hDens = World.GetDensityAltitude(hole.Position);
            var hVelo = Utilities.AngularVelocity(this, hole.Position);

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
            var hVeloMagSq = (float)Math.Pow(hVeloMag, 2f);
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

            _wings.ForEach(w => w.FlipY());
            _wings.ForEach(w => w.Update(World.SUB_DT));
            _vaporTrails.ForEach(v => v.FlipY());
            _flamePos.FlipY();
            _bulletHoles.ForEach(f => f.FlipY());
            _cockpitPosition.FlipY();
            _gun.FlipY();
            _centerOfThrust.FlipY();
            _centerOfMass.FlipY();

            if (_currentDir == Direction.Right)
                _currentDir = Direction.Left;
            else if (_currentDir == Direction.Left)
                _currentDir = Direction.Right;
        }

        private void CheckForFlip()
        {
            var pointingRight = Utilities.ClampAngle180(_gForceDirection - this.Rotation) < 0f;

            // For net planes we don't have an accurate g-force measurement,
            // so estimate using the current velocity angle.
            if (this.IsNetObject)
                pointingRight = Utilities.ClampAngle180((this.Velocity.Angle() + 180f) - this.Rotation) < 0f;

            FlipPoly(pointingRight ? Direction.Right : Direction.Left);
        }

        private D2DPoint GetThrust(bool thrustVector = false)
        {
            var thrust = D2DPoint.Zero;

            const float thrustVectorAmt = 1f;
            const float thrustBoostAmt = 1000f;
            const float thrustBoostMaxSpd = 400f;
            const float MAX_VELO = 2500f;

            if (thrustVector)
                thrust = Utilities.AngleToVectorDegrees(this.Rotation + (_controlWing.Deflection * thrustVectorAmt));
            else
                thrust = Utilities.AngleToVectorDegrees(this.Rotation);

            if (!ThrustOn)
                return thrust;

            // Add a boost effect as speed increases. Jet engines make more power at higher speeds right?
            var velo = this.Velocity.Length();
            var boostFact = Utilities.Factor(velo, thrustBoostMaxSpd);
            var maxVeloFact = 1f - Utilities.Factor(velo, MAX_VELO);

            thrust *= _thrustAmt.Value * ((this.Thrust + (thrustBoostAmt * boostFact)) * World.GetDensityAltitude(this.Position));
            thrust *= maxVeloFact; // Reduce thrust as we approach max velo.

            return thrust;
        }

        public override void Dispose()
        {
            base.Dispose();

            _polyClipLayer?.Dispose();
            //_bulletHoles.ForEach(b => b.Dispose());
            _bulletHoles.Clear();
            _vaporTrails.Clear();
            _engineFireFlame?.Dispose();
        }
    }
}
