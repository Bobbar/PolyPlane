using System.Diagnostics;

namespace PolyPlane
{
    public class FPSLimiter : IDisposable
    {
        private WaitableTimer _waitTimer = new WaitableTimer();
        private Stopwatch _fpsTimer = new Stopwatch();

        const int FPS_OFFSET = 1;

        public FPSLimiter() { }

        public void Wait(int targetFPS)
        {
            long ticksPerSecond = TimeSpan.TicksPerSecond;
            long targetFrameTime = ticksPerSecond / (targetFPS + FPS_OFFSET);
            long elapTime = _fpsTimer.Elapsed.Ticks;

            if (elapTime < targetFrameTime)
            {
                // # High accuracy, low CPU usage. #
                long waitTime = (long)(targetFrameTime - elapTime);
                if (waitTime > 0)
                {
                    _waitTimer.Wait(waitTime, false);
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
