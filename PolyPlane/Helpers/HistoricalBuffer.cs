using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PolyPlane
{
    public class HistoricalBuffer<T>
    {
        private const int MAX_HIST = 100;
        private int _histPos = 0;
        private List<BufferEntry<T>> _history = new List<BufferEntry<T>>();
        public Func<T, T, double, T> Interpolate;

        public HistoricalBuffer()
        {

        }

        public HistoricalBuffer(Func<T, T, double, T> interpolate)
        {
            Interpolate = interpolate;
        }

        public void Enqueue(T state, double timestamp)
        {
            var entry = new BufferEntry<T>(state, timestamp);

            if (_history.Count < MAX_HIST)
            {
                _history.Add(entry);
            }
            else
            {
                _history[_histPos] = entry;
            }

            _histPos = (_histPos + 1) % MAX_HIST;
        }


        public T GetHistoricalState(double timestamp)
        {
            var now = DateTime.UtcNow.TimeOfDay.TotalMilliseconds;
            //var sorted = _history.OrderBy(h => h.UpdatedAt).ToList();
            var sorted = _history.OrderByDescending(h => h.UpdatedAt).ToList();


            var range = sorted.First().UpdatedAt - sorted.Last().UpdatedAt;

            for (int i = 0; i < sorted.Count - 1; i++)
            {
                var entry1 = sorted[i];
                var entry2 = sorted[i + 1];
                //var entry2 = _history[i + 1];

                //if (entry1.UpdatedAt >= timestamp)
                //    return entry1.State;

                if (entry1.UpdatedAt >= timestamp && entry2.UpdatedAt <= timestamp)
                {
                    var pctElapsed = (now - entry1.UpdatedAt) / (entry2.UpdatedAt - entry1.UpdatedAt);
                    return Interpolate(entry1.State, entry2.State, pctElapsed);
                }
            }

            return default(T);
        }

        //public T GetHistoricalState(double timestamp)
        //{
        //    //var sorted = _history.OrderBy(h => h.UpdatedAt).ToList();
        //    var sorted = _history.OrderByDescending(h => h.UpdatedAt).ToList();

        //    for (int i = 0; i < sorted.Count; i++)
        //    {
        //        var entry1 = sorted[i];
        //        //var entry2 = _history[i + 1];

        //        //if (entry1.UpdatedAt >= timestamp)
        //        //    return entry1.State;

        //        if (entry1.UpdatedAt <= timestamp)
        //            return entry1.State;
        //    }

        //    return default(T);
        //}


    }
}
