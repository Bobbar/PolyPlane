using System.Runtime.InteropServices;

namespace PolyPlane
{
    /// <summary>
    /// Provides a high accuracy, low power waitable timer.
    /// </summary>
    public sealed class WaitableTimer : IDisposable
    {
        private IntPtr handle;
        private bool disposedValue;
        private const uint INFINITE_TIMEOUT = 0xFFFFFFFF;

        /// <summary>
        /// Create a new <see cref="WaitableTimer"/> with default configuration.
        /// </summary>
        public WaitableTimer() : this(
            IntPtr.Zero,
            null,
            CreateFlags.CREATE_WAITABLE_TIMER_HIGH_RESOLUTION | CreateFlags.CREATE_WAITABLE_TIMER_MANUAL_RESET,
            AccessFlags.TIMER_ALL_ACCESS)
        { }

        public WaitableTimer(IntPtr attributes, string name, CreateFlags flags, AccessFlags access)
        {
            var handle = CreateWaitableTimerExW(attributes, name, flags, access);

            if (handle == IntPtr.Zero)
            {
                throw new Exception("Failed to create timer.");
            }
            else
            {
                this.handle = handle;
            }
        }

        /// <summary>
        /// Waits and blocks the current thread for the specified number of ticks.
        /// </summary>
        /// <param name="dueTimeTicks">Time to wait in ticks.</param>
        public void WaitTicks(long dueTimeTicks)
        {
            // Due time must be positive.
            if (dueTimeTicks <= 0)
                return;

            // But the timer requires due time to be negative...
            // Flip the sign.
            var dt = dueTimeTicks * -1;

            // Set the timer params then start the wait.
            SetWaitableTimer(handle, ref dt, 0, null, IntPtr.Zero, fResume: false);
            WaitForSingleObject(handle, INFINITE_TIMEOUT);
        }

        /// <summary>
        /// Waits and blocks the current thread for the specified number of milliseconds.
        /// </summary>
        /// <param name="dueTimeMilliseconds">Time to wait in milliseconds.</param>
        public void WaitMs(float dueTimeMilliseconds)
        {
            var dueTimeTicks = TimeSpan.FromMilliseconds(dueTimeMilliseconds).Ticks;
            WaitTicks(dueTimeTicks);
        }

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                // Cancel any current wait.
                CancelWaitableTimer(handle);

                var res = CloseHandle(handle);

                if (res)
                    this.handle = IntPtr.Zero;

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public enum CreateFlags
        {
            CREATE_WAITABLE_TIMER_MANUAL_RESET = 1,
            CREATE_WAITABLE_TIMER_HIGH_RESOLUTION = 2
        }

        public enum AccessFlags
        {
            TIMER_QUERY_STATE = 1,
            TIMER_MODIFY_STATE = 2,
            TIMER_ALL_ACCESS = 2031619
        }

        private delegate void TimerAPCProc(
           IntPtr lpArgToCompletionRoutine,
           UInt32 dwTimerLowValue,
           UInt32 dwTimerHighValue);

        [DllImport("kernel32", SetLastError = true, ExactSpelling = true)]
        private static extern IntPtr CreateWaitableTimerExW(IntPtr lpTimerAttributes, string lpTimerName, CreateFlags dwFlags, AccessFlags dwDesiredAccess);

        [DllImport("kernel32", SetLastError = true, ExactSpelling = true)]
        private static extern bool CancelWaitableTimer(IntPtr hTimer);

        [DllImport("kernel32", SetLastError = true, ExactSpelling = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32", SetLastError = true, ExactSpelling = true)]
        private static extern bool SetWaitableTimer(IntPtr hTimer, [In] ref long pDueTime, Int64 lPeriod, TimerAPCProc pfnCompletionRoutine, IntPtr lpArgToCompletionRoutine, bool fResume);

        [DllImport("kernel32", SetLastError = true, ExactSpelling = true)]
        private static extern Int32 WaitForSingleObject(IntPtr handle, uint milliseconds);
    }
}
