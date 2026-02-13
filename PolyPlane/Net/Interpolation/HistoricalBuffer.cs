namespace PolyPlane.Net.Interpolation
{
    public sealed class HistoricalBuffer<T>
    {
        // Max allowed historical data in milliseconds.
        private const long MAX_HIST_TIME = 400;

        private List<BufferEntry<T>> _history = new List<BufferEntry<T>>();
        private Func<T, T, double, T> _interpolate;

        public HistoricalBuffer(Func<T, T, double, T> interpolate)
        {
            _interpolate = interpolate;
        }

        public void Enqueue(T state, double timestamp)
        {
            var entry = new BufferEntry<T>(state, timestamp);

            if (_history.Count > 0)
            {
                // Only add if the entry is newer than the previous.
                if (entry.UpdatedAt > _history.Last().UpdatedAt)
                {
                    _history.Add(entry);
                }
            }
            else
            {
                _history.Add(entry);
            }

            // Prune old entries.
            var now = World.CurrentNetTimeMs();
            while (_history.Count > 0 && now - _history.First().UpdatedAt > MAX_HIST_TIME)
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
