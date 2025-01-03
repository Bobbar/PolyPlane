﻿namespace PolyPlane.Net
{
    public class HistoricalBuffer<T>
    {
        private const int MAX_HIST = 50;
        private List<BufferEntry<T>> _history = new List<BufferEntry<T>>();
        private  Func<T, T, double, T> _interpolate;

        public HistoricalBuffer(Func<T, T, double, T> interpolate)
        {
            _interpolate = interpolate;
        }

        public void Enqueue(T state, double timestamp)
        {
            var entry = new BufferEntry<T>(state, timestamp);

            _history.Add(entry);

            if (_history.Count > MAX_HIST)
                _history.RemoveAt(0);
        }

        public T GetHistoricalState(double timestamp)
        {
            for (int i = 0; i < _history.Count - 1; i++)
            {
                var entry1 = _history[i];
                var entry2 = _history[i + 1];

                if (entry1.UpdatedAt <= timestamp && entry2.UpdatedAt >= timestamp)
                {
                    var pctElapsed = (timestamp - entry1.UpdatedAt) / (entry2.UpdatedAt - entry1.UpdatedAt);
                    return _interpolate(entry1.State, entry2.State, pctElapsed);
                }
            }

            return default;
        }
    }
}
