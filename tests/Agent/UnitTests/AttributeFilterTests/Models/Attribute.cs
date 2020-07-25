using System;
using NewRelic.Agent;

namespace AttributeFilterTests.Models
{
    public class Attribute : IAttribute
    {
        public String Key { get; private set; }
        public Object Value { get; private set; }

        public AttributeDestinations DefaultDestinations { get; private set; }

        public Attribute(AttributeDestinations defaultDestinations, String key, String value)
        {
            DefaultDestinations = defaultDestinations;
            Key = key;
            Value = value;
        }
    }
}
