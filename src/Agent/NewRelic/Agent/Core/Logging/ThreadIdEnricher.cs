using System.Threading;
using Serilog.Core;
using Serilog.Events;

namespace NewRelic.Agent.Core
{
    class ThreadIdEnricher : ILogEventEnricher
    {
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
                "tid", Thread.CurrentThread.ManagedThreadId));
        }
    }
}