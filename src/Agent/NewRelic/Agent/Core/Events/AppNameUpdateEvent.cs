using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace NewRelic.Agent.Core.Events
{
    public class AppNameUpdateEvent
    {
        [NotNull]
        public readonly IEnumerable<string> AppNames;

        public AppNameUpdateEvent([NotNull] IEnumerable<String> appNames)
        {
            AppNames = appNames;
        }
    }
}
