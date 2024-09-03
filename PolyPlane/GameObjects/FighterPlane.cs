﻿using PolyPlane.AI_Behavior;
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
    public class FighterPlane : GameObjectPoly, ICollidable
    {
        public Gun Gun => _gun;

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

        public bool AIRespawnReady = false;
        public Direction FlipDirection => _currentDir;
        public string PlayerName;
        public float PlayerGuideAngle = 0;

        public int NumMissiles { get { return _numMissiles; } set { _numMissiles = Math.Clamp(value, 0, MAX_MISSILES); } }
        public int NumBullets { get { return _numBullets; } set { _numBullets = Math.Clamp(value, 0, MAX_BULLETS); } }
        public int NumDecoys { get { return _numDecoys; } set { _numDecoys = Math.Clamp(value, 0, MAX_DECOYS); } }
        public float Health { get { return _health; } set { _health = Math.Clamp(value, 0, MAX_HEALTH); } }

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

        public bool IsAI => _isAIPlane;

        public D2DColor PlaneColor
        {
            get { return _planeColor; }
            set { _planeColor = value; }
        }

        public float Thrust { get; set; } = 2000f;
        public bool FiringBurst { get; set; } = false;
        public bool DroppingDecoy { get; set; } = false;
        public float GForce => _gForce;
        public bool ThrustOn { get; set; } = false;
        public float ThrustAmount
        {
            get { return _thrustAmt.Value; }

            set { _thrustAmt.Set(value); }
        }
        public bool AutoPilotOn { get; set; } = false;
        public bool SASOn { get; set; } = false;
        public bool HasCrashed { get; set; } = false;
        public bool WasHeadshot { get; set; } = false;
        public D2DPoint GunPosition => _gun.Position;
        public D2DPoint ExhaustPosition => _centerOfThrust.Position;
        public bool IsDisabled { get; set; } = false;
        public Radar Radar { get; set; }
        public bool HasRadarLock = false;

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

        private Action<Decoy> _dropDecoyCallback;

        private Action<Bullet> _fireBulletCallback;
        public Action<GuidedMissile> FireMissileCallback { get; set; }
        public Action<FighterPlane, GameObject> PlayerKilledCallback { get; set; }
        public Action<FighterPlane> PlayerCrashedCallback { get; set; }


        public List<Wing> Wings = new List<Wing>();

        private Wing? _controlWing = null;
        private RateLimiter _thrustAmt = new RateLimiter(0.5f);
        private Direction _currentDir = Direction.Right;
        private Direction _queuedDir = Direction.Right;
        private bool _isAIPlane = false;
        private readonly float MASS = 90f;

        private float Inertia
        {
            get { return MASS * 20f; }
        }

        private GameTimer _flipTimer = new GameTimer(2f);
        private GameTimer _expireTimeout = new GameTimer(40f);
        private GameTimer _isLockOntoTimeout = new GameTimer(3f);
        private GameTimer _bulletRegenTimer = new GameTimer(0.2f, true);
        private GameTimer _decoyRegenTimer = new GameTimer(0.6f, true);
        private GameTimer _missileRegenTimer = new GameTimer(60f, true);
        private GameTimer _easePhysicsTimer = new GameTimer(5f, true);
        private bool _easePhysicsComplete = false;

        private float _damageDeflection = 0f;
        private float _gForce = 0f;
        private SmoothFloat _gforceAvg = new SmoothFloat(8);
        private int _throttlePos = 0;
        private int _numMissiles = MAX_MISSILES;
        private int _numBullets = MAX_BULLETS;
        private int _numDecoys = MAX_DECOYS;
        private float _health = MAX_HEALTH;

        private const float POLY_TESSELLATE_DIST = 2f; // Tessellation amount. Smaller = higher resolution.
        private const float BULLET_DISTORT_AMT = 4f;
        private const float MISSILE_DISTORT_AMT = 7f;

        private RenderPoly FlamePoly;
        private D2DLayer _polyClipLayer = null;
        private D2DColor _flameFillColor = new D2DColor(0.6f, D2DColor.Yellow);

        private Gun _gun;
        private DecoyDispenser _decoyDispenser;

        private FixturePoint _flamePos;
        private FixturePoint _cockpitPosition;
        private FixturePoint _centerOfThrust;
        private FixturePoint _centerOfMass;

        private D2DSize _cockpitSize = new D2DSize(9f, 6f);
        private D2DColor _planeColor;
        private D2DColor _cockpitColor = new D2DColor(0.5f, D2DColor.LightBlue);
        private SmokeTrail _contrail;
        private List<BulletHole> _bulletHoles = new List<BulletHole>();
        private List<Vapor> _vaporTrails = new List<Vapor>();
        private Flame _engineFireFlame;

        private IAIBehavior _aiBehavior;

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


        public FighterPlane(D2DPoint pos, AIPersonality personality) : base(pos)
        {
            _aiBehavior = new FighterPlaneAI(this, personality);
            _isAIPlane = true;
            _thrustAmt.Target = 1f;
            _planeColor = D2DColor.Randomly();

            InitStuff();
        }

        public FighterPlane(D2DPoint pos, D2DColor color, bool isAI = false, bool isNetPlane = false) : base(pos)
        {
            _thrustAmt.Target = 1f;
            IsNetObject = isNetPlane;
            _isAIPlane = isAI;
            _planeColor = color;
            ThrustOn = true;

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

            this.RenderOffset = 1.5f;

            this.Polygon = new RenderPoly(this, _planePoly, this.RenderOffset, POLY_TESSELLATE_DIST);
            this.FlamePoly = new RenderPoly(this, _flamePoly, new D2DPoint(12f, 0), this.RenderOffset);

            InitWings();

            _controlWing.Deflection = 2f;

            _centerOfThrust = new FixturePoint(this, new D2DPoint(-26.6f * this.RenderOffset, 0.7f));
            _flamePos = new FixturePoint(this, new D2DPoint(-41f, 0.7f));
            _cockpitPosition = new FixturePoint(this, new D2DPoint(19.5f, -5f));
            _gun = new Gun(this, new D2DPoint(35f, 0), FireBulletCallback);
            _decoyDispenser = new DecoyDispenser(this, new D2DPoint(-24f, 0f));
            _engineFireFlame = new Flame(_centerOfThrust, D2DPoint.Zero, true);
            _engineFireFlame.StopSpawning();

            _flamePos.IsNetObject = this.IsNetObject;
            _cockpitPosition.IsNetObject = this.IsNetObject;

            _contrail = new SmokeTrail(this, o =>
            {
                var p = o as FighterPlane;
                return p.ExhaustPosition;
            }, lineWeight: 8f);

            _contrail.IsNetObject = this.IsNetObject;

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
                if (NumMissiles < MAX_MISSILES)
                    NumMissiles++;
            };

            _missileRegenTimer.Start();

            _decoyRegenTimer.TriggerCallback = () =>
            {
                if (NumDecoys < MAX_DECOYS)
                    NumDecoys++;
            };

            _bulletRegenTimer.Start();
            _decoyRegenTimer.Start();
            _easePhysicsTimer.Start();

            _isLockOntoTimeout.TriggerCallback = () => HasRadarLock = false;
            _easePhysicsTimer.TriggerCallback = () => _easePhysicsComplete = true;
        }

        private void InitWings()
        {
            float defRate = 55f;

            // Main wing.
            AddWing(new Wing(this, new WingParameters()
            {
                RenderLength = 10f * this.RenderOffset,
                RenderWidth = 3f,
                Area = 0.5f,
                MaxLiftForce = 10000f,
                MaxDragForce = 17000f,
                AOAFactor = 0.6f,
                MaxAOA = 20f,
                Position = new D2DPoint(-2f * this.RenderOffset, 0.6f * this.RenderOffset),
                MinVelo = 450f
            }));

            // Tail wing. (Control wing)
            AddWing(new Wing(this, new WingParameters()
            {
                RenderLength = 5f * this.RenderOffset,
                RenderWidth = 3f,
                Area = 0.2f,
                MaxDeflection = 40f,
                MaxLiftForce = 5000f,
                MaxDragForce = 9000f,
                AOAFactor = 0.4f,
                MaxAOA = 30f,
                DeflectionRate = defRate,
                PivotPoint = new D2DPoint(-25f * this.RenderOffset, 0.6f * this.RenderOffset),
                Position = new D2DPoint(-27.5f * this.RenderOffset, 0.6f * this.RenderOffset),
                MinVelo = 450f
            }), isControl: true);

            // Center of mass location.
            _centerOfMass = new FixturePoint(this, new D2DPoint(-5f, 0f));
        }
        public override void Update(float dt, float renderScale)
        {
            renderScale *= this.RenderOffset;

            for (int i = 0; i < World.PHYSICS_SUB_STEPS; i++)
            {
                var partialDT = World.SUB_DT;

                base.Update(partialDT, renderScale);

                var wingForce = D2DPoint.Zero;
                var wingTorque = 0f;
                var deflection = this.Deflection;
                var thrust = GetThrust(true);

                if (AutoPilotOn)
                {
                    float guideRot = this.Rotation;

                    if (_isAIPlane)
                        guideRot = GetAPGuidanceDirection(_aiBehavior.GetAIGuidance());
                    else
                        guideRot = GetAPGuidanceDirection(PlayerGuideAngle);

                    var nextDeflect = Utilities.ClampAngle180(guideRot - this.Rotation);
                    deflection = nextDeflect;
                }

                float ogDef = deflection;

                // Apply some stability control to try to prevent thrust vectoring from spinning the plane.
                if (_thrustAmt.Value > 0f && SASOn)
                {
                    var velo = this.AirSpeedIndicated;

                    const float MIN_DEF_SPD = 300f; // Minimum speed required for full deflection.
                    var spdFact = Utilities.Factor(velo, MIN_DEF_SPD);

                    const float MAX_DEF_AOA = 40f;// Maximum AoA allowed. Reduce deflection as AoA increases.
                    var aoaFact = 1f - (Math.Abs(Wings[0].AoA) / (MAX_DEF_AOA + (spdFact * (MAX_DEF_AOA * 6f))));

                    const float MAX_DEF_ROT_SPD = 200f; // Maximum rotation speed allowed. Reduce deflection to try to control rotation speed.
                    var rotSpdFact = 1f - (Math.Abs(this.RotationSpeed) / (MAX_DEF_ROT_SPD + (spdFact * (MAX_DEF_ROT_SPD * 8f))));

                    // Ease out when thrust is decreasing.
                    deflection = Utilities.Lerp(ogDef, ogDef * aoaFact * rotSpdFact, _thrustAmt.Value);
                }

                if (float.IsNaN(deflection))
                    deflection = 0f;

                _controlWing.Deflection = deflection;

                foreach (var wing in Wings)
                {
                    // How much force a damaged wing contributes.
                    const float DAMAGED_FACTOR = 0.2f;

                    var force = wing.GetLiftDragForce();
                    var torque = GetTorque(wing, force);

                    if (wing.Visible)
                    {
                        wingForce += force;
                        wingTorque += torque;
                    }
                    else
                    {
                        wingForce += force * DAMAGED_FACTOR;
                        wingTorque += torque * DAMAGED_FACTOR;
                    }
                }

                if (IsDisabled)
                {
                    wingForce *= 0.1f;
                    wingTorque *= 0.1f;
                    AutoPilotOn = false;
                    SASOn = false;
                    ThrustOn = false;
                    _thrustAmt.Set(0f);
                    _controlWing.Deflection = _damageDeflection;
                    FiringBurst = false;
                    _engineFireFlame.StartSpawning();
                }

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
                    var thrustTorque = GetTorque(_centerOfThrust.Position, thrust);
                    var rotAmt = ((wingTorque + thrustTorque) * easeFact) / this.Inertia;
                    this.RotationSpeed += rotAmt * partialDT;
                    this.Velocity += (thrust * easeFact) / this.MASS * partialDT;
                    this.Velocity += (wingForce * easeFact) / this.MASS * partialDT;
                    this.Velocity += (World.Gravity * partialDT);
                }

                var totForce = (thrust / this.MASS * partialDT) + (wingForce / this.MASS * partialDT);
                var gforce = totForce.Length() / partialDT / World.Gravity.Y;
                _gforceAvg.Add(gforce);

                Wings.ForEach(w => w.Update(partialDT, renderScale));
                _centerOfThrust.Update(partialDT, renderScale);
                _centerOfMass.Update(partialDT, renderScale);
            }

            // Check for wing and engine damage.
            // If the plane polygon gets distorted to the point
            // that a wing attachment or engine are no longer
            // within the polygon, we consider them damaged.
            foreach (var wing in Wings)
            {
                if (wing.Visible && !Utilities.PointInPoly(wing.PivotPoint.Position, this.Polygon.Poly))
                {
                    wing.Visible = false;

                    SpawnDebris(1, wing.Position, D2DColor.Gray);
                }
            }

            if (!Utilities.PointInPoly(_centerOfThrust.Position, this.Polygon.Poly))
            {
                if (this.ThrustOn)
                {
                    this.ThrustOn = false;
                    _thrustAmt.Set(0f);
                }
            }

            _gForce = _gforceAvg.Current;

            _flamePos.Update(dt, renderScale);
            _cockpitPosition.Update(dt, renderScale);
            _thrustAmt.Update(dt);
            _gun.Update(dt, renderScale);
            _decoyDispenser.Update(dt, renderScale);
            _engineFireFlame.Update(dt, renderScale);

            if (!World.IsNetGame || World.IsClient)
            {
                _bulletHoles.ForEach(f => f.Update(dt, renderScale));
                _contrail.Update(dt, renderScale);
                _vaporTrails.ForEach(v => v.Update(dt, renderScale));
            }

            CheckForFlip();
            UpdateFlame(renderScale);

            _easePhysicsTimer.Update(dt);
            _flipTimer.Update(dt);
            _isLockOntoTimeout.Update(dt);
            _expireTimeout.Update(dt);

            this.Radar?.Update(dt, renderScale);

            if (_aiBehavior != null)
                _aiBehavior.Update(dt);

            if (!this.FiringBurst)
                _bulletRegenTimer.Update(dt);

            _missileRegenTimer.Update(dt);

            if (!this.DroppingDecoy)
                _decoyRegenTimer.Update(dt);

            this.RecordHistory();
        }

        public override void NetUpdate(float dt, D2DPoint position, D2DPoint velocity, float rotation, double frameTime)
        {
            base.NetUpdate(dt, position, velocity, rotation, frameTime);

            _controlWing.Deflection = this.Deflection;
        }

        private void UpdateFlame(float renderScale)
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

            FlamePoly.Update(_flamePos.Position, flameAngle, renderScale);
        }

        public override void Render(RenderContext ctx)
        {
            base.Render(ctx);

            _vaporTrails.ForEach(v => v.Render(ctx));
            _contrail.Render(ctx, p => -p.Y > 20000 && -p.Y < 70000 && ThrustAmount > 0f);
            //_bulletHoles.ForEach(f => f.Render(ctx));

            if (_thrustAmt.Value > 0f && GetThrust(true).Length() > 0f)
                ctx.DrawPolygon(this.FlamePoly.Poly, _flameFillColor, 1f, D2DDashStyle.Solid, _flameFillColor);

            //if (this.IsDisabled)
            //    _engineFireFlame.Render(ctx);

            ctx.DrawPolygon(this.Polygon.Poly, D2DColor.Black.WithAlpha(0.3f), 0.5f, D2DDashStyle.Solid, _planeColor);
            DrawClippedObjects(ctx);
            Wings.ForEach(w => w.Render(ctx));
            _gun.Render(ctx);


            //foreach (var b in _bulletHoles)
            //    ctx.DrawArrow(b.Position, b.Position + Utilities.AngleToVectorDegrees(b.Rotation, 10), D2DColor.Blue, 1f, 3f);

            //foreach (var pnt in this.Polygon.Poly)
            //    ctx.FillEllipseSimple(pnt, 0.5f, D2DColor.Red);

            //DrawFOVCone(gfx);
            //_cockpitPosition.Render(ctx);
            //_centerOfThrust.Render(ctx);
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

                ctx.Gfx.FillEllipse(new D2DEllipse(hole.Position, hole.OuterHoleSize), hole.Color);
                ctx.Gfx.FillEllipse(new D2DEllipse(hole.Position, hole.HoleSize), D2DColor.Black);

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
            var dirVec = Utilities.AngleToVectorDegrees(dir, SENSITIVITY);
            var amtVelo = Utilities.RadsToDegrees(this.Velocity.Normalized().Cross(dirVec));
            var amtRot = Utilities.RadsToDegrees(Utilities.AngleToVectorDegrees(this.Rotation).Cross(dirVec));
            var amt = Utilities.Lerp(amtVelo, amtRot, Utilities.Factor(MIN_VELO, this.Velocity.Length()));

            var rot = this.Rotation - amt;
            rot = Utilities.ClampAngle(rot);

            return rot;
        }

        public void SetAutoPilotAngle(float angle)
        {
            PlayerGuideAngle = angle;
        }

        public void ToggleThrust()
        {
            ThrustOn = !ThrustOn;
        }

        public void AddWing(Wing wing, bool isControl = false)
        {
            if (isControl && _controlWing == null)
                _controlWing = wing;

            const float VAPOR_TRAIL_GS = 9f; // How many Gs before vapor trail is visible.
            const float VAPOR_TRAIL_VELO = 1000f; // Velocity before vapor trail is visible.
            const float MAX_GS = 15f; // Gs for max vapor trail intensity.

            _vaporTrails.Add(new Vapor(wing, this, D2DPoint.Zero, 8f, VAPOR_TRAIL_GS, VAPOR_TRAIL_VELO, MAX_GS));

            Wings.Add(wing);
        }

        /// <summary>
        /// Update FixturePoint objects to move them to the current position.
        /// </summary>
        public void SyncFixtures()
        {
            this.Polygon.Update();
            _flamePos.Update(0f, World.RenderScale * this.RenderOffset);
            _bulletHoles.ForEach(f => f.Update(0f, World.RenderScale * this.RenderOffset));
            _centerOfThrust.Update(0f, World.RenderScale * this.RenderOffset);
            _centerOfMass.Update(0f, World.RenderScale * this.RenderOffset);
            _cockpitPosition.Update(0f, World.RenderScale * this.RenderOffset);
            _gun.Update(0f, World.RenderScale * this.RenderOffset);
            this.Wings.ForEach(w => w.Update(0f, World.RenderScale * this.RenderOffset));
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

        public void HandleImpactResult(GameObjectPoly impactor, PlaneImpactResult result)
        {
            var attackPlane = impactor.Owner as FighterPlane;

            if (impactor is Bullet)
                attackPlane.BulletsHit++;
            else if (impactor is Missile)
                attackPlane.MissilesHit++;

            // Always change target to attacking plane?
            if (this.IsAI)
                _aiBehavior.ChangeTarget(attackPlane);

            // Scale the impact position back to the origin of the polygon.
            var ogPos = Utilities.ScaleToOrigin(this, result.ImpactPoint);
            var angle = result.ImpactAngle;

            var distortAmt = BULLET_DISTORT_AMT;
            if (impactor is Missile)
                distortAmt = MISSILE_DISTORT_AMT;

            AddBulletHole(ogPos, angle, distortAmt);

            if (result.DoesDamage)
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
                    if (result.Type == ImpactType.Missile)
                    {
                        this.Health -= MISSILE_DAMAGE;
                        SpawnDebris(4, result.ImpactPoint, this.PlaneColor);
                    }
                    else
                    {
                        this.Health -= BULLET_DAMAGE;
                        SpawnDebris(1, result.ImpactPoint, this.PlaneColor);
                    }
                }

                if (this.Health <= 0)
                {
                    DoPlayerKilled(impactor);
                }
            }

            DoImpactImpulse(impactor, result.ImpactPoint);
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
            var impactTq = GetTorque(impactPos, forceVec);

            this.RotationSpeed += (float)(impactTq / this.Inertia * World.DT);
            this.Velocity += forceVec / this.MASS * World.DT;
        }

        public PlaneImpactResult GetImpactResult(GameObject impactor, D2DPoint impactPos)
        {
            // Make sure cockpit position is up-to-date.
            _cockpitPosition.Update(0f, World.RenderScale * this.RenderOffset);

            var angle = Utilities.ClampAngle((impactor.Velocity - this.Velocity).Angle() - this.Rotation);
            var result = new PlaneImpactResult();
            result.ImpactPoint = impactPos;
            result.ImpactAngle = angle;

            if (!IsDisabled)
            {
                result.DoesDamage = true;

                if (this.Health > 0)
                {
                    var distortAmt = BULLET_DISTORT_AMT;
                    if (impactor is Missile)
                        distortAmt = MISSILE_DISTORT_AMT;

                    var distortVec = Utilities.AngleToVectorDegrees(angle + this.Rotation, distortAmt);
                    var cockpitEllipse = new D2DEllipse(_cockpitPosition.Position, _cockpitSize);
                    var hitCockpit = CollisionHelpers.EllipseContains(cockpitEllipse, _cockpitPosition.Rotation, impactPos + distortVec);
                    if (hitCockpit)
                    {
                        result.WasHeadshot = true;
                    }

                    if (impactor is Missile)
                    {
                        result.Type = ImpactType.Missile;
                    }
                    else
                    {
                        result.Type = ImpactType.Bullet;
                    }
                }
            }

            return result;
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
                World.ObjectManager.AddDebris(debris);
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
            SASOn = false;
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
            _expireTimeout.Stop();
            _flipTimer.Restart();
            _bulletHoles.ForEach(b => b.Dispose());
            _bulletHoles.Clear();
            _contrail.Clear();
            World.ObjectManager.CleanDebris(this.ID);
            _thrustAmt.Target = 1f;
            WasHeadshot = false;
            PlayerGuideAngle = 0f;
            _easePhysicsComplete = false;
            _easePhysicsTimer.Restart();
            AIRespawnReady = false;
            _engineFireFlame.StopSpawning();

            Wings.ForEach(w => w.Visible = true);

            var flipped = this.Polygon.IsFlipped;

            this.Polygon = new RenderPoly(this, _planePoly, this.RenderOffset, POLY_TESSELLATE_DIST);

            if (flipped)
                this.Polygon.FlipY();

            if (IsAI)
                _aiBehavior.ClearTarget();
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

        private float GetTorque(Wing wing, D2DPoint force)
        {
            return GetTorque(wing.Position, force);
        }

        private float GetTorque(D2DPoint pos, D2DPoint force)
        {
            // How is it so simple?
            var r = pos - _centerOfMass.Position;

            var torque = Utilities.Cross(r, force);
            return torque;
        }

        public void FlipPoly(Direction direction)
        {
            if (_queuedDir != direction)
            {
                _flipTimer.Reset();
                _flipTimer.TriggerCallback = () => FlipPoly();
                _flipTimer.Start();
                _queuedDir = direction;
            }
        }

        private void FlipPoly()
        {
            if (_currentDir == _queuedDir || this.HasCrashed || this.IsDisabled)
                return;

            this.Polygon.FlipY();
            this.Polygon.Update();
            Wings.ForEach(w => w.FlipY());
            Wings.ForEach(w => w.Update(World.SUB_DT, this.RenderOffset));
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
            var pointingRight = Utilities.IsPointingRight(this.Rotation);
            FlipPoly(pointingRight ? Direction.Right : Direction.Left);
        }

        private D2DPoint GetThrust(bool thrustVector = false)
        {
            var thrust = D2DPoint.Zero;

            const float thrustVectorAmt = 1f;
            const float thrustBoostAmt = 1000f;
            const float thrustBoostMaxSpd = 200f;
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
            _contrail.Clear();
            _bulletHoles.ForEach(b => b.Dispose());
            _bulletHoles.Clear();
            _vaporTrails.Clear();
        }
    }
}
