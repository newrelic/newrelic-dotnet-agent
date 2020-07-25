using System;

namespace NewRelic.Agent
{
    public interface IAttribute
    {
        String Key { get; }
        Object Value { get; }

        AttributeDestinations DefaultDestinations { get; }
    }
}
