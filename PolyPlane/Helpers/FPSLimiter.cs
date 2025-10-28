using PolyPlane.Helpers;
using System.Diagnostics;

namespace PolyPlane
{
    public class FPSLimiter : IDisposable
    {
        private WaitableTimer _waitTimer = new WaitableTimer();
        private Stopwatch _fpsTimer = new Stopwatch();
        private Stopwatch _offsetTimer = new Stopwatch();
        private SmoothDouble _offset = new SmoothDouble(3);
        private long _targetFrameTime = 0;
        private int _targetFPS = 60;

        public FPSLimiter() { }

        public void Wait(int targetFPS)
        {
            if (targetFPS != _targetFPS)
            {
                _targetFrameTime = TimeSpan.TicksPerSecond / targetFPS;
                _targetFPS = targetFPS;
                _offset.Clear();
            }

            _offsetTimer.Restart();
           
            long elapTime = _fpsTimer.Elapsed.Ticks;

            if (elapTime < _targetFrameTime)
            {
                long waitTime = (long)(_targetFrameTime - elapTime);

                // Apply the current offset.
                waitTime += (long)Math.Ceiling(_offset.Current);
              
                if (waitTime > 0)
                {
                    // High accuracy, low CPU usage waitable timer.
                    _waitTimer.Wait(waitTime, false);

                    // Test how long the timer actually waited versus
                    // the expected wait time and compute an offset
                    // for the next frame.
                    _offsetTimer.Stop();

                    var offset = waitTime - _offsetTimer.Elapsed.Ticks;

                    // Filter out very large deviations. 
                    if (Math.Abs(offset) < _targetFrameTime)
                        _offset.Add(offset);
                }
            }

            _fpsTimer.Restart();
        }

        public void Dispose()
        {
            _waitTimer?.Dispose();
        }
    }
}
