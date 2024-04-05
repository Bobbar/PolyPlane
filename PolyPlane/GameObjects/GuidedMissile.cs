using PolyPlane.GameObjects.Guidance;
using PolyPlane.Rendering;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public class GuidedMissile : Missile
    {
        public float CurrentFuel
        {
            get { return _currentFuel; }
            set { _currentFuel = value; }
        }
        public D2DPoint CenterOfThrust => _centerOfThrust.Position;

        private static readonly D2DPoint[] _missilePoly = new D2DPoint[]
        {
            new D2DPoint(28, 0),
            new D2DPoint(25, 2),
            new D2DPoint(-20, 2),
            new D2DPoint(-22, 4),
            new D2DPoint(-22, -4),
            new D2DPoint(-20, -2),
            new D2DPoint(25, -2)
        };


        private static readonly D2DPoint[] _flamePoly = new D2DPoint[]
        {
            new D2DPoint(-8, 2),
            new D2DPoint(-10, 0),
            new D2DPoint(-8, -2),
        };

        public float TotalMass
        {
            get { return MASS + _currentFuel; }

        }

        public bool IsDistracted = false;
        public float TargetDistance { get; set; } = 0f;

        //public bool MissedTarget => _guidance.MissedTarget;

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



        private readonly float THURST_VECTOR_AMT = 1f;
        private readonly float LIFESPAN = 40f;
        private readonly float BURN_RATE = 0.04f;//0.06f;
        private readonly float THRUST = 84f;//126f;
        private readonly float MASS = 0.478f;
        private readonly float FUEL = 0.375f;

        private float _age = 0;
        private float _currentFuel = 0f;
        private float _gForce = 0f;
        private float _gForcePeak = 0f;

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
        private GameTimer _decoyDistractCooldown = new GameTimer(1f);
        private GameTimer _decoyDistractArm = new GameTimer(2f);

        public float Deflection = 0f;

        private D2DPoint _com = D2DPoint.Zero;

        public GuidedMissile(GameObject player, D2DPoint position, D2DPoint velocity, float rotation)
        {
            this.RenderOffset = 0.8f;
            this.PlayerID = player.ID.PlayerID;
            this.IsNetObject = true;
            _useControlSurfaces = true;
            _useThrustVectoring = true;

            this.Position = position;
            this.Velocity = velocity;
            this.Rotation = rotation;
            this.Owner = player;

            _centerOfThrust = new FixturePoint(this, new D2DPoint(-22, 0));
            _warheadCenterMass = new FixturePoint(this, new D2DPoint(4f, 0));
            _motorCenterMass = new FixturePoint(this, new D2DPoint(-11f, 0));
            _flamePos = new FixturePoint(this, new D2DPoint(-22f, 0));

            this.Polygon = new RenderPoly(_missilePoly, new D2DPoint(-2f, 0f));
            this.FlamePoly = new RenderPoly(_flamePoly, new D2DPoint(6f, 0));

            var liftscale = 1.5f;
            _tailWing = new Wing(this, 4f, 0.002f, 50f, 85.1f * liftscale, new D2DPoint(-22f, 0));
            _rocketBody = new Wing(this, 0f, 0.00159f, 0f, 26.6f * liftscale, D2DPoint.Zero);
            _noseWing = new Wing(this, 4f, 0.00053f, 20f, 74.4f * liftscale, new D2DPoint(19.5f, 0));
        }

        public GuidedMissile(GameObject player, GameObject target, GuidanceType guidance = GuidanceType.Advanced, bool useControlSurfaces = false, bool useThrustVectoring = false) : base(player.Position, player.Velocity, player.Rotation, player, target)
        {
            this.RenderOffset = 0.8f;
            this.PlayerID = player.ID.PlayerID;

            _currentFuel = FUEL;

            _centerOfThrust = new FixturePoint(this, new D2DPoint(-22, 0));
            _warheadCenterMass = new FixturePoint(this, new D2DPoint(4f, 0));
            _motorCenterMass = new FixturePoint(this, new D2DPoint(-11f, 0));
            _flamePos = new FixturePoint(this, new D2DPoint(-22f, 0));

            this.GuidanceType = guidance;
            this.Target = target;
            this.Owner = player;
            this.Polygon = new RenderPoly(_missilePoly, new D2DPoint(-2f, 0f));
            this.FlamePoly = new RenderPoly(_flamePoly, new D2DPoint(6f, 0));
            this.Rotation = player.Rotation;

            _useControlSurfaces = useControlSurfaces;
            _useThrustVectoring = useThrustVectoring;

            if (_useControlSurfaces)
            {
                var liftscale = 1.5f;
                _tailWing = new Wing(this, 4f, 0.002f, 50f, 85.1f * liftscale, new D2DPoint(-22f, 0));
                _rocketBody = new Wing(this, 0f, 0.00159f, 0f, 26.6f * liftscale, D2DPoint.Zero);
                _noseWing = new Wing(this, 4f, 0.00053f, 20f, 74.4f * liftscale, new D2DPoint(19.5f, 0));

            }
            else
            {
                _rocketBody = new Wing(this, 4f, 0.4f, D2DPoint.Zero);
            }

            _guidance = GetGuidance(target);
            _decoyDistractArm.Start();
        }

        public override void Update(float dt, D2DSize viewport, float renderScale)
        {
            base.Update(dt, viewport, renderScale * this.RenderOffset);

            _age += dt;

            if (_age > LIFESPAN && MissedTarget)
                this.IsExpired = true;


            if (_useControlSurfaces)
            {
                _tailWing.Update(dt, viewport, renderScale * this.RenderOffset);
                _noseWing.Update(dt, viewport, renderScale * this.RenderOffset);
                _rocketBody.Update(dt, viewport, renderScale * this.RenderOffset);
            }
            else
            {
                _rocketBody.Update(dt, viewport, renderScale * this.RenderOffset);
            }

            _centerOfThrust.Update(dt, viewport, renderScale * this.RenderOffset);
            _warheadCenterMass.Update(dt, viewport, renderScale * this.RenderOffset);
            _motorCenterMass.Update(dt, viewport, renderScale * this.RenderOffset);
            _flamePos.Update(dt, viewport, renderScale * this.RenderOffset);

            float flameAngle = 0f;

            if (_useThrustVectoring)
            {
                flameAngle = GetThrust(_useThrustVectoring).Angle();
            }
            else
            {
                const float DEF_AMT = 0.2f; // How much the flame will be deflected in relation to velocity.
                flameAngle = this.Rotation - (Helpers.ClampAngle180(this.Rotation - this.Velocity.Angle(true)) * DEF_AMT);
            }

            // Make the flame do flamey things...(Wiggle and color)
            var thrust = GetThrust().Length();
            var len = this.Velocity.Length() * 0.05f;
            len += thrust * 0.01f;
            len *= 0.8f;
            FlamePoly.SourcePoly[1].X = -_rnd.NextFloat(9f + len, 11f + len);
            _flameFillColor.g = _rnd.NextFloat(0.6f, 0.86f);

            FlamePoly.Update(_flamePos.Position, flameAngle, renderScale * this.RenderOffset);

            if (this.IsNetObject)
                return;


            D2DPoint accel = D2DPoint.Zero;

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
                var torqueRot = (tailTorque + bodyTorque + noseTorque + thrustTorque) * dt;

                this.RotationSpeed += torqueRot / this.TotalMass;
            }
            else
            {
                var bodyForce = _rocketBody.GetLiftDragForce();
                liftDrag += bodyForce;
            }

            // Apply guidance.
            var guideRotation = _guidance.GuideTo(dt);

            if (_useControlSurfaces)
            {
                const float TAIL_AUTH = 1f;
                const float NOSE_AUTH = 0f;

                // Compute deflection.
                var veloAngle = this.Velocity.Angle(true);
                var nextDeflect = Helpers.ClampAngle180(guideRotation - veloAngle);

                // Adjust the deflection as speed, rotation speed and AoA increases.
                // This is to try to prevent over-rotation caused by thrust vectoring.
                if (_currentFuel > 0f && _useThrustVectoring)
                {
                    const float MIN_DEF_SPD = 300f;//450f; // Minimum speed required for full deflection.
                    var spdFact = Helpers.Factor(this.Velocity.Length(), MIN_DEF_SPD);

                    const float MAX_DEF_AOA = 20f;// Maximum AoA allowed. Reduce deflection as AoA increases.
                    var aoaFact = 1f - (Math.Abs(_rocketBody.AoA) / (MAX_DEF_AOA + (spdFact * (MAX_DEF_AOA * 2f))));

                    const float MAX_DEF_ROT_SPD = 200f; // Maximum rotation speed allowed. Reduce deflection to try to control rotation speed.
                    var rotSpdFact = 1f - (Math.Abs(this.RotationSpeed) / (MAX_DEF_ROT_SPD + (spdFact * (MAX_DEF_ROT_SPD * 3f))));

                    nextDeflect *= aoaFact * rotSpdFact * spdFact;
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
            accel += GetThrust(thrustVector: false) * dt / TotalMass;
            accel += (liftDrag / TotalMass) * dt;

            this.Velocity += accel;
            this.Velocity += World.Gravity * dt;

            var gforce = accel.Length() / dt / 9.8f;
            _gForce = gforce;
            _gForcePeak = Math.Max(_gForcePeak, _gForce);

            if (_currentFuel > 0f)
            {
                _currentFuel -= BURN_RATE * dt;
            }

            //base.Update(dt, viewport, renderScale * this.RenderOffset);


            if (FUEL <= 0f && this.Velocity.Length() <= 5f)
                this.IsExpired = true;

            if (Target.IsExpired && _age > LIFESPAN)
                this.IsExpired = true;

            _decoyDistractCooldown.Update(dt);
            _decoyDistractArm.Update(dt);
        }

        public override void NetUpdate(float dt, D2DSize viewport, float renderScale, D2DPoint position, D2DPoint velocity, float rotation, double frameTime)
        {
            base.NetUpdate(dt, viewport, renderScale, position, velocity, rotation, frameTime);

            _tailWing.Deflection = this.Deflection;

            if (_useControlSurfaces)
            {
                _tailWing.Update(dt, viewport, renderScale * this.RenderOffset);
                _noseWing.Update(dt, viewport, renderScale * this.RenderOffset);
                _rocketBody.Update(dt, viewport, renderScale * this.RenderOffset);
            }
            else
            {
                _rocketBody.Update(dt, viewport, renderScale * this.RenderOffset);
            }

            _centerOfThrust.Update(dt, viewport, renderScale * this.RenderOffset);
            _warheadCenterMass.Update(dt, viewport, renderScale * this.RenderOffset);
            _motorCenterMass.Update(dt, viewport, renderScale * this.RenderOffset);
            _flamePos.Update(dt, viewport, renderScale * this.RenderOffset);

            float flameAngle = 0f;

            if (_useThrustVectoring)
            {
                flameAngle = GetThrust(_useThrustVectoring).Angle();
            }
            else
            {
                const float DEF_AMT = 0.2f; // How much the flame will be deflected in relation to velocity.
                flameAngle = this.Rotation - (Helpers.ClampAngle180(this.Rotation - this.Velocity.Angle(true)) * DEF_AMT);
            }

            var thrust = GetThrust().Length();
            var len = this.Velocity.Length() * 0.05f;
            len += thrust * 0.01f;
            len *= 0.8f;
            FlamePoly.SourcePoly[1].X = -_rnd.NextFloat(9f + len, 11f + len);
            _flameFillColor.g = _rnd.NextFloat(0.6f, 0.86f);

            FlamePoly.Update(_flamePos.Position, flameAngle, renderScale * this.RenderOffset);
            this.Polygon.Update(this.Position, this.Rotation, renderScale * this.RenderOffset);

            if (FUEL <= 0f && this.Velocity.Length() <= 5f)
                this.IsExpired = true;

            if (Target.IsExpired && _age > LIFESPAN)
                this.IsExpired = true;
        }

        public void ChangeTarget(GameObject target)
        {
            this.Target = target;

            _guidance = GetGuidance(target);
        }

        public void DoChangeTargetChance(GameObject target)
        {
            if (_decoyDistractCooldown.IsRunning || _decoyDistractArm.IsRunning)
                return;

            const int RANDO_AMT = 3;//5;
            var randOChanceO = _rnd.Next(RANDO_AMT);
            var randOChanceO2 = _rnd.Next(RANDO_AMT);
            var lucky = randOChanceO == randOChanceO2; // :-}

            if (lucky)
            {
                ChangeTarget(target);
                _decoyDistractCooldown.Reset();
                _decoyDistractCooldown.Start();
                IsDistracted = true;
                Log.Msg("Missile distracted!");

            }
            else
            {
                _decoyDistractCooldown.Reset();
                _decoyDistractCooldown.Start();
                Log.Msg("Nice try!");
            }
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

        public override void Wrap(D2DSize viewport)
        {
            //var padding = 2000f;

            //if (this.Position.X < -padding)
            //    IsExpired = true;

            //if (this.Position.X > viewport.width + padding)
            //    IsExpired = true;

            //if (this.Position.Y < -padding)
            //    IsExpired = true;

            //if (this.Position.Y > viewport.height + padding)
            //    IsExpired = true;
        }

        public override void Render(RenderContext ctx)
        {
            //gfx.DrawLine(this.Position, this.Position + (AngleToVector(this.Rotation) * 50f), D2DColor.White);
            //gfx.DrawLine(this.Position, this.Position + (this.Velocity * 1f), D2DColor.Blue);

            if (_useThrustVectoring)
                _flameFillColor = D2DColor.Orange;

            if (_currentFuel > 0f)
                ctx.DrawPolygon(this.FlamePoly.Poly, _flameFillColor, 1f, D2DDashStyle.Solid, _flameFillColor);

            var fillColor = D2DColor.White;

            //if (_guidance.MissedTarget)
            //    fillColor = D2DColor.Orange;

            //if (Target is Decoy)
            //    fillColor = D2DColor.Yellow;


            ctx.DrawPolygon(this.Polygon.Poly, D2DColor.White, 0.5f, D2DDashStyle.Solid, fillColor);

            //DrawFuelGauge(gfx);

            if (_useControlSurfaces)
            {
                _tailWing.Render(ctx);
                _noseWing.Render(ctx);

                //var totLift = _tailWing.LiftVector + _noseWing.LiftVector + _rocketBody.LiftVector;
                //gfx.DrawLine(this.Position, this.Position + (totLift * 1.4f), D2DColor.SkyBlue, 0.5f, D2DDashStyle.Solid, D2DCapStyle.Flat, D2DCapStyle.Triangle);
            }

            //_rocketBody.Render(gfx);

            if (World.ShowTracking)
            {
                ctx.FillEllipse(new D2DEllipse(_guidance.CurrentAimPoint, new D2DSize(5f, 5f)), D2DColor.LawnGreen);
                ctx.FillEllipse(new D2DEllipse(_guidance.StableAimPoint, new D2DSize(4f, 4f)), D2DColor.Blue);
                ctx.FillEllipse(new D2DEllipse(_guidance.ImpactPoint, new D2DSize(3f, 3f)), D2DColor.Red);
            }

            //this.DrawVeloLines(ctx.Gfx);
        }

        private void DrawFOVCone(D2DGraphics gfx)
        {
            const float LEN = 20000f;
            const float FOV = 40f;
            var color = D2DColor.Red;

            var centerLine = Helpers.AngleToVectorDegrees(this.Rotation, LEN);
            var cone1 = Helpers.AngleToVectorDegrees(this.Rotation + (FOV * 0.5f), LEN);
            var cone2 = Helpers.AngleToVectorDegrees(this.Rotation - (FOV * 0.5f), LEN);


            gfx.DrawLine(this.Position, this.Position + cone1, D2DColor.Red);
            gfx.DrawLine(this.Position, this.Position + cone2, D2DColor.Blue);
        }

        private float GetTorque(Wing wing, D2DPoint force)
        {
            return GetTorque(wing.Position, force);
        }

        private float GetTorque(D2DPoint pos, D2DPoint force)
        {
            // How is it so simple?
            var r = pos - GetCenterOfGravity();

            var torque = Helpers.Cross(r, force);
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

            if (_currentFuel > 0f)
            {
                D2DPoint vec;

                if (thrustVector)
                    vec = AngleToVector(this.Rotation + (_tailWing.Deflection * THURST_VECTOR_AMT));
                else
                    vec = AngleToVector(this.Rotation);

                vec *= THRUST;

                thrust = vec;
            }

            return thrust;
        }
    }
}
