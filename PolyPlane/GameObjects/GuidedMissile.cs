using PolyPlane.GameObjects.Fixtures;
using PolyPlane.GameObjects.Guidance;
using PolyPlane.GameObjects.Tools;
using PolyPlane.Helpers;
using PolyPlane.Rendering;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public class GuidedMissile : GameObjectPoly, ILightMapContributor
    {
        public float Deflection = 0f;
        public bool FlameOn = false;

        public GameObject Target { get; set; }
        public GuidanceBase Guidance => _guidance;

        public float CurrentFuel
        {
            get { return _currentFuel; }
            set { _currentFuel = value; }
        }

        public D2DPoint CenterOfThrust => _centerOfThrust.Position;


        public float TotalMass
        {
            get { return this.Mass + _currentFuel; }

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

        public bool IsActivated = false;

        private readonly float THURST_VECTOR_AMT = 1f;
        private readonly float LIFESPAN = 100f;
        private readonly float BURN_RATE = 0.85f;
        private readonly float THRUST = 2500f;
        private readonly float FUEL = 10f;

        private float _currentFuel = 0f;
        private float _gForce = 0f;
        private float _gForcePeak = 0f;
        private float _guideRotation = 0f;

        private RenderPoly FlamePoly;
        private D2DColor _flameFillColor = new D2DColor(0.6f, D2DColor.Yellow);
        private readonly D2DColor _lightMapColor = new D2DColor(1f, 0.98f, 0.77f, 0.31f);

        private GuidanceType GuidanceType = GuidanceType.Advanced;
        private GuidanceBase _guidance;

        private Wing _tailWing;
        private Wing _noseWing;
        private Wing _rocketBody;
        private FixturePoint _centerOfThrust;
        private FixturePoint _warheadCenterMass;
        private FixturePoint _motorCenterMass;
        private FixturePoint _flamePos;
        private GameTimer _igniteCooldown;

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
            new D2DPoint(-8, 1.5f),
            new D2DPoint(-10, 0),
            new D2DPoint(-8, -1.5f),
        ];

        public GuidedMissile(GameObject player, D2DPoint position, D2DPoint velocity, float rotation)
        {
            this.PlayerID = player.ID.PlayerID;
            this.IsNetObject = true;
            _currentFuel = FUEL;

            this.Position = position;
            this.Velocity = velocity;
            this.Rotation = rotation;
            this.Owner = player;

            InitStuff();
        }

        public GuidedMissile(GameObject player, GameObject target, GuidanceType guidance = GuidanceType.Advanced) : base(player.Position, player.Velocity, player.Rotation)
        {
            this.PlayerID = player.ID.PlayerID;
            this.GuidanceType = guidance;
            this.Target = target;
            this.Owner = player;
            this.Rotation = player.Rotation;
            _currentFuel = FUEL;

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
            this.Flags = GameObjectFlags.SpatialGrid;
            this.Mass = 22.5f;
            this.RenderScale = 0.9f;
            this.RenderOrder = 2;

            _centerOfThrust = AddAttachment(new FixturePoint(this, new D2DPoint(-21, 0)), true);
            _warheadCenterMass = AddAttachment(new FixturePoint(this, new D2DPoint(12f, 0)), true);
            _motorCenterMass = AddAttachment(new FixturePoint(this, new D2DPoint(-13f, 0)), true);
            _flamePos = AddAttachment(new FixturePoint(this, new D2DPoint(-22f, 0)), true);

            this.Polygon = new RenderPoly(this, _missilePoly, new D2DPoint(-2f, 0f));
            this.FlamePoly = new RenderPoly(_flamePos, _flamePoly, new D2DPoint(6f, 0));

            InitWings();

            _igniteCooldown = AddTimer(1f);

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
            const float liftScale = 1.5f;
            const float minVelo = 600f;

            _tailWing = new Wing(this, new WingParameters()
            {
                RenderLength = 2.5f,
                RenderWidth = 1f,
                Area = 0.1f,
                MaxDeflection = 45f,
                MaxLiftForce = 4500f * liftScale,
                PivotPoint = new D2DPoint(-21f, 0f),
                Position = new D2DPoint(-22f, 0f),
                MinVelo = minVelo,
                ParasiticDrag = 0.7f,
                DragFactor = 0.8f,
                DeflectionRate = 45f,
                MaxAOA = 40f
            });

            _rocketBody = new Wing(this, new WingParameters()
            {
                RenderLength = 0f,
                Area = 0.045f,
                MaxLiftForce = 1000f * liftScale,
                MinVelo = minVelo,
                ParasiticDrag = 0.2f,
                MaxAOA = 30f,
                DragFactor = 0.4f,
            });

            _noseWing = new Wing(this, new WingParameters()
            {
                RenderLength = 2.5f,
                RenderWidth = 1f,
                Area = 0.06f,
                MaxDeflection = 20f,
                MaxLiftForce = 2000f * liftScale,
                Position = new D2DPoint(21.5f, 0f),
                MinVelo = minVelo,
                ParasiticDrag = 0.2f,
                DragFactor = 0.4f,
                MaxAOA = 30f
            });

            AddAttachment(_tailWing, true);
            AddAttachment(_rocketBody, true);
            AddAttachment(_noseWing, true);
        }

        public override void DoPhysicsUpdate(float dt)
        {
            base.DoPhysicsUpdate(dt);

            D2DPoint accel = D2DPoint.Zero;

            if (!this.IsNetObject)
            {
                // Apply aerodynamics.
                var liftDrag = D2DPoint.Zero;
                var cg = GetCenterOfGravity();

                var tailForce = _tailWing.GetForces(cg);
                var noseForce = _noseWing.GetForces(cg);
                var bodyForce = _rocketBody.GetForces(cg);

                liftDrag += tailForce.LiftAndDrag + noseForce.LiftAndDrag + bodyForce.LiftAndDrag;

                // Compute torque and rotation result.
                var thrust = GetThrust();
                var thrustTorque = Utilities.GetTorque(cg, _centerOfThrust.Position, thrust);
                var torqueRot = (tailForce.Torque + bodyForce.Torque + noseForce.Torque + thrustTorque) / this.GetInertia(this.TotalMass);

                this.RotationSpeed += torqueRot * dt;

                const float TAIL_AUTH = 1f;
                const float NOSE_AUTH = 0f;

                // Compute deflection.
                var nextDeflect = GetDeflectionAmount(_guideRotation);

                // Adjust the deflection as speed, rotation speed and AoA increases.
                // This is to try to prevent over-rotation caused by thrust vectoring.
                if (_currentFuel > 0f)
                {
                    const float MIN_DEF_SPD = 450f; // Minimum speed required for full deflection.
                    var spdFact = Utilities.Factor(this.Velocity.Length(), MIN_DEF_SPD);

                    const float MAX_DEF_AOA = 35f;// Maximum AoA allowed. Reduce deflection as AoA increases.
                    var aoaFact = 1f - (Math.Abs(_rocketBody.AoA) / (MAX_DEF_AOA + (spdFact * (MAX_DEF_AOA * 2f))));

                    const float MAX_DEF_ROT_SPD = 200f; // Maximum rotation speed allowed. Reduce deflection to try to control rotation speed.
                    var rotSpdFact = 1f - (Math.Abs(this.RotationSpeed) / (MAX_DEF_ROT_SPD + (spdFact * (MAX_DEF_ROT_SPD * 3f))));

                    // Ease out of SAS as fuel runs out.
                    var fuelFact = Utilities.FactorWithEasing(_currentFuel, FUEL, EasingFunctions.Out.EaseCubic);
                    nextDeflect = Utilities.Lerp(nextDeflect, nextDeflect * (aoaFact * rotSpdFact * spdFact), fuelFact);
                }

                _tailWing.Deflection = TAIL_AUTH * -nextDeflect;
                _noseWing.Deflection = NOSE_AUTH * nextDeflect;

                this.Deflection = _tailWing.Deflection;

                // Add thrust and integrate acceleration.
                accel += thrust * dt / TotalMass;
                accel += (liftDrag / TotalMass) * dt;

                this.Velocity += accel;
                this.Velocity += World.Gravity * dt;
            }

            var gforce = accel.Length() / dt / 9.8f;
            _gForce = gforce;
            _gForcePeak = Math.Max(_gForcePeak, _gForce);
        }

        public override void DoUpdate(float dt)
        {
            base.DoUpdate(dt);

            // Apply guidance.
            if (_guidance != null)
                _guideRotation = _guidance.GuideTo(dt);

            if (_currentFuel > 0f)
            {
                _currentFuel -= BURN_RATE * dt;
            }

            if (_currentFuel <= 0f && this.Age > LIFESPAN && this.MissedTarget)
                this.IsExpired = true;

            if (_currentFuel <= 0f)
                FlameOn = false;
        }

        private float GetDeflectionAmount(float dir)
        {
            var dirVec = Utilities.AngleToVectorDegrees(dir);
            var amtRot = Utilities.RadsToDegrees(Utilities.AngleToVectorDegrees(this.Rotation).Cross(dirVec));
            var rot = amtRot;

            rot = Utilities.ClampAngle180(rot);

            return rot;
        }

        public override void NetUpdate(D2DPoint position, D2DPoint velocity, float rotation, long frameTime)
        {
            base.NetUpdate(position, velocity, rotation, frameTime);

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

            _flameFillColor = D2DColor.Orange;

            UpdateFlame();

            if (FlameOn)
                ctx.DrawPolygon(this.FlamePoly, _flameFillColor, 1f, _flameFillColor);

            var fillColor = D2DColor.White;
            ctx.DrawPolygon(this.Polygon, D2DColor.Black, 0.3f, fillColor);

            _tailWing.Render(ctx);
            _rocketBody.Render(ctx);
            _noseWing.Render(ctx);

            if (World.ShowTracking && _guidance != null)
            {
                ctx.FillEllipseSimple(_guidance.CurrentAimPoint, 50f, D2DColor.LawnGreen);
                ctx.FillEllipseSimple(_guidance.StableAimPoint, 40f, D2DColor.Blue);
                ctx.FillEllipseSimple(_guidance.ImpactPoint, 30f, D2DColor.Red);

                ctx.DrawLine(this.Position, _guidance.CurrentAimPoint, D2DColor.LawnGreen, 5f);
                ctx.DrawLine(this.Position, _guidance.StableAimPoint, D2DColor.Blue, 5f);
                ctx.DrawLine(this.Position, _guidance.ImpactPoint, D2DColor.Red, 5f);
            }
        }

        private void UpdateFlame()
        {
            float flameAngle = 0f;

            flameAngle = GetThrust().Angle();

            // Make the flame do flamey things...(Wiggle and color)
            var thrust = GetThrust().Length();
            var len = this.Velocity.Length() * 0.05f;
            len += thrust * 0.01f;
            len *= 0.8f;
            FlamePoly.SourcePoly[1].X = -_rnd.NextFloat(9f + len, 11f + len);
            _flameFillColor.g = _rnd.NextFloat(0.6f, 0.86f);
            FlamePoly.Update(_flamePos.Position, flameAngle, this.RenderScale);
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

        private D2DPoint GetCenterOfGravity()
        {
            var cm = (this.Mass * _warheadCenterMass.Position + _currentFuel * _motorCenterMass.Position) / (this.Mass + _currentFuel);
            return cm;
        }

        private D2DPoint GetThrust()
        {
            var thrust = D2DPoint.Zero;

            if (_currentFuel > 0f && FlameOn)
            {
                var vec = Utilities.AngleToVectorDegrees(this.Rotation + (_tailWing.Deflection * THURST_VECTOR_AMT));
                vec *= THRUST;
                thrust = vec;
            }

            return thrust;
        }

        float ILightMapContributor.GetLightRadius()
        {
            const float LIGHT_RADIUS = 500f;
            var flickerScale = Utilities.Rnd.NextFloat(0.5f, 1f);

            return LIGHT_RADIUS * flickerScale;
        }

        float ILightMapContributor.GetIntensityFactor()
        {
            return 1f;
        }

        bool ILightMapContributor.IsLightEnabled()
        {
            return this.FlameOn;
        }

        D2DPoint ILightMapContributor.GetLightPosition()
        {
            return _centerOfThrust.Position;
        }

        D2DColor ILightMapContributor.GetLightColor()
        {
            return _lightMapColor;
        }
    }
}
