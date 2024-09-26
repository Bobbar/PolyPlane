using PolyPlane.GameObjects.Interfaces;
using PolyPlane.Helpers;
using PolyPlane.Rendering;
using unvell.D2DLib;

namespace PolyPlane.GameObjects.Fixtures
{
    public class Wing : GameObject, INoGameID
    {
        public WingParameters Parameters => _params;

        public float Deflection
        {
            get { return _defRateLimit.Value; }
            set
            {
                if (value >= -_maxDeflection && value <= _maxDeflection)
                    _defRateLimit.Target = value;
                else
                    _defRateLimit.Target = Math.Sign(value) * _maxDeflection;
            }
        }

        public D2DPoint LiftVector { get; set; }
        public D2DPoint DragVector { get; set; }
        public float AoA { get; set; }

        public FixturePoint FixedPosition;
        public FixturePoint PivotPoint;

        private GameObject _parentObject;
        private RateLimiter _defRateLimit = new RateLimiter(rate: 80f);

        private float _maxDeflection = 40f;

        private WingParameters _params;

        public Wing(GameObject obj, WingParameters parameters)
        {
            _params = parameters;

            if (_params.PivotPoint == D2DPoint.Zero)
                _params.PivotPoint = _params.Position;

            PivotPoint = new FixturePoint(obj, _params.PivotPoint, copyRotation: false);
            FixedPosition = new FixturePoint(PivotPoint, _params.Position - _params.PivotPoint);

            _defRateLimit = new RateLimiter(rate: _params.DeflectionRate);
            _maxDeflection = _params.MaxDeflection;
            Rotation = obj.Rotation;
            _parentObject = obj;

            if (_params.MaxDragForce == 0f)
                _params.MaxDragForce = _params.MaxLiftForce;

            this.Update(0f);
        }

        public override void Update(float dt)
        {
            PivotPoint.Rotation = _parentObject.Rotation + Deflection;

            _defRateLimit.Update(dt);
            PivotPoint.Update(dt);
            FixedPosition.Update(dt);

            Rotation = PivotPoint.Rotation;
            Position = FixedPosition.Position;

            var nextVelo = Utilities.AngularVelocity(_parentObject, Position, dt);
            Velocity = nextVelo;
        }

        public override void FlipY()
        {
            base.FlipY();
            PivotPoint.FlipY();
            FixedPosition.FlipY();
        }

        public override void Render(RenderContext ctx)
        {
            base.Render(ctx);

            //// Draw a fixed box behind the moving wing. Helps to visualize deflection.
            //var fixedVec = Utilities.AngleToVectorDegrees(this.Rotation - this.Deflection);
            //var startB = this.Position - fixedVec * RenderLength;
            //var endB = this.Position + fixedVec * RenderLength;
            //ctx.DrawLine(startB, endB, D2DColor.DarkGray, 2f);

            ////// Draw wing without rate limit.
            //var wingVecRaw = Utilities.AngleToVectorDegrees(_parentObject.Rotation + _defRateLimit.Target);
            //var startRaw = this.Position - wingVecRaw * RenderLength;
            //var end2Raw = this.Position + wingVecRaw * RenderLength;
            //gfx.DrawLine(startRaw, end2Raw, D2DColor.Red, 1f, D2DDashStyle.Solid, D2DCapStyle.Round, D2DCapStyle.Round);

            if (Visible)
            {
                // Draw wing.
                var wingVec = Utilities.AngleToVectorDegrees(Rotation, _params.RenderLength);
                var start = Position - wingVec;
                var end = Position + wingVec;
                ctx.DrawLine(start, end, D2DColor.Black, _params.RenderWidth + 0.5f, D2DDashStyle.Solid, D2DCapStyle.Triangle, D2DCapStyle.Triangle);
                ctx.DrawLine(start, end, D2DColor.Gray, _params.RenderWidth, D2DDashStyle.Solid, D2DCapStyle.Triangle, D2DCapStyle.Triangle);
            }

            if (World.ShowAero)
            {
                const float SCALE = 0.1f;//0.04f;
                const float AERO_WEIGHT = 2f;
                ctx.DrawLine(Position, Position + LiftVector * SCALE, D2DColor.SkyBlue, AERO_WEIGHT, D2DDashStyle.Solid, D2DCapStyle.Flat, D2DCapStyle.Triangle);
                ctx.DrawLine(Position, Position + DragVector * (SCALE + 0.03f), D2DColor.Red, AERO_WEIGHT, D2DDashStyle.Solid, D2DCapStyle.Flat, D2DCapStyle.Triangle);

                var aggForce = (LiftVector + DragVector) * 0.5f;
                ctx.DrawLine(Position, Position + aggForce * (SCALE + 0.03f), D2DColor.Yellow, AERO_WEIGHT, D2DDashStyle.Solid, D2DCapStyle.Flat, D2DCapStyle.Triangle);

                ctx.DrawLine(Position, Position + Velocity * (SCALE + 0.5f), D2DColor.Green, AERO_WEIGHT, D2DDashStyle.Solid, D2DCapStyle.Flat, D2DCapStyle.Triangle);
            }
        }

        public D2DPoint GetLiftDragForce()
        {
            if (Velocity.Length() == 0f)
                return D2DPoint.Zero;

            // Wing & air parameters.
            float AOA_FACT = _params.AOAFactor; // How much AoA effects drag.
            float VELO_FACT = _params.VeloFactor; // How much velocity effects drag.
            float WING_AREA = _params.Area; // Area of the wing. Effects lift & drag forces.
            float MAX_LIFT = _params.MaxLiftForce; // Max lift force allowed.
            float MAX_DRAG = _params.MaxDragForce; // Max drag force allowed.
            float MAX_AOA = _params.MaxAOA; // Max AoA allowed before lift force reduces. (Stall)
            float AIR_DENSITY = World.GetDensityAltitude(Position);
            float PARASITIC_DRAG = _params.ParasiticDrag;
            float MIN_VELO = _params.MinVelo;

            var velo = Velocity;
            velo += -World.Wind;

            // Lerp in turbulence as altitude changes.
            // Greater altitude = less turbulence.
            velo = World.GetTurbulenceVeloAltitude(this.Position, velo);

            var veloMag = velo.Length();
            var veloMagSq = Math.Pow(veloMag, 2f);
            var veloAngle = Velocity.Angle();

            // Compute velo tangent.
            var veloNorm = D2DPoint.Normalize(velo);
            var veloNormTan = veloNorm.Tangent();

            // Compute angle of attack.
            var aoaDegrees = Utilities.ClampAngle180(veloAngle - Rotation);
            var aoaRads = Utilities.DegreesToRads(aoaDegrees);

            // Reduce velo as we approach the minimum. (Increases stall effect)
            var veloFact = Utilities.FactorWithEasing(veloMag, MIN_VELO, EasingFunctions.EaseOutSine);
            veloMag *= veloFact;
            veloMagSq *= veloFact;

            // Drag force.
            var coeffDrag = 1f - Math.Cos(2f * aoaRads);
            var dragForce = coeffDrag * AOA_FACT * WING_AREA * 0.5f * AIR_DENSITY * veloMagSq * VELO_FACT;
            dragForce += veloMag * (WING_AREA * PARASITIC_DRAG);

            // Factor for max AoA.
            // Clamp AoA to always allow a little bit a of lift.
            var aoaFact = Utilities.FactorWithEasing(MAX_AOA, Math.Abs(aoaDegrees), EasingFunctions.EaseOutSine);
            aoaFact = Math.Clamp(aoaFact, 0.1f, 1f);

            // Lift force.
            var coeffLift = Math.Sin(2f * aoaRads) * aoaFact;
            var liftForce = AIR_DENSITY * 0.5f * veloMagSq * WING_AREA * coeffLift;

            // Clamp to max lift & drag force.
            liftForce = Math.Clamp(liftForce, -MAX_LIFT, MAX_LIFT);
            dragForce = Math.Clamp(dragForce, -MAX_DRAG, MAX_DRAG);

            // Compute the final force vectors.
            var dragVec = -veloNorm * (float)dragForce;
            var liftVec = veloNormTan * (float)liftForce;

            LiftVector = liftVec;
            DragVector = dragVec;
            AoA = aoaDegrees;

            return liftVec + dragVec;
        }
    }

    public class WingParameters
    {
        /// <summary>
        /// Attachment point.
        /// </summary>
        public D2DPoint Position = D2DPoint.Zero;

        /// <summary>
        /// Position around which the wing will rotate.
        /// </summary>
        public D2DPoint PivotPoint = D2DPoint.Zero;

        /// <summary>
        /// Render length.
        /// </summary>
        public float RenderLength;

        /// <summary>
        /// Render stroke width.
        /// </summary>
        public float RenderWidth = 2f;

        /// <summary>
        /// Wing area.
        /// </summary>
        public float Area;

        /// <summary>
        /// Max lift force allowed.
        /// </summary>
        public float MaxLiftForce = 15000f;

        /// <summary>
        /// Max drag force allowed.
        /// </summary>
        public float MaxDragForce;

        /// <summary>
        /// Max deflection allowed.
        /// </summary>
        public float MaxDeflection = 40f;

        /// <summary>
        /// Deflection rate. (How fast does it rotate to the target deflection)
        /// </summary>
        public float DeflectionRate = 80f;

        /// <summary>
        /// Minimum velocity before stall.
        /// </summary>
        public float MinVelo = 350f;

        /// <summary>
        /// Maximum AoA before stall.
        /// </summary>
        public float MaxAOA = 30f;

        /// <summary>
        /// How much AoA effects drag.
        /// </summary>
        public float AOAFactor = 0.5f;

        /// <summary>
        /// How much velocity effects drag.
        /// </summary>
        public float VeloFactor = 0.5f;

        /// <summary>
        /// Additional drag applied regardless of AoA.
        /// </summary>
        public float ParasiticDrag = 1f;

        public WingParameters() { }

    }
}
