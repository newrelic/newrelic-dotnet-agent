using System;

namespace NewRelic.Agent.Core.Events
{
    public class CounterMetricEvent
    {
        public readonly String Namespace;
        public readonly String Name;
        public readonly Int32 Count;

        public CounterMetricEvent(String @namespace, String name, Int32 count = 1)
        {
            Namespace = @namespace;
            Name = name;
            Count = count;
        }
        public CounterMetricEvent(String name, Int32 count = 1)
        {
            Namespace = "";
            Name = name;
            Count = count;
        }
    }
}
