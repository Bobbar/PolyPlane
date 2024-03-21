using System.Diagnostics;

namespace PolyPlane.GameObjects.Guidance
{
    public abstract class GuidanceBase
    {
        public D2DPoint ImpactPoint { get; set; }
        public D2DPoint StableAimPoint { get; set; }
        public D2DPoint CurrentAimPoint { get; set; }

        protected Missile Missile { get; set; }
        protected GameObject Target { get; set; }

        private const float ARM_TIME = 3f;

        private GameTimer _lostLockTimer = new GameTimer(2f);
        private GameTimer _groundScatterTimer = new GameTimer(2f);
        private GameTimer _armTimer = new GameTimer(ARM_TIME);

        public bool MissedTarget => _missedTarget || _lostInGround;
        public bool _missedTarget = false;
        private bool _lostInGround = false;

        protected GuidanceBase(Missile missile, GameObject target)
        {
            Missile = missile;
            Target = target;
            _lostLockTimer.TriggerCallback = () => _missedTarget = true;

            _armTimer.Start();
        }

        public float GuideTo(float dt)
        {
            _lostLockTimer.Update(dt);
            _groundScatterTimer.Update(dt);
            _armTimer.Update(dt);

            // The guidance logic doesn't work when velo is zero (or very close).
            // Always return the current rotation if we aren't moving yet.
            if (Missile.Velocity.Length() == 0f)
                return Missile.Rotation;

            var rotFactor = 1f;
            var veloAngle = Missile.Velocity.Angle(true);
            var initialAngle = veloAngle;

            // Get rotation from implementation.
            var rotation = GetGuidanceDirection(dt);

            var isInFOV = Missile.IsObjInFOV(Target, World.SENSOR_FOV);

            if (!isInFOV)
            {
                if (!_missedTarget && !_lostLockTimer.IsRunning && !_armTimer.IsRunning)
                    _lostLockTimer.Restart();
            }
            else
            {
                if (_missedTarget || _lostLockTimer.IsRunning)
                {
                    _lostLockTimer.Stop();
                    _missedTarget = false;
                }
            }


            if (Target.Altitude <= 3000f)
            {
                const int CHANCE_INIT = 10;
                var chance = CHANCE_INIT;

                var altFact = Helpers.Factor(Target.Altitude, 3000f);

                chance -= (int)(altFact * 5);

                if (!_groundScatterTimer.IsRunning)
                {
                    _groundScatterTimer.Restart();

                    var rnd1 = Helpers.Rnd.Next(chance);
                    var rnd2 = Helpers.Rnd.Next(chance);
                    if (rnd1 == rnd2)
                    {
                        _lostInGround = true;
                        _missedTarget = true;
                        Log.Msg("Lost in ground scatter....");
                    }

                }
            }
            else
                _lostInGround = false;


            if (_missedTarget || _lostInGround)
                rotation = Missile.Rotation;

            if (float.IsNaN(rotation))
                Debugger.Break();


            // Lerp from current rotation towards guidance rotation as we 
            // approach the specified arm time.
            var armFactor = Helpers.Factor(_armTimer.Value, _armTimer.Interval);
            var finalRot = Helpers.LerpAngle(initialAngle, rotation, rotFactor * armFactor);

            return finalRot;
        }

        protected D2DPoint GetTargetPosition()
        {
            //if (this.Target is Plane plane)
            //    return plane.ExhaustPosition;
            //else

            var pos = this.Target.Position;

            // Try to compensate for lag?
            if (this.Target.IsNetObject)
                pos = this.Target.Position + (this.Target.Velocity * (float)((World.CurrentTime() - this.Target.ClientCreateTime) / 1000f));

            return this.Target.Position;
        }

        /// <summary>
        /// Implement guidance, dummy!
        /// </summary>
        /// <param name="dt"></param>
        /// <returns></returns>
        public abstract float GetGuidanceDirection(float dt);


    }
}
