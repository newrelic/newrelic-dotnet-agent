using System;
using JetBrains.Annotations;
using NewRelic.Agent;

namespace AttributeFilterTests.Models
{
    public class Attribute : IAttribute
    {
        [NotNull]
        public String Key { get; private set; }

        [NotNull]
        public Object Value { get; private set; }

        public AttributeDestinations DefaultDestinations { get; private set; }

        public Attribute(AttributeDestinations defaultDestinations, [NotNull] String key, [NotNull] String value)
        {
            DefaultDestinations = defaultDestinations;
            Key = key;
            Value = value;
        }
    }
}
