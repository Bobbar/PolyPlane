using PolyPlane.Helpers;

namespace PolyPlane.GameObjects.Tools
{
    public class GameTimer
    {
        public float Interval
        {
            get { return _interval; }

            set
            {
                if (value < 0f)
                    _interval = 0f;
                else
                    _interval = value;
            }
        }

        public float Cooldown { get; set; } = -1;
        public bool IsInCooldown => _isInCooldown;
        public bool IsRunning => _isRunning;
        public bool AutoRestart { get; set; } = false;

        /// <summary>
        /// True if <see cref="StartCallback"/> firing rate will be limited to the interval.  Otherwise it will always fire immediately.
        /// </summary>
        public bool RateLimitStartCallback { get; set; } = false;

        public float Value => _current;

        /// <summary>
        /// Current timer position as a percentage.
        /// </summary>
        public float Position => Utilities.Factor(_current, _interval);

        public Action StartCallback { get; set; }
        public Action CoolDownEndCallback { get; set; }
        public Action TriggerCallback { get; set; }

        private float _interval = 0f;
        private float _current = 0f;
        private float _currentCooldown = 0f;
        private float _elapSinceLastStart = 0f;

        private bool _hasFired = false;
        private bool _isRunning = false;
        private bool _isInCooldown = false;
        private bool _firstStart = true;

        public GameTimer(float interval)
        {
            Interval = interval;
        }

        public GameTimer(float interval, float coolDown)
        {
            Interval = interval;
            Cooldown = coolDown;
        }

        public GameTimer(float interval, float coolDown, bool autoRestart)
        {
            Interval = interval;
            Cooldown = coolDown;
            AutoRestart = autoRestart;
        }

        public GameTimer(float interval, bool autoRestart)
        {
            Interval = interval;
            AutoRestart = autoRestart;
        }

        public void Update(float dt)
        {
            _elapSinceLastStart += dt;

            if (Cooldown > 0f)
            {
                if (IsInCooldown)
                {
                    _currentCooldown += dt;

                    if (_currentCooldown >= Cooldown)
                    {
                        _currentCooldown = 0f;
                        _isInCooldown = false;

                        if (CoolDownEndCallback != null)
                            CoolDownEndCallback();
                    }
                }
            }

            if (!_isRunning)
                return;

            if (_current >= Interval && !_hasFired)
            {
                if (TriggerCallback != null)
                    TriggerCallback();

                _hasFired = true;

                if (Cooldown > 0f)
                    _isInCooldown = true;

                if (AutoRestart)
                    Reset();
                else
                    Stop();
            }

            if (_current < Interval)
                _current += dt;
        }

        public void Start()
        {
            if (!_isRunning)
            {
                if (StartCallback != null)
                {
                    // Don't allow start callbacks to be fired at a rate higher than the interval.
                    if (RateLimitStartCallback)
                    {
                        if (_firstStart || (_elapSinceLastStart > 0f && _elapSinceLastStart >= _interval))
                        {
                            _elapSinceLastStart = 0f;
                            StartCallback();
                            _isRunning = true;
                            _firstStart = false;
                        }
                    }
                    else
                    {
                        StartCallback();
                        _isRunning = true;
                    }
                }
                else
                {
                    _isRunning = true;
                }
            }
        }

        public void Stop()
        {
            _isRunning = false;
        }

        public void Reset()
        {
            _current = 0f;
            _currentCooldown = 0f;
            _isInCooldown = false;
            _hasFired = false;
        }

        public void Restart(bool ignoreCooldown = false)
        {
            if (Cooldown > 0f && !ignoreCooldown && _isInCooldown)
                return;

            Reset();
            Start();
        }

    }
}
