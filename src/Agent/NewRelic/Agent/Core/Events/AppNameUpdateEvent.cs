using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.Events
{
    public class AppNameUpdateEvent
    {
        public readonly IEnumerable<string> AppNames;

        public AppNameUpdateEvent(IEnumerable<String> appNames)
        {
            AppNames = appNames;
        }
    }
}
