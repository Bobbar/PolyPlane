using PolyPlane.AI_Behavior;
using PolyPlane.Rendering;
using System.Numerics;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public class FighterPlane : GameObjectPoly
    {
        public bool AIRespawnReady = false;
        public Direction FlipDirection => _currentDir;
        public string PlayerName;
        public float PlayerGuideAngle = 0;

        public int NumMissiles = MAX_MISSILES;
        public int NumBullets = MAX_BULLETS;
        public int NumDecoys = MAX_DECOYS;
        public int Hits = MAX_HITS;

        public float Deflection = 0f;
        public int BulletsFired = 0;
        public int MissilesFired = 0;

        public int BulletsHit = 0;
        public int MissilesHit = 0;

        public int Kills = 0;
        public int Headshots = 0;

        public const int MAX_DECOYS = 15;
        public const int MAX_BULLETS = 30;
        public const int MAX_MISSILES = 6;
        public const int MAX_HITS = 32;
        public const int MISSILE_DAMAGE = 8;
        public const int BULLET_DAMAGE = 1;

        private readonly float MASS = 90f;

        public bool IsAI => _isAIPlane;
        public bool IsDefending = false;

        public D2DColor PlaneColor
        {
            get { return _planeColor; }
            set { _planeColor = value; }
        }

        public float Thrust { get; set; } = 10f;
        public bool FiringBurst { get; set; } = false;
        public bool DroppingDecoy { get; set; } = false;
        public float GForce => _gForce;
        public float GForceDirection => _gForceDir;
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
        public bool IsDamaged { get; set; } = false;

        public List<Wing> Wings = new List<Wing>();
        public FixturePoint _centerOfThrust;
        private Wing? _controlWing = null;
        private RateLimiter _thrustAmt = new RateLimiter(0.5f);
        private Direction _currentDir = Direction.Right;
        private Direction _queuedDir = Direction.Right;
        private bool _isAIPlane = false;

        private GameTimer _flipTimer = new GameTimer(2f);
        private GameTimer _expireTimeout = new GameTimer(30f);
        private GameTimer _isLockOntoTimeout = new GameTimer(3f);
        private GameTimer _bulletRegenTimer = new GameTimer(0.2f, true);
        private GameTimer _decoyRegenTimer = new GameTimer(0.4f, true);
        private GameTimer _missileRegenTimer = new GameTimer(60f, true);
        private GameTimer _easePhysicsTimer = new GameTimer(3f, true);
        private bool _easePhysicsComplete = false;

        private float _damageDeflection = 0f;
        private float _gForce = 0f;
        private float _gForceDir = 0f;
        private int _throttlePos = 0;

        private RenderPoly FlamePoly;
        private D2DLayer _polyClipLayer = null;
        private D2DColor _flameFillColor = new D2DColor(0.6f, D2DColor.Yellow);
        private FixturePoint _flamePos;
        private FixturePoint _gunPosition;
        private FixturePoint _cockpitPosition;
        private D2DSize _cockpitSize = new D2DSize(9f, 6f);
        private D2DColor _planeColor;
        private D2DColor _cockpitColor = new D2DColor(0.5f, D2DColor.LightBlue);
        private SmokeTrail _contrail;
        private List<Flame> _flames = new List<Flame>();
        private List<Debris> _debris = new List<Debris>();
        private const float VAPOR_TRAIL_GS = 15f; // How many Gs before showing vapor trail.
        private List<Vapor> _vaporTrails = new List<Vapor>();
        private Vapor _gunSmoke;

        public Radar Radar { get; set; }
        public bool HasRadarLock = false;

        public Action<Bullet> FireBulletCallback { get; set; }
        public Action<GuidedMissile> FireMissileCallback { get; set; }
        public Action<FighterPlane, GameObject> PlayerKilledCallback { get; set; }

        public IAIBehavior _aiBehavior;

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

        private static readonly D2DPoint[] _flamePoly = new D2DPoint[]
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
            _aiBehavior.SkipFrames = World.PHYSICS_SUB_STEPS;

            _isAIPlane = true;

            Thrust = 1000f;

            _thrustAmt.Target = 1f;

            _planeColor = D2DColor.Randomly();

            InitStuff();
        }

        private void InitStuff()
        {
            this.RenderOffset = 1.5f;

            float defRate = 50f;

            if (_isAIPlane)
                defRate = 20f;

            this.Polygon = new RenderPoly(_planePoly, this.RenderOffset);

            AddWing(new Wing(this, 10f * this.RenderOffset, 0.5f, 40f, 10000f, new D2DPoint(1.5f, 1f), defRate));
            AddWing(new Wing(this, 5f * this.RenderOffset, 0.2f, 50f, 5000f, new D2DPoint(-35f, 1f), defRate), true);

            _controlWing.Deflection = 2f;
            _centerOfThrust = new FixturePoint(this, new D2DPoint(-33f, 0));

            var skipFrames = IsNetObject ? 1 : World.PHYSICS_SUB_STEPS;

            this.FlamePoly = new RenderPoly(_flamePoly, new D2DPoint(12f, 0), 1.7f);
            _flamePos = new FixturePoint(this, new D2DPoint(-38f, 1f), skipFrames);
            _gunPosition = new FixturePoint(this, new D2DPoint(35f, 0), skipFrames);
            _cockpitPosition = new FixturePoint(this, new D2DPoint(19.5f, -5f));
            _gunSmoke = new Vapor(_gunPosition, D2DPoint.Zero, 8f, new D2DColor(0.7f, D2DColor.BurlyWood));

            _flamePos.IsNetObject = this.IsNetObject;
            _gunPosition.IsNetObject = this.IsNetObject;
            _cockpitPosition.IsNetObject = this.IsNetObject;

            _contrail = new SmokeTrail(this, o =>
            {
                var p = o as FighterPlane;
                return p.ExhaustPosition;
            });
            _contrail.SkipFrames = skipFrames;

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

        public override void Update(float dt, D2DSize viewport, float renderScale)
        {
            this.Hits = Math.Clamp(this.Hits, 0, MAX_HITS);

            base.Update(dt, viewport, renderScale * this.RenderOffset);
            this.Radar?.Update(dt, viewport, renderScale, skipFrames: true);

            if (GForce > VAPOR_TRAIL_GS)
                _vaporTrails.ForEach(v => v.Visible = true);
            else
                _vaporTrails.ForEach(v => v.Visible = false);

            if (this.FiringBurst && this.NumBullets > 0)
                _gunSmoke.Visible = true;
            else
                _gunSmoke.Visible = false;

            if (!World.IsNetGame || (World.IsNetGame && !World.IsServer))
            {
                _flames.ForEach(f => f.Update(dt, viewport, renderScale, skipFrames: World.IsNetGame));
                _debris.ForEach(d => d.Update(dt, viewport, renderScale, skipFrames: true));
                _contrail.Update(dt, viewport, renderScale, skipFrames: true);
                _vaporTrails.ForEach(v => v.Update(dt, viewport, renderScale * this.RenderOffset));
                _gunSmoke.Update(dt, viewport, renderScale * this.RenderOffset);
            }

            _flamePos.Update(dt, viewport, renderScale * this.RenderOffset, skipFrames: true);
            _gunPosition.Update(dt, viewport, renderScale * this.RenderOffset, skipFrames: true);
            _cockpitPosition.Update(dt, viewport, renderScale * this.RenderOffset);

            if (_aiBehavior != null)
                _aiBehavior.Update(dt, viewport, renderScale, skipFrames: true);

            var wingForce = D2DPoint.Zero;
            var wingTorque = 0f;
            var thrust = GetThrust(true);
            var deflection = this.Deflection;

            if (AutoPilotOn)
            {
                float guideRot = this.Rotation;

                if (_isAIPlane)
                    guideRot = GetAPGuidanceDirection(_aiBehavior.GetAIGuidance());
                else
                    guideRot = GetAPGuidanceDirection(PlayerGuideAngle);

                var veloAngle = this.Velocity.Angle(true);
                var nextDeflect = Helpers.ClampAngle180(guideRot - veloAngle);
                deflection = nextDeflect;
            }

            float ogDef = deflection;

            // Apply some stability control to try to prevent thrust vectoring from spinning the plane.
            const float MIN_DEF_SPD = 300f; // Minimum speed required for full deflection.
            var velo = this.Velocity.Length();
            if (_thrustAmt.Value > 0f && SASOn)
            {
                var spdFact = Helpers.Factor(velo, MIN_DEF_SPD);

                const float MAX_DEF_AOA = 20f;// Maximum AoA allowed. Reduce deflection as AoA increases.
                var aoaFact = 1f - (Math.Abs(Wings[0].AoA) / (MAX_DEF_AOA + (spdFact * (MAX_DEF_AOA * 6f))));

                const float MAX_DEF_ROT_SPD = 55f; // Maximum rotation speed allowed. Reduce deflection to try to control rotation speed.
                var rotSpdFact = 1f - (Math.Abs(this.RotationSpeed) / (MAX_DEF_ROT_SPD + (spdFact * (MAX_DEF_ROT_SPD * 8f))));

                // Ease out when thrust is decreasing.
                deflection = Helpers.Lerp(ogDef, ogDef * aoaFact * rotSpdFact, _thrustAmt.Value);
            }

            if (float.IsNaN(deflection))
                deflection = 0f;

            _controlWing.Deflection = deflection;

            foreach (var wing in Wings)
            {
                var force = wing.GetLiftDragForce();
                var torque = GetTorque(wing, force);

                torque += GetTorque(_centerOfThrust.Position, thrust);

                wingForce += force;
                wingTorque += torque;
            }

            if (IsDamaged)
            {
                wingForce *= 0.3f;
                wingTorque *= 0.3f;
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
                    easeFact = Helpers.Factor(_easePhysicsTimer.Value, _easePhysicsTimer.Interval);

                // Integrate torque, thrust and wing force.
                this.RotationSpeed += (wingTorque * easeFact) / this.MASS * dt;
                this.Velocity += (thrust * easeFact) / this.MASS * dt;
                this.Velocity += (wingForce * easeFact) / this.MASS * dt;

                var gravFact = 1f;

                if (IsDamaged)
                    gravFact = 4f;

                this.Velocity += (World.Gravity * gravFact * dt);
            }


            var totForce = (thrust / this.MASS * dt) + (wingForce / this.MASS * dt);

            var gforce = totForce.Length() / dt / World.Gravity.Y;
            _gForce = gforce;
            _gForceDir = totForce.Angle(true);

            // TODO:  This is so messy...
            Wings.ForEach(w => w.Update(dt, viewport, renderScale * this.RenderOffset));
            _centerOfThrust.Update(dt, viewport, renderScale * this.RenderOffset);
            _thrustAmt.Update(dt);

            CheckForFlip();

            var thrustMag = thrust.Length();
            var flameAngle = thrust.Angle();
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

            if (!this.FiringBurst)
                _bulletRegenTimer.Update(dt);

            _missileRegenTimer.Update(dt);

            if (!this.DroppingDecoy)
                _decoyRegenTimer.Update(dt);

            //if (this.HasCrashed)
            //    _debris.Clear();

            if (this.IsExpired)
                _debris.Clear();
        }

        public override void NetUpdate(float dt, D2DSize viewport, float renderScale, D2DPoint position, D2DPoint velocity, float rotation, double frameTime)
        {
            base.NetUpdate(dt, viewport, renderScale, position, velocity, rotation, frameTime);

            _controlWing.Deflection = this.Deflection;
        }

        public override void Render(RenderContext ctx)
        {
            base.Render(ctx);

            _vaporTrails.ForEach(v => v.Render(ctx));

            _contrail.Render(ctx, p => -p.Y > 20000 && -p.Y < 70000 && ThrustAmount > 0f);

            ctx.Gfx.AntiAliasingOff();
            _flames.ForEach(f => f.Render(ctx));
            ctx.Gfx.AntiAliasingOn();

            if (_thrustAmt.Value > 0f && GetThrust(true).Length() > 0f)
                ctx.DrawPolygon(this.FlamePoly.Poly, _flameFillColor, 1f, D2DDashStyle.Solid, _flameFillColor);

            ctx.DrawPolygon(this.Polygon.Poly, D2DColor.Black, 1f, D2DDashStyle.Solid, _planeColor);
            Wings.ForEach(w => w.Render(ctx));
            DrawCockpit(ctx.Gfx);

            ctx.Gfx.AntiAliasingOff();
            _debris.ForEach(d => d.Render(ctx));
            ctx.Gfx.AntiAliasingOn();

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

                foreach (var flame in _flames)
                {
                    ctx.Gfx.FillEllipseSimple(flame.Position, 5f, D2DColor.Gray);
                    ctx.Gfx.FillEllipseSimple(flame.Position, 3f, D2DColor.Black);
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

            var centerLine = Helpers.AngleToVectorDegrees(this.Rotation, LEN);
            var cone1 = Helpers.AngleToVectorDegrees(this.Rotation + (FOV * 0.5f), LEN);
            var cone2 = Helpers.AngleToVectorDegrees(this.Rotation - (FOV * 0.5f), LEN);


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
            if (this.NumMissiles <= 0 || this.IsDamaged)
            {
                Log.Msg("Click...");
                return;
            }

            if (this.IsAI)
            {
                var missile = new GuidedMissile(this, target, GuidanceType.Advanced, useControlSurfaces: true, useThrustVectoring: true);
                FireMissileCallback(missile);
            }
            else
            {
                var missile = new GuidedMissile(this, target, GuidanceType.Advanced, useControlSurfaces: true, useThrustVectoring: true);
                FireMissileCallback(missile);
            }

            this.MissilesFired++;
            this.NumMissiles--;
        }

        public void FireBullet(Action<D2DPoint> addExplosion)
        {
            if (IsDamaged)
                return;

            if (this.NumBullets <= 0)
                return;

            if (this.IsNetObject)
                return;

            var bullet = new Bullet(this);

            bullet.AddExplosionCallback = addExplosion;
            FireBulletCallback(bullet);
            this.BulletsFired++;
            this.NumBullets--;
        }

        private float GetAPGuidanceDirection(float dir)
        {
            var amt = Helpers.RadsToDegrees(this.Velocity.Normalized().Cross(Helpers.AngleToVectorDegrees(dir, 2f)));
            var rot = this.Rotation - amt;
            rot = Helpers.ClampAngle(rot);

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

            _vaporTrails.Add(new Vapor(wing, new D2DPoint(5f, 0f), 2f));

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
            _flamePos.Update(0f, World.ViewPortSize, World.RenderScale * this.RenderOffset);
            _flames.ForEach(f => f.Update(0f, World.ViewPortSize, World.RenderScale * this.RenderOffset));
            _centerOfThrust.Update(0f, World.ViewPortSize, World.RenderScale * this.RenderOffset);
            _cockpitPosition.Update(0f, World.ViewPortSize, World.RenderScale * this.RenderOffset);
            _gunPosition.Update(0f, World.ViewPortSize, World.RenderScale * this.RenderOffset);
        }

        public void SetOnFire()
        {
            var offset = Helpers.RandOPointInPoly(_planePoly);
            SetOnFire(offset);
        }

        public void SetOnFire(D2DPoint pos)
        {
            var flame = new Flame(this, pos, hasFlame: Helpers.Rnd.Next(3) == 2);

            flame.IsNetObject = this.IsNetObject;
            flame.SkipFrames = this.IsNetObject ? 1 : World.PHYSICS_SUB_STEPS;
            _flames.Add(flame);
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

            if (result.DoesDamage)
            {
                if (result.WasHeadshot)
                {
                    SpawnDebris(8, result.ImpactPoint, D2DColor.Red);
                    WasHeadshot = true;
                    IsDamaged = true;
                    Hits = 0;
                    attackPlane.Headshots++;
                    attackPlane.Kills++;
                }
                else
                {
                    if (result.Type == ImpactType.Missile)
                    {
                        this.Hits -= MISSILE_DAMAGE;
                        SpawnDebris(4, result.ImpactPoint, this.PlaneColor);
                    }
                    else
                    {
                        this.Hits -= BULLET_DAMAGE;

                        if (Helpers.Rnd.Next(3) == 2)
                            SpawnDebris(1, result.ImpactPoint, this.PlaneColor);
                    }
                }

                // Scale the impact position back to the origin of the polygon.
                var mat = Matrix3x2.CreateRotation(-this.Rotation * (float)(Math.PI / 180f), this.Position);
                mat *= Matrix3x2.CreateTranslation(new D2DPoint(-this.Position.X, -this.Position.Y));
                var ogPos1 = D2DPoint.Transform(result.ImpactPoint, mat);

                SetOnFire(ogPos1);

                if (this.Hits <= 0)
                {
                    IsDamaged = true;
                    _damageDeflection = _rnd.NextFloat(-180, 180);

                    attackPlane.Kills++;

                    //if (attackPlane.Hits < MAX_HITS)
                    //    attackPlane.Hits += 2;

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

            //if (impactor is GuidedMissile missile)
            //    impactMass = missile.TotalMass * 4f;

            var velo = impactor.Velocity - this.Velocity;
            var force = (IMPACT_MASS * velo.Length()) / 4f;
            var forceVec = (impactPos.Normalized() * force);
            var impactTq = GetTorque(impactPos, forceVec);

            this.RotationSpeed += impactTq / this.MASS * World.DT;
            this.Velocity += forceVec / this.MASS * World.DT;
        }

        public PlaneImpactResult GetImpactResult(GameObject impactor, D2DPoint impactPos)
        {
            var result = new PlaneImpactResult();
            result.ImpactPoint = impactPos;

            if (!IsDamaged)
            {
                result.DoesDamage = true;

                if (this.Hits > 0)
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

        private void SpawnDebris(int num, D2DPoint pos, D2DColor color)
        {
            for (int i = 0; i < num; i++)
            {
                var debris = new Debris(pos, this.Velocity, color);
                //debris.SkipFrames = World.PHYSICS_STEPS;

                _debris.Add(debris);
            }
        }

        public void DoHitGround()
        {
            if (_isAIPlane)
                _expireTimeout.Start();

            HasCrashed = true;
            _flipTimer.Stop();
        }

        public void FixPlane()
        {
            Hits = MAX_HITS;
            NumBullets = MAX_BULLETS;
            NumMissiles = MAX_MISSILES;
            NumDecoys = MAX_DECOYS;
            IsDamaged = false;
            HasCrashed = false;
            ThrustOn = true;
            _expireTimeout.Stop();
            _expireTimeout.Reset();
            _flipTimer.Restart();
            _flames.Clear();
            _debris.Clear();
            _thrustAmt.Target = 1f;
            WasHeadshot = false;
            PlayerGuideAngle = 0f;
            _easePhysicsComplete = false;
            _easePhysicsTimer.Stop();
            _easePhysicsTimer.Reset();
            AIRespawnReady = false;
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

            var torque = Helpers.Cross(r, force);
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
            if (_currentDir == _queuedDir)
                return;

            this.Polygon.FlipY();
            Wings.ForEach(w => w.FlipY());
            _flamePos.FlipY();
            _flames.ForEach(f => f.FlipY());
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
            var pointingRight = Helpers.IsPointingRight(this.Rotation);
            FlipPoly(pointingRight ? Direction.Right : Direction.Left);
        }

        private D2DPoint GetThrust(bool thrustVector = false)
        {
            var thrust = D2DPoint.Zero;

            if (!ThrustOn)
                return thrust;

            //if (ThrustOn)
            //    _thrustAmt.Target = 1f;
            //else
            //    _thrustAmt.Target = 0f;

            const float thrustVectorAmt = 1f;
            const float thrustBoostAmt = 1000f;
            const float thrustBoostMaxSpd = 200f;//600f;

            D2DPoint vec;

            if (thrustVector)
                vec = AngleToVector(this.Rotation + (_controlWing.Deflection * thrustVectorAmt));
            else
                vec = AngleToVector(this.Rotation);

            // Add a boost effect as speed increases. Jet engines make more power at higher speeds right?
            var boostFact = Helpers.Factor(this.Velocity.Length(), thrustBoostMaxSpd);
            vec *= _thrustAmt.Value * ((this.Thrust + (thrustBoostAmt * boostFact)) * World.GetDensityAltitude(this.Position));

            thrust = vec;

            return thrust;
        }

        public override void Dispose()
        {
            base.Dispose();

            _polyClipLayer?.Dispose();
            _contrail.Clear();
            _flames.Clear();
            _debris.Clear();
            _vaporTrails.Clear();
        }
    }
}
