namespace PolyPlane.GameObjects
{
    public class GameTimer
    {
        public float Interval { get; set; }
        public bool IsRunning => _isRunning;
        public bool AutoRestart { get; set; } = false;
        public float Value => _current;

        public Action TriggerCallback { get; set; }

        private float _current = 0f;
        private bool _hasFired = false;
        private bool _isRunning = false;

        public GameTimer(float interval, Action callback)
        {
            Interval = interval;
            TriggerCallback = callback;
        }

        public GameTimer(float interval)
        {
            Interval = interval;
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

                if (AutoRestart)
                {
                    Restart();
                }
            }
        }

        public void Start(bool fireTriggerOnStart = false)
        {
            _isRunning = true;

            if (fireTriggerOnStart)
                if (TriggerCallback != null)
                    TriggerCallback();
        }

        public void Stop()
        {
            _isRunning = false;
        }

        public void Reset()
        {
            _current = 0f;
            _hasFired = false;
        }

        public void Restart()
        {
            Reset();
            Start();
        }

    }
}
