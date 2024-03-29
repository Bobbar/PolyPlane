﻿using PolyPlane.AI_Behavior;
using System.Diagnostics;
using System.Numerics;

using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public class Plane : GameObjectPoly
    {

        public int BulletsFired = 0;
        public int MissilesFired = 0;

        public int BulletsHit = 0;
        public int MissilesHit = 0;

        public int Kills = 0;
        public int Headshots = 0;

        public const int MAX_MISSILES = 6;
        public const int MAX_HITS = 16;
        public int Hits = MAX_HITS;

        public Radar Radar { get; set; }
        public bool HasRadarLock = false;

        public int NumMissiles = MAX_MISSILES;

        public bool IsAI => _isAIPlane;
        public bool IsDefending = false;

        public D2DColor PlaneColor => _planeColor;
        public float Mass
        {
            get { return _mass; }

            set { _mass = value; }
        }

        private float _mass = 90f;
        private const int MAX_FLAMES = 10;
        private int nFlames = 0;

        public float Thrust { get; set; } = 10f;
        public bool FiringBurst { get; set; } = false;
        public bool DroppingDecoy { get; set; } = false;

        public float GForce => _gForce;
        public bool ThrustOn { get; set; } = false;
        public float ThrustAmount => _thrustAmt.Value;
        public bool AutoPilotOn { get; set; } = false;
        public bool SASOn { get; set; } = true;
        public bool HasCrashed { get; set; } = false;
        public bool WasHeadshot { get; set; } = false;

        public D2DPoint GunPosition => _gunPosition.Position;
        public D2DPoint ExhaustPosition => _centerOfThrust.Position;
        public bool IsDamaged { get; set; } = false;

        public IAIBehavior _aiBehavior;

        public bool IsEngaged
        {
            get
            {
                return IsAI && _engageTimer.IsRunning;
            }
        }

        public List<Wing> Wings = new List<Wing>();
        public FixturePoint _centerOfThrust;
        private Wing? _controlWing = null;
        private float _renderOffset = 1.5f;
        private RateLimiter _thrustAmt = new RateLimiter(0.5f);
        private RateLimiterAngle _apAngleLimiter = new RateLimiterAngle(50f);
        private float _targetDeflection = 0f;
        private float _maxDeflection = 50f;
        private GameTimer _flipTimer = new GameTimer(2f);
        private Direction _currentDir = Direction.Right;
        private Direction _queuedDir = Direction.Right;
        private D2DPoint _force = D2DPoint.Zero;
        private bool _isAIPlane = false;
        private bool _damageFlash = false;

        private GameTimer _engageTimer;
        private GameTimer _expireTimeout = new GameTimer(100f);
        private GameTimer _isLockOntoTimeout = new GameTimer(3f);
        private GameTimer _damageCooldownTimeout = new GameTimer(4f);
        private GameTimer _damageFlashTimer = new GameTimer(0.2f, true);

        private float _damageDeflection = 0f;

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
        private int _throttlePos = 0;

        private List<Flame> _flames = new List<Flame>();
        private List<Debris> _debris = new List<Debris>();

        public Action<Bullet> FireBulletCallback { get; set; }
        public Action<GuidedMissile> FireMissileCallback { get; set; }

        private float _gForce = 0f;

        private float Deflection
        {
            get { return _targetDeflection; }
            set
            {
                if (value >= -_maxDeflection && value <= _maxDeflection)
                    _targetDeflection = value;
                else
                    _targetDeflection = Math.Sign(value) * _maxDeflection;
            }
        }

        private readonly D2DPoint[] _poly = new D2DPoint[]
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

        public Plane(D2DPoint pos, Plane playerPlane = null) : base(pos)
        {
            if (playerPlane != null)
            {
                _aiBehavior = new FighterPlaneAI(this, null);
                _aiBehavior.SkipFrames = World.PHYSICS_STEPS;

                _isAIPlane = true;

                Thrust = 1000f;
            }
            else
            {
                Thrust = 2000f;
            }

            _thrustAmt.Target = 1f;

            _isLockOntoTimeout.TriggerCallback = () => HasRadarLock = false;

            _damageFlashTimer.TriggerCallback = () => _damageFlash = !_damageFlash;
            _damageCooldownTimeout.TriggerCallback = () =>
            {
                _damageFlashTimer.Stop();
                _damageFlash = false;
            };

            _planeColor = D2DColor.Randomly();

            this.Polygon = new RenderPoly(_poly, _renderOffset);

            InitStuff();

            _controlWing.Deflection = 2f;
            _centerOfThrust = new FixturePoint(this, new D2DPoint(-33f, 0));
        }

        private void InitStuff()
        {
            float defRate = 50f;

            if (_isAIPlane)
                defRate = 20f;

            AddWing(new Wing(this, 10f * _renderOffset, 0.5f, 40f, 10000f, new D2DPoint(1.5f, 1f), defRate));
            AddWing(new Wing(this, 5f * _renderOffset, 0.2f, 50f, 5000f, new D2DPoint(-35f, 1f), defRate), true);

            this.FlamePoly = new RenderPoly(_flamePoly, new D2DPoint(12f, 0), 1.7f);
            _flamePos = new FixturePoint(this, new D2DPoint(-37f, 0), World.PHYSICS_STEPS);
            _gunPosition = new FixturePoint(this, new D2DPoint(33f, 0), World.PHYSICS_STEPS);
            _cockpitPosition = new FixturePoint(this, new D2DPoint(19.5f, -5f));

            _contrail = new SmokeTrail(this, o =>
            {
                var p = o as Plane;
                return p.ExhaustPosition;
            });
            _contrail.SkipFrames = World.PHYSICS_STEPS;

            _expireTimeout.TriggerCallback = () => this.IsExpired = true;
        }

        public override void Update(float dt, D2DSize viewport, float renderScale)
        {
            base.Update(dt, viewport, renderScale * _renderOffset);

            this.Radar?.Update(dt, viewport, renderScale, skipFrames: true);

            _flames.ForEach(f => f.Update(dt, viewport, renderScale));
            _debris.ForEach(d => d.Update(dt, viewport, renderScale));

            if (_aiBehavior != null)
                _aiBehavior.Update(dt, viewport, renderScale, skipFrames: true);

            _contrail.Update(dt, viewport, renderScale, skipFrames: true);
            _flamePos.Update(dt, viewport, renderScale * _renderOffset, skipFrames: true);
            _gunPosition.Update(dt, viewport, renderScale * _renderOffset, skipFrames: true);
            _cockpitPosition.Update(dt, viewport, renderScale * _renderOffset);

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
                    guideRot = GetAPGuidanceDirection(_apAngleLimiter.Value);

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
                wingForce *= 0.2f;
                wingTorque *= 0.2f;
                AutoPilotOn = false;
                SASOn = false;
                ThrustOn = false;
                _thrustAmt.Set(0f);
                _controlWing.Deflection = _damageDeflection;
            }

            _force = wingForce;

            this.RotationSpeed += wingTorque / this.Mass * dt;

            this.Velocity += thrust / this.Mass * dt;
            this.Velocity += wingForce / this.Mass * dt;

            var gravFact = 1f;

            if (IsDamaged)
                gravFact = 4f;

            this.Velocity += (World.Gravity * gravFact * dt);

            var totForce = (thrust / this.Mass * dt) + (wingForce / this.Mass * dt);

            var gforce = totForce.Length() / dt / World.Gravity.Y;
            _gForce = gforce;

            // TODO:  This is so messy...
            Wings.ForEach(w => w.Update(dt, viewport, renderScale * _renderOffset));
            _centerOfThrust.Update(dt, viewport, renderScale * _renderOffset);
            _thrustAmt.Update(dt);
            _apAngleLimiter.Update(dt);
            CheckForFlip();

            var thrustMag = thrust.Length();
            var flameAngle = thrust.Angle();
            var len = this.Velocity.Length() * 0.05f;
            len += thrustMag * 0.01f;
            len *= 0.6f;
            FlamePoly.SourcePoly[1].X = -_rnd.NextFloat(9f + len, 11f + len);
            _flameFillColor.g = _rnd.NextFloat(0.6f, 0.86f);

            FlamePoly.Update(_flamePos.Position, flameAngle, renderScale * _renderOffset);

            _flipTimer.Update(dt);
            _isLockOntoTimeout.Update(dt);
            _damageCooldownTimeout.Update(dt);
            _damageFlashTimer.Update(dt);
            _expireTimeout.Update(dt);

            //if (this.HasCrashed)
            //    _debris.Clear();
        }

        public override void Render(RenderContext ctx)
        {
            base.Render(ctx);

            _contrail.Render(ctx, p => -p.Y > 20000 && -p.Y < 70000 && ThrustAmount > 0f);

            if (_thrustAmt.Value > 0f && GetThrust(true).Length() > 0f)
                ctx.DrawPolygon(this.FlamePoly.Poly, _flameFillColor, 1f, D2DDashStyle.Solid, _flameFillColor);

            var color = _planeColor;
            if (_damageFlash && !this.IsDamaged)
                color = new D2DColor(0.2f, _planeColor);

            ctx.DrawPolygon(this.Polygon.Poly, D2DColor.Black, 1f, D2DDashStyle.Solid, color);
            Wings.ForEach(w => w.Render(ctx));
            DrawCockpit(ctx.Gfx);
            

            ctx.Gfx.AntiAliasingOff();
            _debris.ForEach(d => d.Render(ctx));
            ctx.Gfx.AntiAliasingOn();

            DrawBulletHoles(ctx);

            ctx.Gfx.AntiAliasingOff();
            _flames.ForEach(f => f.Render(ctx));
            ctx.Gfx.AntiAliasingOn();

            //DrawFOVCone(gfx);
            //_cockpitPosition.Render(ctx);
            //_centerOfThrust.Render(ctx);
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

        public void EngagePlayer(float duration)
        {
            if (_engageTimer.IsRunning == false)
            {
                _engageTimer.Interval = duration;
                _engageTimer.Reset();
                _engageTimer.Start();

                Log.Msg("Engaging Player!");
            }
        }

        public void FireMissile(GameObject target)
        {
            if (this.NumMissiles <= 0)
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

            var bullet = new Bullet(this);

            bullet.AddExplosionCallback = addExplosion;
            FireBulletCallback(bullet);
            this.BulletsFired++;
        }

        public void Pitch(bool pitchUp)
        {
            float amt = 1f;//0.5f;

            if (pitchUp)
                this.Deflection += amt;
            else
                this.Deflection -= amt;
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
            //_apAngleLimiter.Target = angle;
            _apAngleLimiter.Set(angle);
        }

        public void ToggleThrust()
        {
            ThrustOn = !ThrustOn;
        }

        public void AddWing(Wing wing, bool isControl = false)
        {
            if (isControl && _controlWing == null)
                _controlWing = wing;

            Wings.Add(wing);
        }

        public void Reset()
        {
            Wings.ForEach(w => w.Reset(this.Position));
        }

        public void SetOnFire()
        {
            var offset = Helpers.RandOPointInPoly(_poly);
            SetOnFire(offset);
        }

        public void SetOnFire(D2DPoint pos)
        {
            var flame = new Flame(this, pos);
            flame.SkipFrames = World.PHYSICS_STEPS;
            _flames.Add(flame);
        }

        public void DoImpact(GameObject impactor)
        {
            DoImpact(impactor, impactor.Position);
        }

        public void DoImpact(GameObject impactor, D2DPoint impactPos)
        {
            var attackPlane = impactor.Owner as Plane;

            if (impactor is Bullet)
                attackPlane.BulletsHit++;
            else if (impactor is Missile)
                attackPlane.MissilesHit++;


            if (!IsDamaged && !_damageCooldownTimeout.IsRunning)
            {
                if (this.Hits > 0)
                {
                    var cockpitEllipse = new D2DEllipse(_cockpitPosition.Position, _cockpitSize);
                    var hitCockpit = CollisionHelpers.EllipseContains(cockpitEllipse, _cockpitPosition.Rotation, impactPos);
                    if (hitCockpit)
                    {
                        Debug.WriteLine("HEADSHOT!");
                        SpawnDebris(8, impactPos, D2DColor.Red);
                        WasHeadshot = true;
                        IsDamaged = true;

                        attackPlane.Headshots++;
                        attackPlane.Kills++;

                    }

                    //var cockpitDist = _cockpitPosition.Position.DistanceTo(impactPos);
                    //if (cockpitDist <= _cockpitRadius)
                    //{
                    //    Debug.WriteLine("HEADSHOT!");
                    //    SpawnDebris(8, impactPos, D2DColor.Red);
                    //    WasHeadshot = true;
                    //    IsDamaged = true;
                    //}


                    if (impactor is Missile)
                    {
                        this.Hits -= 4;
                        SpawnDebris(4, impactPos, this.PlaneColor);
                    }
                    else
                    {
                        this.Hits -= 2;
                        SpawnDebris(2, impactPos, this.PlaneColor);
                    }

                    if (nFlames < MAX_FLAMES)
                    {
                        // Scale the impact position back to the origin of the polygon.
                        var mat = Matrix3x2.CreateRotation(-this.Rotation * (float)(Math.PI / 180f), this.Position);
                        mat *= Matrix3x2.CreateTranslation(new D2DPoint(-this.Position.X, -this.Position.Y));
                        var ogPos1 = D2DPoint.Transform(impactPos, mat);

                        SetOnFire(ogPos1);
                        nFlames++;
                    }

                    _damageCooldownTimeout.Restart();
                    _damageFlashTimer.Restart();
                }


                if (this.Hits <= 0)
                {
                    IsDamaged = true;
                    _damageDeflection = _rnd.NextFloat(-180, 180);

                    // Award attacking plane with missiles and health?
                    if (attackPlane.NumMissiles < Plane.MAX_MISSILES)
                    {
                        attackPlane.NumMissiles++;
                    }

                    attackPlane.Kills++;

                    if (attackPlane.Hits < MAX_HITS)
                        attackPlane.Hits += 2;
                }
            }

            float impactMass = 40f;

            //if (impactor is GuidedMissile missile)
            //    impactMass = missile.TotalMass * 4f;

            impactMass *= 4f;//10f;

            var velo = impactor.Velocity - this.Velocity;
            var force = (impactMass * velo.Length()) / 2f * 0.5f;
            var forceVec = (impactPos.Normalized() * force);
            var impactTq = GetTorque(impactPos, forceVec);


            this.RotationSpeed += impactTq / this.Mass * World.DT;
            this.Velocity += forceVec / this.Mass * World.DT;
        }


        private void SpawnDebris(int num, D2DPoint pos, D2DColor color)
        {
            for (int i = 0; i < num; i++)
            {
                var debris = new Debris(pos, this.Velocity, color);
                debris.SkipFrames = World.PHYSICS_STEPS;

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
            this.Hits = MAX_HITS;
            nFlames = 0;
            NumMissiles = MAX_MISSILES;
            IsDamaged = false;
            HasCrashed = false;
            _expireTimeout.Stop();
            _expireTimeout.Reset();
            _flipTimer.Restart();
            _flames.Clear();
            _thrustAmt.Target = 1f;
            WasHeadshot = false;
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
            _flames.ForEach(f => f.FlipY());
            _cockpitPosition.FlipY();


            if (_currentDir == Direction.Right)
                _currentDir = Direction.Left;
            else if (_currentDir == Direction.Left)
                _currentDir = Direction.Right;
        }

        private void CheckForFlip()
        {
            if (this.Rotation > 90f && this.Rotation < 270f)
                FlipPoly(Direction.Left);
            else if (this.Rotation > 0f && this.Rotation < 90f || this.Rotation > 270 && this.Rotation < 360f)
                FlipPoly(Direction.Right);
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



    }
}
