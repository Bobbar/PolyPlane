namespace PolyPlane.Helpers
{
    public static class Profiler
    {
        private static Marker[] _stats = Array.Empty<Marker>();

        static Profiler()
        {
            // Find the last stat in the enum and init the array.
            var lastStat = Enum.GetValues(typeof(ProfilerStat)).Cast<ProfilerStat>().Max();
            var numStats = (int)lastStat + 1;

            _stats = new Marker[numStats];

            for (int i = 0; i < numStats; i++)
            {
                _stats[i] = new Marker();
            }
        }

        /// <summary>
        /// Get milliseconds elapsed for the specified <see cref="ProfilerStat"/>.
        /// </summary>
        /// <param name="stat"></param>
        /// <returns></returns>
        public static double GetElapsedMilliseconds(ProfilerStat stat)
        {
            var marker = GetStatMarker(stat);

            return TimeSpan.FromTicks(marker.Elapsed).TotalMilliseconds;
        }

        /// <summary>
        /// Reset elapsed times for all stats.
        /// </summary>
        public static void ResetAll()
        {
            foreach (var marker in _stats)
            {
                marker.Elapsed = 0;
            }
        }

        /// <summary>
        /// Start recording time elapsed for the specified <see cref="ProfilerStat"/>.
        /// </summary>
        public static void Start(ProfilerStat stat)
        {
            var marker = GetStatMarker(stat);

            marker.StartTime = CurrentTime();
        }

        /// <summary>
        /// Stop recording time elapsed for the specified <see cref="ProfilerStat"/>.
        /// </summary>
        /// <param name="stat"></param>
        /// <returns>Returns the updated <see cref="Marker"/> associated with the specified <see cref="ProfilerStat"/>.</returns>
        public static Marker Stop(ProfilerStat stat)
        {
            var marker = GetStatMarker(stat);

            marker.EndTime = CurrentTime();
            marker.Elapsed = marker.EndTime - marker.StartTime;

            return marker;
        }

        /// <summary>
        /// Stop recording time elapsed for the specified <see cref="ProfilerStat"/> but adds the result to the previous elapsed value.
        /// 
        /// Used when a stat is composed of multiple disparate code blocks.
        /// </summary>
        /// <param name="stat"></param>
        /// <returns>Returns the updated <see cref="Marker"/> associated with the specified <see cref="ProfilerStat"/>.</returns>
        public static Marker StopAndAppend(ProfilerStat stat)
        {
            var marker = GetStatMarker(stat);

            marker.EndTime = CurrentTime();
            marker.Elapsed += marker.EndTime - marker.StartTime;

            return marker;
        }

        private static Marker GetStatMarker(ProfilerStat stat)
        {
            var marker = _stats[(int)stat];
            return marker;
        }

        private static long CurrentTime()
        {
            var now = DateTimeOffset.UtcNow.Ticks;
            return now;
        }
    }

    public sealed class Marker
    {
        public long StartTime { get; set; }
        public long EndTime { get; set; }
        public long Elapsed { get; set; }

        public Marker() { }

        public double GetElapsedMilliseconds()
        {
            return TimeSpan.FromTicks(Elapsed).TotalMilliseconds;
        }
    }

    public enum ProfilerStat
    {
        Update,
        Collisions,
        Render,
        NetTime,
        LigthMap
    }
}
