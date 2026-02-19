namespace PolyPlane.Helpers
{
    /// <summary>
    /// Provides a round-robin running average for a float value over time.
    /// </summary>
    public sealed class SmoothFloat
    {
        private readonly int _numValues;
        private float _current;
        private float _num;

        /// <summary>
        /// Current average.
        /// </summary>
        public float Current
        {
            get
            {
                return _current;
            }
        }

        /// <summary>
        /// Creates a new instance with the specified max number of values.
        /// </summary>
        /// <param name="numValues">The max number of values to maintain an average of.</param>
        public SmoothFloat(int numValues)
        {
            _numValues = numValues;
        }

        /// <summary>
        /// Add a new value to the averaged collection.
        /// </summary>
        /// <param name="value">Value to be added to the collection and averaged.</param>
        /// <returns>Returns the new accumulative average value.</returns>
        public float Add(float value)
        {
            if (float.IsNaN(value))
                return _current;

            if (_num < _numValues)
                _num++;

            // Cumulative average.
            _current = (value + _num * _current) / (_num + 1);

            return _current;
        }

        public void Clear()
        {
            _num = 0;
            _current = 0f;
        }
    }

    public sealed class SmoothDouble
    {
        private readonly int _numValues;
        private double _current;
        private double _num;

        /// <summary>
        /// Current average.
        /// </summary>
        public double Current
        {
            get
            {
                return _current;
            }
        }

        /// <summary>
        /// Creates a new instance with the specified max number of values.
        /// </summary>
        /// <param name="numValues">The max number of values to maintain an average of.</param>
        public SmoothDouble(int numValues)
        {
            _numValues = numValues;
        }

        /// <summary>
        /// Add a new value to the averaged collection.
        /// </summary>
        /// <param name="value">Value to be added to the collection and averaged.</param>
        /// <returns>Returns the new accumulative average value.</returns>
        public double Add(double value)
        {
            if (double.IsNaN(value))
                return _current;

            if (_num < _numValues)
                _num++;

            // Cumulative average.
            _current = (value + _num * _current) / (_num + 1);

            return _current;
        }

        public void Clear()
        {
            _current = 0;
            _num = 0;
        }
    }

    public sealed class SmoothPoint
    {
        private readonly int _numValues;
        private D2DPoint _current;
        private int _num = 0;

        /// <summary>
        /// Current average.
        /// </summary>
        public D2DPoint Current
        {
            get
            {
                return _current;
            }
        }

        /// <summary>
        /// Creates a new instance with the specified max number of values.
        /// </summary>
        /// <param name="numValues">The max number of values to maintain an average of.</param>
        public SmoothPoint(int numValues)
        {
            _numValues = numValues;
        }

        /// <summary>
        /// Add a new value to the averaged collection.
        /// </summary>
        /// <param name="value">Value to be added to the collection and averaged.</param>
        /// <returns>Returns the new accumulative average value.</returns>
        public D2DPoint Add(D2DPoint value)
        {
            if (float.IsNaN(value.X) || float.IsNaN(value.Y))
                return _current;

            if (_num < _numValues)
                _num++;

            // Cumulative average.
            _current = (value + _num * _current) / (_num + 1);

            return _current;
        }
    }
}
