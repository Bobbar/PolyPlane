namespace PolyPlane.GameObjects
{
    public class GameTimer
    {
        public float Interval { get; set; }
        public float Cooldown { get; set; } = -1;
        public bool IsInCooldown => _isInCooldown;
        public bool IsRunning => _isRunning;
        public bool AutoRestart { get; set; } = false;
        public float Value => _current;

        public Action StartCallback { get; set; }
        public Action CoolDownEndCallback { get; set; }
        public Action TriggerCallback { get; set; }

        private float _current = 0f;
        private float _currentCooldown = 0f;

        private bool _hasFired = false;
        private bool _isRunning = false;
        private bool _isInCooldown = false;

        public GameTimer(float interval, Action callback)
        {
            Interval = interval;
            TriggerCallback = callback;
        }

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

        public GameTimer(float interval, bool autoRestart, Action callback)
        {
            Interval = interval;
            AutoRestart = autoRestart;
        }

        public void Update(float dt)
        {
            if (Cooldown > 0f)
            {
                if (IsInCooldown)
                {
                    _currentCooldown += dt;

                    if (_currentCooldown >= Cooldown)
                    {
                        _currentCooldown = 0f;
                        _isInCooldown = false;
                    }
                }
            }

            if (!_isRunning)
                return;

            if (_current < Interval)
                _current += dt;

            if (_current >= Interval && !_hasFired)
            {
                if (TriggerCallback != null)
                    TriggerCallback();

                _hasFired = true;
                Stop();

                if (Cooldown > 0f)
                    _isInCooldown = true;

                if (AutoRestart)
                {
                    Restart();
                }
            }
        }

        public void Start()
        {
            _isRunning = true;

            if (StartCallback != null)
                StartCallback();
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
