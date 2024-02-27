﻿using System.Diagnostics;

namespace PolyPlane.GameObjects.Guidance
{
    public abstract class GuidanceBase
    {
        public D2DPoint ImpactPoint { get; set; }
        public D2DPoint StableAimPoint { get; set; }
        public D2DPoint CurrentAimPoint { get; set; }

        protected Missile Missile { get; set; }
        protected GameObject Target { get; set; }

        private GameTimer _lostLockTimer = new GameTimer(2f);
        private GameTimer _groundScatterTimer = new GameTimer(2f);

        //private bool _lostLock = false;

        public bool MissedTarget => _missedTarget || _lostInGround;
        public bool _missedTarget = false;
        private bool _lostInGround = false;
        private float _prevTargDist = 0f;
        private float _reEngageMod = 0f;
        private float _missDistTraveled = 0f;
        private float _missDirection = 0f; // O.o

        private readonly float MISS_TARG_DIST = 400f; // Distance to be considered a miss when the closing rate goes negative.
        private readonly float REENGAGE_DIST = 1500f; // How far we must be from the target before re-engaging after a miss.
        private readonly float ARM_DIST = 1200f;

        protected GuidanceBase(Missile missile, GameObject target)
        {
            Missile = missile;
            Target = target;
            _lostLockTimer.TriggerCallback = () =>
            _missedTarget = true;
        }

        public float GuideTo(float dt)
        {
            _lostLockTimer.Update(dt);
            _groundScatterTimer.Update(dt);

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
                if (!_missedTarget && !_lostLockTimer.IsRunning)
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
                        Debug.WriteLine("Lost in ground scatter....");
                    }

                }
            }
            else
                _lostInGround = false;


            if (_missedTarget || _lostInGround)
                rotation = Missile.Rotation;

            if (float.IsNaN(rotation))
                Debugger.Break();

            //if (!_missedTarget && !isInFOV && Missile.DistTraveled > 1000f)
            //{
            //    Debug.WriteLine("Target lost!");
            //    _missedTarget = true;
            //}

            //if (_missedTarget && isInFOV && Missile.DistTraveled > 1000f)
            //{
            //    Debug.WriteLine("Target re-acquired!");
            //    _missedTarget = false;
            //}


            //// Compute closing rate and detect when we miss the target.
            //var targDist = D2DPoint.Distance(Missile.Position, Target.Position);
            //var closingRate = _prevTargDist - targDist;
            //_prevTargDist = targDist;

            //if (closingRate < 0.1f)
            //{
            //    if (!_missedTarget && targDist < MISS_TARG_DIST && Missile.DistTraveled > ARM_DIST)
            //    {
            //        if (World.ExpireMissilesOnMiss)
            //            this.Missile.IsExpired = true;

            //        _missedTarget = true;
            //        _missDistTraveled = Missile.DistTraveled;
            //        _reEngageMod += REENGAGE_DIST * 0.2f;
            //        _missDirection = Missile.Rotation;
            //    }
            //}

            //// Reduce the rotation amount to fly a straighter course until
            //// we are the specified distance away from the target.
            //var missDist = Missile.DistTraveled - _missDistTraveled;

            //if (_missedTarget)
            //{
            //    var reengageDist = REENGAGE_DIST + _reEngageMod;
            //    rotFactor = Helpers.Factor(missDist, reengageDist);
            //    initialAngle = _missDirection;
            //}

            //if (_missedTarget && missDist >= REENGAGE_DIST + _reEngageMod)
            //    _missedTarget = false;

            // Lerp from current rotation towards guidance rotation as we 
            // approach the specified arm distance.
            var armFactor = Helpers.Factor(Missile.DistTraveled, ARM_DIST);

            var finalRot = Helpers.LerpAngle(initialAngle, rotation, rotFactor * armFactor);

            return finalRot;
        }

        protected D2DPoint GetTargetPosition()
        {
            if (this.Target is Plane plane)
                return plane.ExhaustPosition;
            else
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
