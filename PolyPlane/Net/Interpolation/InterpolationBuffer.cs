using PolyPlane.Helpers;

namespace PolyPlane.Net.Interpolation
{
    public class InterpolationBuffer<T>
    {
        private double _clientStartTime = -1;
        private SmoothDouble _offsetMedian = new SmoothDouble(10);
        private List<BufferEntry<T>> _buffer = new List<BufferEntry<T>>();
        private double _tickRate;
        private T _resetingState;
        private bool _firstTurn = true;
        private Action<T, T, double> _interpolate;

        public InterpolationBuffer(double tickRate, Action<T, T, double> interpolate)
        {
            _tickRate = tickRate;
            _interpolate = interpolate;
        }

        public void Enqueue(T state, double updatedAt)
        {
            if (_firstTurn)
            {
                _resetingState = state;
                _firstTurn = false;
            }

            var now = World.CurrentNetTimeMs();

            if (_buffer.Count == 0 && _clientStartTime == -1)
                _clientStartTime = now;

            var offset = _offsetMedian.Add(now - updatedAt);
            var roundedOffset = offset / (_tickRate / 2d) * (_tickRate / 2d);
            var newState = new BufferEntry<T>(state, updatedAt + roundedOffset + _tickRate);

            _buffer.Add(newState);
        }

        public void InterpolateState(double now)
        {
            if (_buffer.Count == 0)
            {
                _clientStartTime = -1;
                return;
            }

            if (_buffer[_buffer.Count - 1].UpdatedAt <= now)
            {
                _resetingState = _buffer[_buffer.Count - 1].State;
                _clientStartTime = _buffer[_buffer.Count - 1].UpdatedAt;

                _buffer.Clear();
                return;
            }

            for (int i = _buffer.Count - 1; i >= 0; i--)
            {
                if (_buffer[i].UpdatedAt <= now)
                {
                    _clientStartTime = -1;
                    _buffer.RemoveRange(0, i);

                    Interp(_buffer[0], _buffer[1], now);
                    return;
                }
            }

            Interp(new BufferEntry<T>(_resetingState, _clientStartTime == -1 ? now : _clientStartTime), _buffer[0], now);
        }

        private void Interp(BufferEntry<T> from, BufferEntry<T> to, double now)
        {
            var pctElapsed = (now - from.UpdatedAt) / (to.UpdatedAt - from.UpdatedAt);
            _interpolate(from.State, to.State, pctElapsed);
        }
    }
}
