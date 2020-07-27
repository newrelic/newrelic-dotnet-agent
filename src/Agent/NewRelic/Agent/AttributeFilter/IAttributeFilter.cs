using System.Collections.Generic;

namespace NewRelic.Agent
{
    public interface IAttributeFilter<T> where T : IAttribute
    {
        IEnumerable<T> FilterAttributes(IEnumerable<T> attributes, AttributeDestinations destination);
    }
}
