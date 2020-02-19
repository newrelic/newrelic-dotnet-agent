using System.Collections.Generic;

namespace NewRelic.Agent.Core.Attributes
{
	public interface IAttributeFilter
	{
		IEnumerable<Attribute> FilterAttributes(IEnumerable<Attribute> attributes, AttributeDestinations destination);
		IEnumerable<Attribute> FilterAttributes(IEnumerable<Attribute> attributes, AttributeDestinations destination, bool logInvalidAttribs);

	}
}
