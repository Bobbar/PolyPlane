namespace PolyPlane.Helpers
{
    /// <summary>
    /// Provides basic rate limiting for a single value.
    /// </summary>
    public class RateLimiter
    {
        public float Target
        {
            get { return _target; }
            set { _target = value; }
        }

        public float Value => _current;

        private float _target = 0f;
        private float _current = 0f;
        private float _rate = 0f;

        public RateLimiter(float rate)
        {
            _rate = rate;
        }

        public void Update(float dt)
        {
            if (_current == _target)
                return;

            var diff = _target - _current;
            var sign = Math.Sign(diff);
            var amt = _rate * sign * dt;

            if (Math.Abs(amt) > Math.Abs(diff))
                amt = diff;

            _current += amt;
        }

        public void Set(float value)
        {
            _current = value;
            _target = value;
        }
    }


    /// <summary>
    /// Provides basic rate limiting for a single angle.
    /// </summary>
    public class RateLimiterAngle
    {
        public float Target
        {
            get { return _target; }
            set { _target = value; }
        }

        public float Value => _current;

        private float _target = 0f;
        private float _current = 0f;
        private float _rate = 0f;

        public RateLimiterAngle(float rate)
        {
            _rate = rate;
        }

        public void Update(float dt)
        {
            if (_current == _target)
                return;

            var diff = Utilities.ClampAngle180(_target - _current);
            var sign = Math.Sign(diff);
            var amt = _rate * sign * dt;

            if (Math.Abs(amt) > Math.Abs(diff))
                amt = diff;

            _current += amt;
        }

        public void Set(float value)
        {
            _current = value;
            _target = value;
        }
    }
}
