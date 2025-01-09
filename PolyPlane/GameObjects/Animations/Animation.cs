namespace PolyPlane.GameObjects.Animations
{
    public abstract class Animation<T> : GameObject
    {
        public T StartValue;
        public T EndValue;

        public float Duration;
        public float AnimPostition;
        public Func<float, float> EaseFunc;

        public bool IsPlaying = false;
        public bool Loop = false;
        public bool ReverseOnLoop = false;
        public float Speed = 1.0f;
        protected Action<T> _setVal;

        protected bool _isReversed = false;
        protected float _elapsed = 0f;

        protected Animation() { }

        protected Animation(T start, T end, float duration, Func<float, float> easeFunc, Action<T> setValFunc)
        {
            StartValue = start;
            EndValue = end;
            Duration = duration;
            EaseFunc = easeFunc;
            _setVal = setValFunc;
        }

        public override void DoUpdate(float dt)
        {
            if (!IsPlaying)
                return;

            if (_isReversed)
                _elapsed -= dt;
            else
                _elapsed += dt;

            AnimPostition = Math.Clamp(_elapsed / Duration, 0f, 1f);

            if (_elapsed <= (Duration + dt) && _elapsed >= 0f)
            {
                var factor = EaseFunc(AnimPostition);
                DoStep(factor);
            }
            else
            {
                if (Loop)
                {
                    AnimPostition = 0f;

                    if (ReverseOnLoop)
                    {
                        _isReversed = !_isReversed;

                        if (!_isReversed)
                            _elapsed = 0f;
                    }
                }
                else
                {
                    Done();
                    IsPlaying = false;
                }
            }
        }

        public void Start()
        {
            IsPlaying = true;
        }

        public void Stop()
        {
            IsPlaying = false;
        }

        public void Restart()
        {
            IsPlaying = true;
            _elapsed = 0f;
            AnimPostition = 0f;
        }

        public void Reset()
        {
            _elapsed = 0f;
            AnimPostition = 0f;
        }

        public virtual void DoStep(float factor)
        {

        }

        public virtual void Done()
        {

        }

    }
}
