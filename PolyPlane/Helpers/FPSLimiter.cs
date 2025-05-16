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

        public FPSLimiter() { }

        public void Wait(int targetFPS)
        {
            _offsetTimer.Restart();

            long targetFrameTime = TimeSpan.TicksPerSecond / targetFPS;
            long elapTime = _fpsTimer.Elapsed.Ticks;

            if (elapTime < targetFrameTime)
            {
                // # High accuracy, low CPU usage. #
                long waitTime = (long)(targetFrameTime - elapTime);

                // Apply the current offset.
                waitTime += (long)Math.Ceiling(_offset.Current);

                if (waitTime > 0)
                {
                    _waitTimer.Wait(waitTime, false);

                    // Test how long the timer actually waited versus
                    // the expected wait time and compute an offset
                    // for the next frame.
                    _offsetTimer.Stop();

                    var offset = waitTime - _offsetTimer.Elapsed.Ticks;

                    // Filter out very large deviations. 
                    if (Math.Abs(offset) < targetFrameTime)
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
