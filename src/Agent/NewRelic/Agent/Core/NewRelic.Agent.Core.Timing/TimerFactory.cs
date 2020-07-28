using NewRelic.Agent.Core.Timing;

namespace NewRelic.Agent.Core.NewRelic.Agent.Core.Timing
{
    public interface ITimerFactory
    {
        /// <summary>
        /// Starts and returns a new timer.
        /// </summary>
        /// <returns>A started timer.</returns>
        ITimer StartNewTimer();
    }

    public class TimerFactory : ITimerFactory
    {
        public ITimer StartNewTimer()
        {
            return new Timer();
        }
    }
}
