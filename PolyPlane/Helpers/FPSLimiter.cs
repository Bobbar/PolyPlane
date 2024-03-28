using System.Diagnostics;

namespace PolyPlane
{
    public class FPSLimiter : IDisposable
    {
        private WaitableTimer _waitTimer = new WaitableTimer();
        private Stopwatch _fpsTimer = new Stopwatch();

        public FPSLimiter() { }



        public void Wait(int targetFPS)
        {
            long ticksPerSecond = TimeSpan.TicksPerSecond;
            long targetFrameTime = ticksPerSecond / targetFPS;
            long waitTime = 0;

            if (_fpsTimer.IsRunning)
            {
                long elapTime = _fpsTimer.Elapsed.Ticks;

                if (elapTime < targetFrameTime)
                {
                    // # High accuracy, low CPU usage. #
                    waitTime = (long)(targetFrameTime - elapTime);
                    if (waitTime > 0)
                    {
                        _waitTimer.Wait(waitTime, false);
                    }

                    // # Most accurate, highest CPU usage. #
                    //while (_fpsTimer.Elapsed.Ticks < targetFrameTime && !_loopTask.IsCompleted)
                    //{
                    //	Thread.SpinWait(10000);
                    //}
                    //elapTime = _fpsTimer.Elapsed.Ticks;

                    // # Less accurate, less CPU usage. #
                    //waitTime = (long)(targetFrameTime - elapTime);
                    //if (waitTime > 0)
                    //{
                    //	Thread.Sleep(new TimeSpan(waitTime));
                    //}
                }

                _fpsTimer.Restart();
            }
            else
            {
                _fpsTimer.Start();
                return;
            }
        }

        public void Dispose()
        {
            _waitTimer?.Dispose();
        }
    }
}
