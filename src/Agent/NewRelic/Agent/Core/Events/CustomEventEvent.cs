using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.Events
{
    public class CustomEventEvent
    {
        public readonly String EventType;
        public readonly IEnumerable<KeyValuePair<String, Object>> Attributes;

        public CustomEventEvent(string eventType, IEnumerable<KeyValuePair<String, Object>> attributes)
        {
            EventType = eventType;
            Attributes = attributes;
        }
    }
}
