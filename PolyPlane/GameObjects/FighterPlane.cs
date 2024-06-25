using PolyPlane.AI_Behavior;
using PolyPlane.GameObjects.Manager;
using PolyPlane.Helpers;
using PolyPlane.Rendering;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public class FighterPlane : GameObjectPoly, ICollidable
    {
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
        public int Health { get { return _health; } set { _health = Math.Clamp(value, 0, MAX_HEALTH); } }

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
        public const int MAX_HEALTH = 32;
        public const int MISSILE_DAMAGE = 32;
        public const int BULLET_DAMAGE = 4;

        public bool IsAI => _isAIPlane;

        public D2DColor PlaneColor
        {
            get { return _planeColor; }
            set { _planeColor = value; }
        }

        public float Thrust { get; set; } = 10f;
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
        public bool SASOn { get; set; } = true;
        public bool HasCrashed { get; set; } = false;
        public bool WasHeadshot { get; set; } = false;
        public D2DPoint GunPosition => _gunPosition.Position;
        public D2DPoint ExhaustPosition => _centerOfThrust.Position;
        public bool IsDisabled { get; set; } = false;
        public Radar Radar { get; set; }
        public bool HasRadarLock = false;

        public Action<Bullet> FireBulletCallback { get; set; }
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

        private GameTimer _flipTimer = new GameTimer(2f);
        private GameTimer _expireTimeout = new GameTimer(30f);
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
        private int _health = MAX_HEALTH;


        private RenderPoly FlamePoly;
        private D2DLayer _polyClipLayer = null;
        private D2DColor _flameFillColor = new D2DColor(0.6f, D2DColor.Yellow);
        private FixturePoint _flamePos;
        private FixturePoint _gunPosition;
        private FixturePoint _cockpitPosition;
        private FixturePoint _centerOfThrust;
        private D2DSize _cockpitSize = new D2DSize(9f, 6f);
        private D2DColor _planeColor;
        private D2DColor _cockpitColor = new D2DColor(0.5f, D2DColor.LightBlue);
        private SmokeTrail _contrail;
        private List<BulletHole> _bulletHoles = new List<BulletHole>();
        private List<Vapor> _vaporTrails = new List<Vapor>();
        private GunSmoke _gunSmoke;

        private IAIBehavior _aiBehavior;

        private readonly D2DPoint[] _planePoly = new D2DPoint[]
        {
            new D2DPoint(28,0),
            new D2DPoint(25,-2),
            new D2DPoint(20,-3),
            new D2DPoint(16,-5),
            new D2DPoint(13,-6),
            new D2DPoint(10,-5),
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
        };

        private readonly D2DPoint[] _flamePoly = new D2DPoint[]
        {
            new D2DPoint(-8, 1),
            new D2DPoint(-10, 0),
            new D2DPoint(-8, -1),
        };

        public FighterPlane(D2DPoint pos) : base(pos)
        {
            Thrust = 2000f;
            _thrustAmt.Target = 1f;
            _planeColor = D2DColor.Randomly();

            InitStuff();
        }

        public FighterPlane(D2DPoint pos, D2DColor color) : base(pos)
        {
            IsNetObject = true;
            _planeColor = color;
            _isAIPlane = false;
            AutoPilotOn = false;
            ThrustOn = true;
            _thrustAmt.Target = 1f;

            InitStuff();
        }

        public FighterPlane(D2DPoint pos, AIPersonality personality) : base(pos)
        {
            _aiBehavior = new FighterPlaneAI(this, personality);

            _isAIPlane = true;

            Thrust = 1000f;

            _thrustAmt.Target = 1f;

            _planeColor = D2DColor.Randomly();

            InitStuff();
        }

        private void InitStuff()
        {
            this.Radar = new Radar(this);

            this.RenderOffset = 1.5f;

            this.Polygon = new RenderPoly(_planePoly, this.RenderOffset);

            InitWings();

            _controlWing.Deflection = 2f;
            _centerOfThrust = new FixturePoint(this, new D2DPoint(-33f, 0));

            this.FlamePoly = new RenderPoly(_flamePoly, new D2DPoint(12f, 0), this.RenderOffset);
            _flamePos = new FixturePoint(this, new D2DPoint(-38f, 1f));
            _gunPosition = new FixturePoint(this, new D2DPoint(35f, 0));
            _cockpitPosition = new FixturePoint(this, new D2DPoint(19.5f, -5f));
            _gunSmoke = new GunSmoke(_gunPosition, D2DPoint.Zero, 8f, new D2DColor(0.7f, D2DColor.BurlyWood));

            _flamePos.IsNetObject = this.IsNetObject;
            _gunPosition.IsNetObject = this.IsNetObject;
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

            _bulletRegenTimer.Start();

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

            _decoyRegenTimer.Start();

            _isLockOntoTimeout.TriggerCallback = () => HasRadarLock = false;

            _easePhysicsTimer.Start();
            _easePhysicsTimer.TriggerCallback = () => _easePhysicsComplete = true;
        }

        private void InitWings()
        {
            float defRate = 50f;

            if (_isAIPlane)
                defRate = 30f;

            // Main wing.
            AddWing(new Wing(this, new WingParameters()
            {
                RenderLength = 10f * this.RenderOffset,
                RenderWidth = 3f,
                Area = 0.5f,
                MaxDeflection = 40f,
                MaxLiftForce = 12000f,
                MaxDragForce = 20000f,
                DeflectionRate = defRate,
                Position = new D2DPoint(1.5f, 1f),
                MinVelo = 250f
            }));

            // Tail wing. (Control wing)
            AddWing(new Wing(this, new WingParameters()
            {
                RenderLength = 5f * this.RenderOffset,
                RenderWidth = 3f,
                Area = 0.2f,
                MaxDeflection = 50f,
                MaxLiftForce = 5500f,
                MaxDragForce = 7500f,
                DeflectionRate = defRate,
                Position = new D2DPoint(-35f, 1f),
                MinVelo = 250f
            }), isControl: true);

        }
        public override void Update(float dt, float renderScale)
        {
            for (int i = 0; i < World.PHYSICS_SUB_STEPS; i++)
            {
                var partialDT = World.SUB_DT;

                base.Update(partialDT, renderScale * this.RenderOffset);

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

                    var veloAngle = this.Velocity.Angle(true);
                    var nextDeflect = Utilities.ClampAngle180(guideRot - veloAngle);
                    deflection = nextDeflect;
                }

                float ogDef = deflection;

                // Apply some stability control to try to prevent thrust vectoring from spinning the plane.
                const float MIN_DEF_SPD = 300f; // Minimum speed required for full deflection.
                var velo = this.AirSpeedIndicated;
                if (_thrustAmt.Value > 0f && SASOn)
                {
                    var spdFact = Utilities.Factor(velo, MIN_DEF_SPD);

                    const float MAX_DEF_AOA = 20f;// Maximum AoA allowed. Reduce deflection as AoA increases.
                    var aoaFact = 1f - (Math.Abs(Wings[0].AoA) / (MAX_DEF_AOA + (spdFact * (MAX_DEF_AOA * 6f))));

                    const float MAX_DEF_ROT_SPD = 55f; // Maximum rotation speed allowed. Reduce deflection to try to control rotation speed.
                    var rotSpdFact = 1f - (Math.Abs(this.RotationSpeed) / (MAX_DEF_ROT_SPD + (spdFact * (MAX_DEF_ROT_SPD * 8f))));

                    // Ease out when thrust is decreasing.
                    deflection = Utilities.Lerp(ogDef, ogDef * aoaFact * rotSpdFact, _thrustAmt.Value);
                }

                if (float.IsNaN(deflection))
                    deflection = 0f;

                _controlWing.Deflection = deflection;

                foreach (var wing in Wings)
                {
                    var force = wing.GetLiftDragForce();
                    var torque = GetTorque(wing, force);

                    wingForce += force;
                    wingTorque += torque;
                }

                if (IsDisabled)
                {
                    wingForce *= 0.2f;
                    wingTorque *= 0.2f;
                    AutoPilotOn = false;
                    SASOn = false;
                    ThrustOn = false;
                    _thrustAmt.Set(0f);
                    _controlWing.Deflection = _damageDeflection;
                    FiringBurst = false;
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
                    this.RotationSpeed += ((wingTorque + thrustTorque) * easeFact) / this.MASS * partialDT;
                    this.Velocity += (thrust * easeFact) / this.MASS * partialDT;
                    this.Velocity += (wingForce * easeFact) / this.MASS * partialDT;

                    var gravFact = 1f;

                    if (IsDisabled)
                        gravFact = 4f;

                    this.Velocity += (World.Gravity * gravFact * partialDT);
                }

                var totForce = (thrust / this.MASS * partialDT) + (wingForce / this.MASS * partialDT);
                var gforce = totForce.Length() / partialDT / World.Gravity.Y;
                _gforceAvg.Add(gforce);

                // TODO:  This is so messy...
                Wings.ForEach(w => w.Update(partialDT, renderScale * this.RenderOffset));
                _centerOfThrust.Update(partialDT, renderScale * this.RenderOffset);
                _thrustAmt.Update(partialDT);
            }

            _gForce = _gforceAvg.Current;

            _flamePos.Update(dt, renderScale * this.RenderOffset);
            _gunPosition.Update(dt, renderScale * this.RenderOffset);
            _cockpitPosition.Update(dt, renderScale * this.RenderOffset);

            if (!World.IsNetGame || World.IsClient)
            {
                _bulletHoles.ForEach(f => f.Update(dt, renderScale * this.RenderOffset));
                _contrail.Update(dt, renderScale);
                _vaporTrails.ForEach(v => v.Update(dt, renderScale * this.RenderOffset));
                _gunSmoke.Update(dt, renderScale * this.RenderOffset);
            }

            CheckForFlip();

            var thrust2 = GetThrust(true);
            var thrustMag = thrust2.Length();
            var flameAngle = thrust2.Angle();
            var len = this.Velocity.Length() * 0.05f;
            len += thrustMag * 0.01f;
            len *= 0.6f;
            FlamePoly.SourcePoly[1].X = -_rnd.NextFloat(9f + len, 11f + len);
            _flameFillColor.g = _rnd.NextFloat(0.6f, 0.86f);

            FlamePoly.Update(_flamePos.Position, flameAngle, renderScale * this.RenderOffset);

            _easePhysicsTimer.Update(dt);
            _flipTimer.Update(dt);
            _isLockOntoTimeout.Update(dt);
            _expireTimeout.Update(dt);

            this.Radar?.Update(dt, renderScale);


            if (this.FiringBurst && this.NumBullets > 0 && !this.IsDisabled)
                _gunSmoke.Visible = true;
            else
                _gunSmoke.Visible = false;

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

        public override void Render(RenderContext ctx)
        {
            base.Render(ctx);

            _vaporTrails.ForEach(v => v.Render(ctx));
            _contrail.Render(ctx, p => -p.Y > 20000 && -p.Y < 70000 && ThrustAmount > 0f);
            _bulletHoles.ForEach(f => f.Render(ctx));

            if (_thrustAmt.Value > 0f && GetThrust(true).Length() > 0f)
                ctx.DrawPolygon(this.FlamePoly.Poly, _flameFillColor, 1f, D2DDashStyle.Solid, _flameFillColor);

            ctx.DrawPolygon(this.Polygon.Poly, D2DColor.Black, 0.5f, D2DDashStyle.Solid, _planeColor);
            Wings.ForEach(w => w.Render(ctx));
            DrawCockpit(ctx.Gfx);
            DrawBulletHoles(ctx);
            _gunSmoke.Render(ctx);

            //DrawFOVCone(gfx);
            //_cockpitPosition.Render(ctx);
            //_centerOfThrust.Render(ctx);
            //_gunPosition.Render(ctx);
        }

        private void DrawBulletHoles(RenderContext ctx)
        {
            if (_polyClipLayer == null)
                _polyClipLayer = ctx.Device.CreateLayer();

            // Clip the polygon to give the holes some depth/realism.
            using (var polyClipGeo = ctx.Device.CreatePathGeometry())
            {
                polyClipGeo.AddLines(this.Polygon.Poly);
                polyClipGeo.ClosePath();

                ctx.Gfx.PushLayer(_polyClipLayer, ctx.Viewport, polyClipGeo);

                foreach (var flame in _bulletHoles)
                {
                    var outsideSz = new D2DSize(flame.HoleSize.width + 2f, flame.HoleSize.height + 2f);

                    ctx.Gfx.PushTransform();
                    ctx.Gfx.RotateTransform(flame.Rotation, flame.Position);

                    ctx.Gfx.FillEllipse(new D2DEllipse(flame.Position, outsideSz), D2DColor.Gray);
                    ctx.Gfx.FillEllipse(new D2DEllipse(flame.Position, flame.HoleSize), D2DColor.Black);

                    ctx.Gfx.PopTransform();
                }

                ctx.Gfx.PopLayer();
            }
        }

        private void DrawCockpit(D2DGraphics gfx)
        {
            gfx.PushTransform();
            gfx.RotateTransform(_cockpitPosition.Rotation, _cockpitPosition.Position);
            gfx.FillEllipse(new D2DEllipse(_cockpitPosition.Position, _cockpitSize), WasHeadshot ? D2DColor.DarkRed : _cockpitColor);
            gfx.DrawEllipse(new D2DEllipse(_cockpitPosition.Position, _cockpitSize), D2DColor.Black);
            gfx.PopTransform();
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

        public void FireBullet()
        {
            if (IsDisabled)
                return;

            if (this.NumBullets <= 0)
                return;

            if (this.IsNetObject)
                return;

            var bullet = new Bullet(this);

            FireBulletCallback(bullet);
            this.BulletsFired++;
            this.NumBullets--;
        }

        private float GetAPGuidanceDirection(float dir)
        {
            var amt = Utilities.RadsToDegrees(this.Velocity.Normalized().Cross(Utilities.AngleToVectorDegrees(dir, 2f)));
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

            const float VAPOR_TRAIL_GS = 15f; // How many Gs before vapor trail is visible.
            const float MAX_GS = 30f; // Gs for max vapor trail intensity.

            _vaporTrails.Add(new Vapor(wing, this, new D2DPoint(10f, 0f), 8f, VAPOR_TRAIL_GS, MAX_GS));

            Wings.Add(wing);
        }

        public void Reset()
        {
            Wings.ForEach(w => w.Reset(this.Position));
        }

        /// <summary>
        /// Update FixturePoint objects to move them to the current position.
        /// </summary>
        public void SyncFixtures()
        {
            this.Polygon.Update(this.Position, this.Rotation, World.RenderScale * this.RenderOffset);
            _flamePos.Update(0f, World.RenderScale * this.RenderOffset);
            _bulletHoles.ForEach(f => f.Update(0f, World.RenderScale * this.RenderOffset));
            _centerOfThrust.Update(0f, World.RenderScale * this.RenderOffset);
            _cockpitPosition.Update(0f, World.RenderScale * this.RenderOffset);
            _gunPosition.Update(0f, World.RenderScale * this.RenderOffset);
        }

        public void AddBulletHole()
        {
            var offset = Utilities.RandOPointInPoly(_planePoly);
            AddBulletHole(offset);
        }

        public void AddBulletHole(D2DPoint pos)
        {
            var bulletHole = new BulletHole(this, pos, hasFlame: Utilities.Rnd.Next(3) == 2);
            bulletHole.IsNetObject = this.IsNetObject;
            _bulletHoles.Add(bulletHole);
        }

        public void AddBulletHole(D2DPoint pos, float angle)
        {
            var bulletHole = new BulletHole(this, pos, angle, hasFlame: Utilities.Rnd.Next(3) == 2);
            bulletHole.IsNetObject = this.IsNetObject;
            _bulletHoles.Add(bulletHole);
        }

        public void DoImpact(GameObject impactor, D2DPoint impactPos)
        {
            var result = GetImpactResult(impactor, impactPos);
            HandleImpactResult(impactor, result);
        }

        public void HandleImpactResult(GameObject impactor, PlaneImpactResult result)
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

            AddBulletHole(ogPos, angle);

            if (result.DoesDamage)
            {
                if (result.WasHeadshot)
                {
                    SpawnDebris(8, result.ImpactPoint, D2DColor.Red);
                    WasHeadshot = true;
                    IsDisabled = true;
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

                        if (Utilities.Rnd.Next(3) == 2)
                            SpawnDebris(1, result.ImpactPoint, this.PlaneColor);
                    }
                }

                if (this.Health <= 0)
                {
                    IsDisabled = true;
                    _damageDeflection = _rnd.NextFloat(-180, 180);

                    attackPlane.Kills++;
                    Deaths++;

                    PlayerKilledCallback?.Invoke(this, impactor);
                }
            }

            DoImpactImpulse(impactor, result.ImpactPoint);
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

            this.RotationSpeed += impactTq / this.MASS * World.DT;
            this.Velocity += forceVec / this.MASS * World.DT;
        }

        public PlaneImpactResult GetImpactResult(GameObject impactor, D2DPoint impactPos)
        {
            var angle = Utilities.ClampAngle(impactPos.AngleTo(impactor.Position, false) - this.Rotation);
            var result = new PlaneImpactResult();
            result.ImpactPoint = impactPos;
            result.ImpactAngle = angle;

            if (!IsDisabled)
            {
                result.DoesDamage = true;

                if (this.Health > 0)
                {
                    var cockpitEllipse = new D2DEllipse(_cockpitPosition.Position, _cockpitSize);
                    var hitCockpit = CollisionHelpers.EllipseContains(cockpitEllipse, _cockpitPosition.Rotation, impactPos);
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
            World.ObjectManager.CleanDebris(this.ID);
            _thrustAmt.Target = 1f;
            WasHeadshot = false;
            PlayerGuideAngle = 0f;
            _easePhysicsComplete = false;
            _easePhysicsTimer.Restart();
            AIRespawnReady = false;

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
            var r = pos - this.Position;

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
            if (_currentDir == _queuedDir || this.HasCrashed)
                return;

            this.Polygon.FlipY();
            Wings.ForEach(w => w.FlipY());
            _flamePos.FlipY();
            _bulletHoles.ForEach(f => f.FlipY());
            _cockpitPosition.FlipY();
            _gunPosition.FlipY();
            _centerOfThrust.FlipY();

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
            _bulletHoles.Clear();
            _vaporTrails.Clear();
        }
    }
}
