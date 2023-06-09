  public class Clock
    {
        public Action OnTick;
        private System.Threading.Timer timer;
        private readonly int tickFrequency;
        public Clock(int tickFrequency)
        {
           timer = new System.Threading.Timer(Tick,null,tickFrequency,tickFrequency);
           this.tickFrequency = tickFrequency;
        }

        private void Tick(object state)
        {
            OnTick?.Invoke();
        }

        public void Awake()
        {
            timer.Change(tickFrequency, tickFrequency);
        }
        public void Pause()
        {
            timer.Change(Timeout.Infinite, tickFrequency);
        }

    }
