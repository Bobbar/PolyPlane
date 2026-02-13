namespace PolyPlane.Helpers
{
    public sealed class RandomVariationFloat
    {
        private float _curTime = 0f;
        private float _nextTime = 0f;
        private float _target = 0f;
        private float _prevTarget = 0f;
        private float _minTime = 0f;
        private float _maxTime = 0f;
        private float _minValue = 0f;
        private float _maxValue = 0f;

        public float Value { get; private set; } = 0f;

        public RandomVariationFloat(float minValue, float maxValue, float minTime, float maxTime)
        {
            _minValue = minValue;
            _maxValue = maxValue;
            _minTime = minTime;
            _maxTime = maxTime;

            _target = Utilities.Rnd.NextFloat(_minValue, _maxValue);
            _prevTarget = _target;
            Value = _target;
            _nextTime = Utilities.Rnd.NextFloat(_minTime, _maxTime);
        }

        public void Update(float dt)
        {
            if (_curTime >= _nextTime)
            {
                _target = Utilities.Rnd.NextFloat(_minValue, _maxValue);
                _nextTime = Utilities.Rnd.NextFloat(_minTime, _maxTime);
                _curTime = 0f;
                _prevTarget = Value;
            }

            Value = Utilities.Lerp(_prevTarget, _target, Utilities.Factor(_curTime, _nextTime));

            _curTime += dt;
        }
    }

    public sealed class RandomVariationPoint
    {
        private float _curTime = 0f;
        private float _nextTime = 0f;
        private D2DPoint _target = D2DPoint.Zero;
        private D2DPoint _prevTarget = D2DPoint.Zero;
        private float _minTime = 0f;
        private float _maxTime = 0f;
        private float _minValue = 0f;
        private float _maxValue = 0f;

        public D2DPoint Value { get; private set; } = D2DPoint.Zero;


        public RandomVariationPoint(float minMaxValue, float minTime, float maxTime)
        {
            _minValue = -minMaxValue;
            _maxValue = minMaxValue;
            _minTime = minTime;
            _maxTime = maxTime;

            _target = new D2DPoint(Utilities.Rnd.NextFloat(_minValue, _maxValue), Utilities.Rnd.NextFloat(_minValue, _maxValue));
            _prevTarget = _target;
            Value = _target;
            _nextTime = Utilities.Rnd.NextFloat(_minTime, _maxTime);
        }

        public RandomVariationPoint(float minValue, float maxValue, float minTime, float maxTime)
        {
            _minValue = minValue;
            _maxValue = maxValue;
            _minTime = minTime;
            _maxTime = maxTime;

            _target = new D2DPoint(Utilities.Rnd.NextFloat(_minValue, _maxValue), Utilities.Rnd.NextFloat(_minValue, _maxValue));
            _prevTarget = _target;
            Value = _target;
            _nextTime = Utilities.Rnd.NextFloat(_minTime, _maxTime);
        }

        public void Update(float dt)
        {
            if (_curTime >= _nextTime)
            {
                _target = new D2DPoint(Utilities.Rnd.NextFloat(_minValue, _maxValue), Utilities.Rnd.NextFloat(_minValue, _maxValue));
                _nextTime = Utilities.Rnd.NextFloat(_minTime, _maxTime);
                _curTime = 0f;
                _prevTarget = Value;
            }

            Value = Utilities.LerpPoints(_prevTarget, _target, Utilities.Factor(_curTime, _nextTime));

            _curTime += dt;
        }

        public void SetMinMaxValue(float minMax)
        {
            _minValue = -minMax;
            _maxValue = minMax;
        }
    }
}
