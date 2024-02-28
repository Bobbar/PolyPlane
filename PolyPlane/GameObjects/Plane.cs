using PolyPlane.AI_Behavior;
using System.Diagnostics;
using System.Numerics;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public class Plane : GameObjectPoly
    {
        
        public Radar Radar { get; set; }
        private const int MAX_FLAMES = 10;
        private int nFlames = 0;
        private const int MAX_MISSILES = 6;
        public int NumMissiles = MAX_MISSILES;
        public bool IsAI => _isAIPlane;
        public bool IsDefending = false;

        public float Mass 
        {
            get { return _mass + (NumMissiles * 0.853f); }
            //get { return _mass; }

            set {  _mass = value; }
        }

        private float _mass = 379.8f;

        public float Thrust { get; set; } = 10f;
        public bool FiringBurst { get; set; } = false;
        public bool DroppingDecoy { get; set; } = false;

        public float GForce => _gForce;
        public bool ThrustOn { get; set; } = false;
        public float ThrustAmount => _thrustAmt.Value;
        public bool AutoPilotOn { get; set; } = false;
        public bool SASOn { get; set; } = true;
        public bool HasCrashed { get; set; } = false;

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

        private GameTimer _engageTimer;
        private GameTimer _dropDecoyTimer = new GameTimer(4f);
        private GameTimer _dropDecoyCooldownTimer = new GameTimer(2f);
        private GameTimer _dropDecoyDelayTimer = new GameTimer(0.3f);
        private GameTimer _expireTimeout = new GameTimer(50f);

        private float _damageDeflection = 0f;

        private RenderPoly FlamePoly;
        private D2DColor _flameFillColor = new D2DColor(0.6f, D2DColor.Yellow);
        private FixturePoint _flamePos;
        private FixturePoint _gunPosition;
        private D2DColor _planeColor;
        private SmokeTrail _contrail;
        private int _throttlePos = 0;

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

                _isAIPlane = true;

                Thrust = 3040f;

            }
            else
            {

                Thrust = 5040f;

            }

            _thrustAmt.Target = 1f;

            _dropDecoyTimer.TriggerCallback = () => _dropDecoyCooldownTimer.Restart();
            _dropDecoyCooldownTimer.TriggerCallback = () => _dropDecoyTimer.Restart();
            _dropDecoyDelayTimer.TriggerCallback = () =>
            {
                _dropDecoyTimer.Restart();
                this.DroppingDecoy = true;
            };

            _planeColor = D2DColor.Randomly();

            this.Polygon = new RenderPoly(_poly, 1.5f);

            InitStuff();

            _controlWing.Deflection = 2f;
            _centerOfThrust = new FixturePoint(this, new D2DPoint(-33f, 0));
        }

        private void InitStuff()
        {
            float defRate = 150f;

            if (_isAIPlane)
                defRate = 20f;

            //AddWing(new Wing(this, 10f * _renderOffset, 2.11f, 40f, 52200f, new D2DPoint(1.5f, 1f), defRate));
            //AddWing(new Wing(this, 5f * _renderOffset, 0.844f, 50f, 31100f, new D2DPoint(-35f, 1f), defRate), true);

            //AddWing(new Wing(this, 10f * _renderOffset, 2.11f, 0f, 62200f, new D2DPoint(1.5f, 1f), defRate));
            //AddWing(new Wing(this, 5f * _renderOffset, 0.844f, 50f, 41100f, new D2DPoint(-35f, 1f), defRate), true);

            //AddWing(new Wing(this, 10f * _renderOffset, 0.6084f, 0f, 60000f, new D2DPoint(1.5f, 1f), defRate));
            //AddWing(new Wing(this, 5f * _renderOffset, 0.17161f, 50f, 40500f, new D2DPoint(-35f, 1f), defRate), true);

            var totArea = 0.78001f;
            var mainRatio = 0.7f;//0.7766f;
            var tailRatio = 1f - mainRatio;

            AddWing(new Wing(this, 10f * _renderOffset, totArea * mainRatio, 0f, 46800f, new D2DPoint(1.5f, 1f), defRate));
            AddWing(new Wing(this, 5f * _renderOffset, totArea * tailRatio, 50f, 13200f, new D2DPoint(-35f, 1f), defRate), true);


            //AddWing(new Wing(this, 10f * _renderOffset, 0.6084f, 0f, 46800f, new D2DPoint(1.5f, 1f), defRate));
            //AddWing(new Wing(this, 5f * _renderOffset, 0.17161f, 50f, 13200f, new D2DPoint(-35f, 1f), defRate), true);

            this.FlamePoly = new RenderPoly(_flamePoly, new D2DPoint(12f, 0), 1.7f);
            _flamePos = new FixturePoint(this, new D2DPoint(-37f, 0));
            _gunPosition = new FixturePoint(this, new D2DPoint(33f, 0));

            _contrail = new SmokeTrail(this, o =>
            {
                var p = o as Plane;
                return p.ExhaustPosition;
            });

            _expireTimeout.TriggerCallback = () => this.IsExpired = true;
        }

        public override void Update(float dt, D2DSize viewport, float renderScale)
        {
            base.Update(dt, viewport, renderScale * _renderOffset);

            this.Radar?.Update(dt);

            if (_aiBehavior != null)
                _aiBehavior.Update(dt);

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
            const float MIN_DEF_SPD = 300f;//450f; // Minimum speed required for full deflection.
            var velo = this.Velocity.Length();
            if (_thrustAmt.Value > 0f && velo < MIN_DEF_SPD * 2f && SASOn)
            {
                var spdFact = Helpers.Factor(velo, MIN_DEF_SPD);

                const float MAX_DEF_AOA = 20f;// Maximum AoA allowed. Reduce deflection as AoA increases.
                var aoaFact = 1f - (Math.Abs(Wings[0].AoA) / (MAX_DEF_AOA + (spdFact * (MAX_DEF_AOA * 6f))));

                const float MAX_DEF_ROT_SPD = 50f; // Maximum rotation speed allowed. Reduce deflection to try to control rotation speed.
                var rotSpdFact = 1f - (Math.Abs(this.RotationSpeed) / (MAX_DEF_ROT_SPD + (spdFact * (MAX_DEF_ROT_SPD * 7-f))));

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
                wingForce *= 0.1f;
                wingTorque *= 0.1f;
                AutoPilotOn = false;
                SASOn = false;
                ThrustOn = false;
                _thrustAmt.Set(0f);
                //_controlWing.Deflection = -20f;
                _controlWing.Deflection = _damageDeflection;
            }

            _force = wingForce;

            this.RotationSpeed += wingTorque / this.Mass * dt;

            this.Velocity += thrust / this.Mass * dt;
            this.Velocity += wingForce / this.Mass * dt;
            this.Velocity += (World.Gravity * (IsDamaged ? 4f : 1f)) * dt;

            var totForce = (thrust / this.Mass * dt) + (wingForce / this.Mass * dt);

            var gforce = totForce.Length() / dt / World.Gravity.Y;
            _gForce = gforce;

            // TODO:  This is so messy...
            Wings.ForEach(w => w.Update(dt, viewport, renderScale * _renderOffset));
            _centerOfThrust.Update(dt, viewport, renderScale * _renderOffset);
            _thrustAmt.Update(dt);
            _apAngleLimiter.Update(dt);
            CheckForFlip();

            _contrail.Update(dt);

            _flamePos.Update(dt, viewport, renderScale * _renderOffset);
            _gunPosition.Update(dt, viewport, renderScale * _renderOffset);

            var thrustMag = thrust.Length();
            var flameAngle = thrust.Angle();
            var len = this.Velocity.Length() * 0.05f;
            len += thrustMag * 0.01f;
            len *= 0.6f;
            FlamePoly.SourcePoly[1].X = -_rnd.NextFloat(9f + len, 11f + len);
            _flameFillColor.g = _rnd.NextFloat(0.6f, 0.86f);

            FlamePoly.Update(_flamePos.Position, flameAngle, renderScale * _renderOffset);

            _flipTimer.Update(dt);
            _dropDecoyTimer.Update(dt);
            _dropDecoyCooldownTimer.Update(dt);
            _dropDecoyDelayTimer.Update(dt);

            if (this.DroppingDecoy && _dropDecoyCooldownTimer.IsRunning)
                this.DroppingDecoy = false;
            
            _expireTimeout.Update(dt);
        }

        public override void Render(RenderContext ctx)
        {
            base.Render(ctx);

            //DrawFOVCone(gfx);

            if (_thrustAmt.Value > 0f && GetThrust(true).Length() > 0f)
                ctx.DrawPolygon(this.FlamePoly.Poly, _flameFillColor, 1f, D2DDashStyle.Solid, _flameFillColor);

            ctx.DrawPolygon(this.Polygon.Poly, D2DColor.Black, 1f, D2DDashStyle.Solid, _planeColor);

            Wings.ForEach(w => w.Render(ctx));

            _contrail.Render(ctx, p => -p.Y > 20000 && -p.Y < 70000 && ThrustAmount > 0f);
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

      

        public void EngagePlayer(float duration)
        {
            if (_engageTimer.IsRunning == false)
            {
                _engageTimer.Interval = duration;
                _engageTimer.Reset();
                _engageTimer.Start();

                Debug.WriteLine("Engaging Player!");
            }
        }

        public void FireMissile(GameObject target)
        {
            if (this.NumMissiles <= 0)
            {
                Debug.WriteLine("Click...");
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

            this.NumMissiles--;
        }

        public void FireBullet(Action<D2DPoint> addExplosion)
        {
            if (IsDamaged)
                return;

            var bullet = new Bullet(this);

            bullet.AddExplosionCallback = addExplosion;
            FireBulletCallback(bullet);
        }

        public void DropDecoys()
        {
            if (IsDamaged)
                return;

            if (!this.DroppingDecoy && !_dropDecoyDelayTimer.IsRunning)
                _dropDecoyDelayTimer.Restart();
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
            var amt = Helpers.RadsToDegrees(this.Velocity.Normalized().Cross(Helpers.AngleToVectorDegrees(dir)));
            var rot = this.Velocity.Angle() + amt;

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

        public void SetOnFire(List<GameObject> flameList)
        {
            var offset = Helpers.RandOPointInPoly(_poly);
            SetOnFire(offset, flameList);
        }

        public void SetOnFire(D2DPoint pos, List<GameObject> flameList)
        {
            flameList.Add(new Flame(this, pos, 8f));
        }

        public void DoImpact(GameObject impactor, List<GameObject> flameList)
        {
            DoImpact(impactor, impactor.Position, flameList);
        }

        public void DoImpact(GameObject impactor, D2DPoint impactPos, List<GameObject> flameList)
        {
            if (nFlames < MAX_FLAMES)
            {
                var mat = Matrix3x2.CreateRotation(-this.Rotation * (float)(Math.PI / 180f), this.Position);
                mat *= Matrix3x2.CreateTranslation(new D2DPoint(-this.Position.X, -this.Position.Y));
                var ogPos1 = D2DPoint.Transform(impactPos, mat);

                SetOnFire(ogPos1, flameList);
                nFlames++;
            }

            if (!IsDamaged)
            {

                IsDamaged = true;
                _damageDeflection = _rnd.NextFloat(-180, 180);
            }

            float impactMass = 40f;

            //if (impactor is GuidedMissile missile)
            //    impactMass = missile.TotalMass * 4f;

            impactMass *= 10f;

            var velo = impactor.Velocity - this.Velocity;
            var force = (impactMass * velo.Length()) / 2f * 0.5f;
            var forceVec = (impactPos.Normalized() * force);
            var impactTq = GetTorque(impactPos, forceVec);


            this.RotationSpeed += impactTq / this.Mass * World.DT;
            this.Velocity += forceVec / this.Mass * World.DT;
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
            nFlames = 0;
            NumMissiles = MAX_MISSILES;
            IsDamaged = false;
            HasCrashed = false;
            _expireTimeout.Stop();
            _expireTimeout.Reset();
            _flipTimer.Restart();
            _thrustAmt.Target = 1f;
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

            const float thrustVectorAmt = 1f;//1f;
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
