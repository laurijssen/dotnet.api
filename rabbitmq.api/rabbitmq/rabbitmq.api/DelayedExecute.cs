namespace rabbitmq.api
{
    public static class DelayedExecute
    {
        class TimerState
        {
            public Timer Timer { get; set; } = null!;
        }

        public static Timer Do(Action action, int dueTime)
        {
            var state = new TimerState();
            state.Timer = new Timer(o =>
            {
                action();
                lock (o!) // The locking should prevent the timer callback from trying to free the timer prior to the Timer field having been set.
                {
                    ((TimerState)o).Timer.Dispose();
                }
            }, state, dueTime, -1);
            return state.Timer;
        }
    }
}
