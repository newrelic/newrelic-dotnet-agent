using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace NewRelic.Agent.Core.Events
{
    public class CustomEventEvent
    {
        [NotNull]
        public readonly String EventType;

        [NotNull]
        public readonly IEnumerable<KeyValuePair<String, Object>> Attributes;

        public CustomEventEvent([NotNull] string eventType, [NotNull] IEnumerable<KeyValuePair<String, Object>> attributes)
        {
            EventType = eventType;
            Attributes = attributes;
        }
    }
}
