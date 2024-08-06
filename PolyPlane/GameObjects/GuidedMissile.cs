using PolyPlane.GameObjects.Guidance;
using PolyPlane.Helpers;
using PolyPlane.Rendering;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public class GuidedMissile : Missile
    {
        public float Deflection = 0f;
        public bool FlameOn = false;

        public GuidanceBase Guidance => _guidance;

        public float CurrentFuel
        {
            get { return _currentFuel; }
            set { _currentFuel = value; }
        }

        public D2DPoint CenterOfThrust => _centerOfThrust.Position;


        public float TotalMass
        {
            get { return MASS + _currentFuel; }

        }

        public float TargetDistance { get; set; } = 0f;

        public bool MissedTarget
        {
            get
            {
                if (_guidance != null)
                    return _guidance.MissedTarget;
                else
                    return false;
            }
        }

        private bool IsActivated = false;
        private readonly float THURST_VECTOR_AMT = 1f;
        private readonly float LIFESPAN = 70f;
        private readonly float BURN_RATE = 0.85f;
        private readonly float THRUST = 2500f;
        private readonly float MASS = 22.5f;
        private readonly float FUEL = 10f;

        private float Inertia
        {
            get { return TotalMass * 20f; }
        }

        private float _currentFuel = 0f;
        private float _gForce = 0f;
        private float _gForcePeak = 0f;
        private float _initRotation = 0f;

        private RenderPoly FlamePoly;
        private D2DColor _flameFillColor = new D2DColor(0.6f, D2DColor.Yellow);

        private GuidanceType GuidanceType = GuidanceType.Advanced;
        private GuidanceBase _guidance;

        private bool _useControlSurfaces = false;
        private bool _useThrustVectoring = false;
        private Wing _tailWing;
        private Wing _noseWing;
        private Wing _rocketBody;
        private FixturePoint _centerOfThrust;
        private FixturePoint _warheadCenterMass;
        private FixturePoint _motorCenterMass;
        private FixturePoint _flamePos;
        private GameTimer _igniteCooldown = new GameTimer(1f);

        private const float LEN = 7f;

        private static readonly D2DPoint[] _missilePoly =
        [
            new D2DPoint(28 + LEN, 0),
            new D2DPoint(25 + LEN, -2),
            new D2DPoint(19 + LEN, -2),
            new D2DPoint(14 + LEN, -5),
            new D2DPoint(14 + LEN, -2),
            new D2DPoint(-17, -2),
            new D2DPoint(-19, -4),
            new D2DPoint(-21, -6),
            new D2DPoint(-23, -6),
            new D2DPoint(-23, -2),
            new D2DPoint(-25, -2),
            new D2DPoint(-25, 2),
            new D2DPoint(-23, 2),
            new D2DPoint(-23, 6),
            new D2DPoint(-21, 6),
            new D2DPoint(-19, 4),
            new D2DPoint(-17, 2),
            new D2DPoint(14 + LEN, 2),
            new D2DPoint(14 + LEN, 5),
            new D2DPoint(19 + LEN, 2),
            new D2DPoint(25 + LEN, 2)
        ];

        private static readonly D2DPoint[] _flamePoly =
        [
            new D2DPoint(-8, 2),
            new D2DPoint(-10, 0),
            new D2DPoint(-8, -2),
        ];

        public GuidedMissile(GameObject player, D2DPoint position, D2DPoint velocity, float rotation)
        {
            this.PlayerID = player.ID.PlayerID;
            this.IsNetObject = true;
            _useControlSurfaces = true;
            _useThrustVectoring = true;
            _currentFuel = FUEL;

            this.Position = position;
            this.Velocity = velocity;
            this.Rotation = rotation;
            this.Owner = player;

            InitStuff();
        }

        public GuidedMissile(GameObject player, GameObject target, GuidanceType guidance = GuidanceType.Advanced, bool useControlSurfaces = false, bool useThrustVectoring = false) : base(player.Position, player.Velocity, player.Rotation, player, target)
        {
            this.PlayerID = player.ID.PlayerID;
            this.GuidanceType = guidance;
            this.Target = target;
            this.Owner = player;
            this.Rotation = player.Rotation;
            _initRotation = this.Rotation;
            _currentFuel = FUEL;
            _useControlSurfaces = useControlSurfaces;
            _useThrustVectoring = useThrustVectoring;

            _guidance = GetGuidance(target);

            var ownerPlane = this.Owner as FighterPlane;
            if (ownerPlane != null)
            {
                const float EJECT_FORCE = 200f;
                var toRight = ownerPlane.FlipDirection == Direction.Right;
                var rotVec = Utilities.AngleToVectorDegrees(ownerPlane.Rotation + (toRight ? 180f : 0f));
                var topVec = new D2DPoint(rotVec.Y, -rotVec.X);
                this.Velocity += topVec * EJECT_FORCE;
            }

            InitStuff();
        }

        private void InitStuff()
        {
            this.RenderOffset = 0.9f;

            _centerOfThrust = new FixturePoint(this, new D2DPoint(-22, 0));
            _warheadCenterMass = new FixturePoint(this, new D2DPoint(6f, 0));
            _motorCenterMass = new FixturePoint(this, new D2DPoint(-11f, 0));
            _flamePos = new FixturePoint(this, new D2DPoint(-22f, 0));

            this.Polygon = new RenderPoly(_missilePoly, new D2DPoint(-2f, 0f));
            this.FlamePoly = new RenderPoly(_flamePoly, new D2DPoint(6f, 0));

            InitWings();

            _igniteCooldown.TriggerCallback = () =>
            {
                IsActivated = true;
                FlameOn = true;

                // Add a quick impulse/boost when we ignite.
                const float BOOST_AMT = 100f;
                this.Velocity += Utilities.AngleToVectorDegrees(this.Rotation, BOOST_AMT);
            };

            _igniteCooldown.Restart();
        }

        private void InitWings()
        {
            if (_useControlSurfaces)
            {
                var liftScale = 0.8f;

                _tailWing = new Wing(this, new WingParameters()
                {
                    RenderLength = 2.5f,
                    RenderWidth = 1f,
                    Area = 0.1f,
                    MaxDeflection = 50f,
                    MaxLiftForce = 6500f * liftScale,
                    Position = new D2DPoint(-22f, 0f),
                    MinVelo = 650f,
                    ParasiticDrag = 0.4f,
                    AOAFactor = 0.5f,
                    DeflectionRate = 30f,
                    MaxAOA = 50f
                });

                _rocketBody = new Wing(this, new WingParameters()
                {
                    RenderLength = 0f,
                    Area = 0.075f,
                    MaxLiftForce = 1500f * liftScale,
                    MinVelo = 700f,
                    ParasiticDrag = 0.2f,
                    MaxAOA = 40f,
                    AOAFactor = 0.2f
                });

                _noseWing = new Wing(this, new WingParameters()
                {
                    RenderLength = 2.5f,
                    RenderWidth = 1f,
                    Area = 0.025f,
                    MaxDeflection = 20f,
                    MaxLiftForce = 4000f * liftScale,
                    Position = new D2DPoint(21.5f, 0f),
                    MinVelo = 650f,
                    ParasiticDrag = 0.2f,
                    AOAFactor = 0.1f,
                    MaxAOA = 40f
                });
            }
            else
            {
                _rocketBody = new Wing(this, new WingParameters() { RenderLength = 4f, Area = 0.4f });
            }
        }


        public override void Update(float dt, float renderScale)
        {
            // Apply guidance.
            var guideRotation = 0f;

            if (_guidance != null)
                guideRotation = _guidance.GuideTo(dt);

            for (int i = 0; i < World.PHYSICS_SUB_STEPS; i++)
            {
                var partialDT = World.SUB_DT;

                base.Update(partialDT, renderScale * this.RenderOffset);

                if (_useControlSurfaces)
                {
                    _tailWing.Update(partialDT, renderScale * this.RenderOffset);
                    _noseWing.Update(partialDT, renderScale * this.RenderOffset);
                    _rocketBody.Update(partialDT, renderScale * this.RenderOffset);
                }
                else
                {
                    _rocketBody.Update(partialDT, renderScale * this.RenderOffset);
                }

                _centerOfThrust.Update(partialDT, renderScale * this.RenderOffset);
                _warheadCenterMass.Update(partialDT, renderScale * this.RenderOffset);
                _motorCenterMass.Update(partialDT, renderScale * this.RenderOffset);
                _flamePos.Update(partialDT, renderScale * this.RenderOffset);

                D2DPoint accel = D2DPoint.Zero;

                if (!this.IsNetObject)
                {
                    // Apply aerodynamics.
                    var liftDrag = D2DPoint.Zero;

                    if (_useControlSurfaces)
                    {
                        var tailForce = _tailWing.GetLiftDragForce();
                        var noseForce = _noseWing.GetLiftDragForce();
                        var bodyForce = _rocketBody.GetLiftDragForce();
                        liftDrag += tailForce + noseForce + bodyForce;

                        // Compute torque and rotation result.
                        var tailTorque = GetTorque(_tailWing, tailForce);
                        var bodyTorque = GetTorque(_rocketBody, bodyForce);
                        var noseTorque = GetTorque(_noseWing, noseForce);
                        var thrustTorque = GetTorque(_centerOfThrust.Position, GetThrust(thrustVector: _useThrustVectoring));

                        var torqueRot = (tailTorque + bodyTorque + noseTorque + thrustTorque) / this.Inertia;
                        this.RotationSpeed += torqueRot * partialDT;
                    }
                    else
                    {
                        var bodyForce = _rocketBody.GetLiftDragForce();
                        liftDrag += bodyForce;
                    }


                    if (!this.IsActivated)
                        guideRotation = _initRotation;

                    if (_useControlSurfaces)
                    {
                        const float TAIL_AUTH = 1f;
                        const float NOSE_AUTH = 0f;

                        // Compute deflection.
                        var veloAngle = this.Velocity.Angle(true);
                        var nextDeflect = Utilities.ClampAngle180(guideRotation - veloAngle);

                        // Adjust the deflection as speed, rotation speed and AoA increases.
                        // This is to try to prevent over-rotation caused by thrust vectoring.
                        if (_currentFuel > 0f && _useThrustVectoring)
                        {
                            const float MIN_DEF_SPD = 550f;//450f; // Minimum speed required for full deflection.
                            var spdFact = Utilities.Factor(this.Velocity.Length(), MIN_DEF_SPD);

                            const float MAX_DEF_AOA = 25f;// Maximum AoA allowed. Reduce deflection as AoA increases.
                            var aoaFact = 1f - (Math.Abs(_rocketBody.AoA) / (MAX_DEF_AOA + (spdFact * (MAX_DEF_AOA * 2f))));

                            const float MAX_DEF_ROT_SPD = 80f; // Maximum rotation speed allowed. Reduce deflection to try to control rotation speed.
                            var rotSpdFact = 1f - (Math.Abs(this.RotationSpeed) / (MAX_DEF_ROT_SPD + (spdFact * (MAX_DEF_ROT_SPD * 3f))));

                            // Ease out of SAS as fuel runs out.
                            var fuelFact = Utilities.FactorWithEasing(_currentFuel, FUEL, EasingFunctions.EaseOutCubic);
                            nextDeflect = Utilities.Lerp(nextDeflect, nextDeflect * (aoaFact * rotSpdFact * spdFact), fuelFact);
                        }

                        _tailWing.Deflection = TAIL_AUTH * -nextDeflect;
                        _noseWing.Deflection = NOSE_AUTH * nextDeflect;

                        this.Deflection = _tailWing.Deflection;
                    }
                    else
                    {
                        this.Rotation = guideRotation;
                    }

                    // Add thrust and integrate acceleration.
                    accel += GetThrust(thrustVector: false) * partialDT / TotalMass;
                    accel += (liftDrag / TotalMass) * partialDT;

                    this.Velocity += accel;
                    this.Velocity += World.Gravity * partialDT;
                }

                var gforce = accel.Length() / partialDT / 9.8f;
                _gForce = gforce;
                _gForcePeak = Math.Max(_gForcePeak, _gForce);
            }

            if (_currentFuel > 0f)
            {
                _currentFuel -= BURN_RATE * dt;
            }

            if (_currentFuel <= 0f && this.Age > LIFESPAN && this.MissedTarget)
                this.IsExpired = true;

            _igniteCooldown.Update(dt);

            if (_currentFuel <= 0f)
                FlameOn = false;

            this.RecordHistory();
        }

        public override void NetUpdate(float dt, D2DPoint position, D2DPoint velocity, float rotation, double frameTime)
        {
            base.NetUpdate(dt, position, velocity, rotation, frameTime);

            _tailWing.Deflection = this.Deflection;
        }

        public void ChangeTarget(GameObject target)
        {
            this.Target = target;

            if (_guidance != null)
                _guidance.Target = target;
        }

        private GuidanceBase GetGuidance(GameObject target)
        {
            switch (GuidanceType)
            {
                case GuidanceType.Advanced:
                    return new AdvancedGuidance(this, target);

                case GuidanceType.BasicLOS:
                    return new BasicLOSGuidance(this, target);

                case GuidanceType.SimplePN:
                    return new SimplePNGuidance(this, target);

                case GuidanceType.QuadraticPN:
                    return new QuadraticPNGuidance(this, target);
            }

            return new AdvancedGuidance(this, target);
        }

        public override void Render(RenderContext ctx)
        {
            base.Render(ctx);

            if (_useThrustVectoring)
                _flameFillColor = D2DColor.Orange;

            UpdateFlame();

            if (FlameOn)
                ctx.DrawPolygon(this.FlamePoly.Poly, _flameFillColor, 1f, D2DDashStyle.Solid, _flameFillColor);

            var fillColor = D2DColor.White;
            ctx.DrawPolygon(this.Polygon.Poly, D2DColor.Black, 0.5f, D2DDashStyle.Solid, fillColor);

            if (_useControlSurfaces)
            {
                _tailWing.Render(ctx);
                _noseWing.Render(ctx);
            }

            if (World.ShowTracking && _guidance != null)
            {
                ctx.FillEllipse(new D2DEllipse(_guidance.CurrentAimPoint, new D2DSize(50f, 50f)), D2DColor.LawnGreen);
                ctx.FillEllipse(new D2DEllipse(_guidance.StableAimPoint, new D2DSize(40f, 40f)), D2DColor.Blue);
                ctx.FillEllipse(new D2DEllipse(_guidance.ImpactPoint, new D2DSize(30f, 30f)), D2DColor.Red);

                ctx.DrawLine(this.Position, _guidance.CurrentAimPoint, D2DColor.LawnGreen, 5f);
                ctx.DrawLine(this.Position, _guidance.StableAimPoint, D2DColor.Blue, 5f);
                ctx.DrawLine(this.Position, _guidance.ImpactPoint, D2DColor.Red, 5f);
            }
        }

        private void UpdateFlame()
        {
            float flameAngle = 0f;

            if (_useThrustVectoring)
            {
                flameAngle = GetThrust(_useThrustVectoring).Angle();
            }
            else
            {
                const float DEF_AMT = 0.2f; // How much the flame will be deflected in relation to velocity.
                flameAngle = this.Rotation - (Utilities.ClampAngle180(this.Rotation - this.Velocity.Angle(true)) * DEF_AMT);
            }

            // Make the flame do flamey things...(Wiggle and color)
            var thrust = GetThrust().Length();
            var len = this.Velocity.Length() * 0.05f;
            len += thrust * 0.01f;
            len *= 0.8f;
            FlamePoly.SourcePoly[1].X = -_rnd.NextFloat(9f + len, 11f + len);
            _flameFillColor.g = _rnd.NextFloat(0.6f, 0.86f);
            FlamePoly.Update(_flamePos.Position, flameAngle, World.RenderScale * this.RenderOffset);
        }

        private void DrawFOVCone(D2DGraphics gfx, float fov)
        {
            const float LEN = 20000f;
            var color = D2DColor.Red;

            var centerLine = Utilities.AngleToVectorDegrees(this.Rotation, LEN);
            var cone1 = Utilities.AngleToVectorDegrees(this.Rotation + (fov * 0.5f), LEN);
            var cone2 = Utilities.AngleToVectorDegrees(this.Rotation - (fov * 0.5f), LEN);


            gfx.DrawLine(this.Position, this.Position + cone1, D2DColor.Red, 3f);
            gfx.DrawLine(this.Position, this.Position + cone2, D2DColor.Blue, 3f);
        }

        private float GetTorque(Wing wing, D2DPoint force)
        {
            return GetTorque(wing.Position, force);
        }

        private float GetTorque(D2DPoint pos, D2DPoint force)
        {
            // How is it so simple?
            var r = pos - GetCenterOfGravity();

            var torque = Utilities.Cross(r, force);
            return torque;
        }

        private D2DPoint GetCenterOfGravity()
        {
            var cm = (MASS * _warheadCenterMass.Position + _currentFuel * _motorCenterMass.Position) / (MASS + _currentFuel);
            return cm;
        }

        private D2DPoint GetThrust(bool thrustVector = false)
        {
            var thrust = D2DPoint.Zero;

            if (_currentFuel > 0f && FlameOn)
            {
                D2DPoint vec;

                if (thrustVector)
                    vec = Utilities.AngleToVectorDegrees(this.Rotation + (_tailWing.Deflection * THURST_VECTOR_AMT));
                else
                    vec = Utilities.AngleToVectorDegrees(this.Rotation);

                vec *= THRUST;

                thrust = vec;
            }

            return thrust;
        }
    }
}
