using System.Diagnostics;

namespace PolyPlane
{
    public class Profiler
    {
        private Stopwatch _timer = new Stopwatch();
        private Stopwatch _timer2 = new Stopwatch();


        private List<Marker> _markers = new List<Marker>();

        public Profiler() { }

        public void Clear()
        {
            _markers.Clear();
            _timer.Restart();
            _timer2.Restart();
        }

        public void Restart()
        {
            //_markers.Clear();
            _timer.Restart();
        }

        public void AddMarker(string label)
        {
            _timer.Stop();
            _markers.Add(new Marker(_timer.Elapsed, label));
            _timer.Restart();
        }

        public void Stop()
        {
            _timer.Stop();
        }

        public void Reset()
        {
            _timer.Reset();
        }

        public void AddTimeSpan(TimeSpan elap, string label)
        {
            _markers.Add(new Marker(elap, label));
        }

        public void PrintResults()
        {
            _timer.Stop();
            _timer2.Stop();

            TimeSpan totalTime = TimeSpan.Zero;

            foreach (var marker in _markers)
            {
                totalTime += marker.Elapsed;
                Log.Msg($"{marker.Label}:  {marker.Elapsed.TotalMilliseconds} ms  {marker.Elapsed.Ticks} ticks");
            }

            Log.Msg($"Total: {totalTime.TotalMilliseconds} ms  {totalTime.Ticks} ticks");
            Log.Msg($"");

        }

        private class Marker
        {
            public TimeSpan Elapsed { get; set; }
            public string Label { get; set; }

            public Marker(TimeSpan elap, string label)
            {
                Elapsed = elap;
                Label = label;
            }
        }
    }
}
