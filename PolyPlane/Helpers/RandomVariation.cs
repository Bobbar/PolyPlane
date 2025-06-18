namespace PolyPlane.Helpers
{
    public class RandomVariationFloat
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

    public class RandomVariationPoint
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

    public class RandomVariationVector
    {
        private float _curMagTime = 0f;
        private float _nextMagTime = 0f;

        private float _curDirTime = 0f;
        private float _nextDirTime = 0f;

        private float _targetMag = 0f;
        private float _currentMag = 0f;
        private float _prevTargetMag = 0f;

        private float _targetDir = 0f;
        private float _currentDir = 0f;
        private float _prevTargetDir = 0f;

        private float _minTime = 0f;
        private float _maxTime = 0f;
        private float _minMag = 0f;
        private float _maxMag = 0f;

        public D2DPoint Value
        {
            get
            {
                var vec = Utilities.AngleToVectorDegrees(_currentDir) * _currentMag;
                return vec;
            }
        }

        public RandomVariationVector(float minMaxMagnitude, float minTime, float maxTime)
        {
            _minMag = -minMaxMagnitude;
            _maxMag = minMaxMagnitude;
            _minTime = minTime;
            _maxTime = maxTime;

            _targetMag = Utilities.Rnd.NextFloat(_minMag, _maxMag);
            _prevTargetMag = _targetMag;
            _currentMag = _targetMag;

            _targetDir = Utilities.Rnd.NextFloat(0f, 360f);
            _prevTargetDir = _targetDir;
            _currentDir = _targetDir;

            _nextMagTime = Utilities.Rnd.NextFloat(_minTime, _maxTime);
            _nextDirTime = Utilities.Rnd.NextFloat(_minTime, _maxTime);
        }

        public void Update(float dt)
        {
            if (_curMagTime >= _nextMagTime)
            {
                _targetMag = Utilities.Rnd.NextFloat(_minMag, _maxMag);
                _nextMagTime = Utilities.Rnd.NextFloat(_minTime, _maxTime);
                _curMagTime = 0f;
                _prevTargetMag = _currentMag;
            }

            _currentMag = Utilities.Lerp(_prevTargetMag, _targetMag, Utilities.Factor(_curMagTime, _nextMagTime));
            _curMagTime += dt;


            if (_curDirTime >= _nextDirTime)
            {
                _targetDir = Utilities.Rnd.NextFloat(0f, 360f);
                _nextDirTime = Utilities.Rnd.NextFloat(_minTime, _maxTime);
                _curDirTime = 0f;
                _prevTargetDir = _currentDir;
            }

            //_currentDir = Utilities.Lerp(_prevTargetDir, _targetDir, Utilities.Factor(_curDirTime, _nextDirTime));
            _currentDir = Utilities.LerpAngle(_prevTargetDir, _targetDir, Utilities.Factor(_curDirTime, _nextDirTime));

            _curDirTime += dt;
        }
    }

}
