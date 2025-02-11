namespace PolyPlane.Helpers
{
    /// <summary>
    /// Provides basic scheduling for events which need to occur on a regular interval.
    /// </summary>
    public sealed class ActionScheduler
    {
        private List<ScheduledAction> _actions = new List<ScheduledAction>();

        public ActionScheduler() { }

        /// <summary>
        /// Executes all actions scheduled for the current time.
        /// </summary>
        public void DoActions()
        {
            var now = CurrentTime();

            foreach (var action in _actions)
            {
                if (now >= action.NextRun)
                {
                    action.Action();
                    action.NextRun = GetNextRun(action.Interval);
                }
            }
        }

        /// <summary>
        /// Add a new scheduled action.
        /// </summary>
        /// <param name="interval">Interval in milliseconds.</param>
        /// <param name="action">Action to perform per the specified interval.</param>
        public void AddAction(float interval, Action action)
        {
            var nextRun = GetNextRun(interval);
            var schedAction = new ScheduledAction(action, interval, nextRun);
            _actions.Add(schedAction);
        }

        private long GetNextRun(float interval)
        {
            var nextRun = CurrentTime() + TimeSpan.FromMilliseconds(interval).Ticks;
            return nextRun;
        }

        private long CurrentTime()
        {
            var now = DateTimeOffset.UtcNow.Ticks;
            return now;
        }

        private class ScheduledAction
        {
            public float Interval;
            public long NextRun;
            public Action Action;

            public ScheduledAction(Action action, float interval)
            {
                Action = action;
                Interval = interval;
            }

            public ScheduledAction(Action action, float interval, long nextRun) 
            {
                Interval = interval;
                NextRun = nextRun;
                Action = action;
            }
        }
    }
}
