using PolyPlane.Helpers;
using PolyPlane.Rendering;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public class Wing : GameObject
    {
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

        private FixturePoint FixedPosition;
        private D2DPoint _prevPosition;
        private GameObject _parentObject;
        private RateLimiter _defRateLimit = new RateLimiter(rate: 80f);

        private float _maxDeflection = 40f;
        private bool _wrapped = false;

        private WingParameters _params;

        public Wing(GameObject obj, WingParameters parameters)
        {
            _params = parameters;
            FixedPosition = new FixturePoint(obj, _params.Position);
            _defRateLimit = new RateLimiter(rate: _params.DeflectionRate);
            _maxDeflection = _params.MaxDeflection;
            Rotation = obj.Rotation;
           _parentObject = obj;

            if (_params.MaxDragForce == 0f)
                _params.MaxDragForce = _params.MaxLiftForce;
        }

        public override void Update(float dt, float renderScale)
        {
            _defRateLimit.Update(dt);
            FixedPosition.Update(dt, renderScale);

            this.Rotation = _parentObject.Rotation + this.Deflection;
            this.Position = FixedPosition.Position;

            if (_wrapped)
            {
                _prevPosition = this.Position;
                _wrapped = false;
            }

            var nextVelo = D2DPoint.Zero;

            if (_prevPosition != D2DPoint.Zero)
                nextVelo = (this.Position - _prevPosition) / dt;
            else
                _prevPosition = this.Position;


            _prevPosition = this.Position;

            this.Velocity = nextVelo;
        }

        public void Reset(D2DPoint pos)
        {
            _wrapped = true;
            this.Position = pos;
            _prevPosition = this.Position;
            this.Velocity = D2DPoint.Zero;
        }

        public void FlipY()
        {
            FixedPosition.FlipY();
            Reset(this.Position);
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

            // Draw wing.
            const float WEIGHT = 2f;//1f;
            var wingVec = Utilities.AngleToVectorDegrees(this.Rotation);
            var start = this.Position - wingVec * _params.RenderLength;
            var end = this.Position + wingVec * _params.RenderLength;
            ctx.DrawLine(start, end, D2DColor.Black, WEIGHT + 0.5f, D2DDashStyle.Solid, D2DCapStyle.Round, D2DCapStyle.Round);
            ctx.DrawLine(start, end, D2DColor.Gray, WEIGHT, D2DDashStyle.Solid, D2DCapStyle.Round, D2DCapStyle.Round);

            if (World.ShowAero)
            {
                const float SCALE = 0.1f;//0.04f;
                ctx.DrawLine(this.Position, this.Position + (LiftVector * SCALE), D2DColor.SkyBlue, 2f, D2DDashStyle.Solid, D2DCapStyle.Flat, D2DCapStyle.Triangle);
                ctx.DrawLine(this.Position, this.Position + (DragVector * (SCALE + 0.03f)), D2DColor.Red, 2f, D2DDashStyle.Solid, D2DCapStyle.Flat, D2DCapStyle.Triangle);
            }

            //gfx.DrawLine(this.Position, this.Position + (this.Velocity * 0.1f), D2DColor.GreenYellow, 0.5f);
        }

        public D2DPoint GetLiftDragForce()
        {
            if (this.Velocity.Length() == 0f)
                return D2DPoint.Zero;

            var velo = -World.Wind;

            velo += this.Velocity;

            var veloMag = velo.Length();
            var veloMagSq = (float)Math.Pow(veloMag, 2f);

            float MIN_VELO = _params.MinVelo;

            var veloFact = Utilities.Factor(veloMag, MIN_VELO);

            // Compute velo tangent. For lift/drag and rotation calcs.
            var veloNorm = D2DPoint.Normalize(velo);
            var veloNormTan = new D2DPoint(veloNorm.Y, -veloNorm.X);

            // Compute angle of attack.
            var aoaRads = Utilities.AngleToVectorDegrees(this.Rotation).Cross(veloNorm);
            var aoa = Utilities.RadsToDegrees(aoaRads);

            // Compute lift force as velocity tangent with angle-of-attack effecting magnitude and direction. Velocity magnitude is factored as well.
            // Greater AoA and greater velocity = more lift force.

            // Wing & air parameters.
            float AOA_FACT = _params.AOAFactor; // How much AoA effects drag.
            float VELO_FACT = _params.VeloFactor; // How much velocity effects drag.
            float WING_AREA = _params.Area; // Area of the wing. Effects lift & drag forces.
            float MAX_LIFT = _params.MaxLiftForce; // Max lift force allowed.
            float MAX_DRAG = _params.MaxDragForce; // Max drag force allowed.
            float MAX_AOA = _params.MaxAOA; // Max AoA allowed before lift force reduces. (Stall)
            float AIR_DENSITY = World.GetDensityAltitude(this.Position);
            float PARASITIC_DRAG = _params.ParasiticDrag;

            // Reduce velo as we approach the minimum. (Increases stall effect)
            veloMag *= veloFact;
            veloMagSq *= veloFact;

            // Drag force.
            var coeffDrag = 1f - (float)Math.Cos(2f * aoaRads);
            var dragForce = coeffDrag * AOA_FACT * WING_AREA * 0.5f * AIR_DENSITY * veloMagSq * VELO_FACT;
            dragForce += veloMag * (WING_AREA * PARASITIC_DRAG);

            // Lift force.
            var aoaFact = Utilities.Factor(MAX_AOA, Math.Abs(aoa));
            var coeffLift = (float)Math.Sin(2f * aoaRads) * aoaFact;
            var liftForce = AIR_DENSITY * 0.5f * veloMagSq * WING_AREA * coeffLift;

            liftForce = Math.Clamp(liftForce, -MAX_LIFT, MAX_LIFT);
            dragForce = Math.Clamp(dragForce, -MAX_DRAG, MAX_DRAG);

            var dragVec = -veloNorm * dragForce;
            var liftVec = veloNormTan * liftForce;

            this.LiftVector = liftVec;
            this.DragVector = dragVec;
            this.AoA = aoa;

            return (liftVec + dragVec);
        }
    }

    public class WingParameters
    {
        public D2DPoint Position = D2DPoint.Zero;
        public float RenderLength;
        public float Area;
        public float MaxLiftForce = 15000f;
        public float MaxDragForce;
        public float MaxDeflection = 40f;
        public float DeflectionRate = 80f;
        public float MinVelo = 350f;
        public float MaxAOA = 30f;
        public float AOAFactor = 0.5f;
        public float VeloFactor = 0.5f;
        public float ParasiticDrag = 1f;

        public WingParameters() { }
            
    }
}
