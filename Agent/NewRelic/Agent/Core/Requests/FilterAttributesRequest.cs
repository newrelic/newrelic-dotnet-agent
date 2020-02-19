using NewRelic.Agent.Core.Attributes;

namespace NewRelic.Agent.Core.Requests
{
	public class FilterAttributesRequest
	{
		public readonly AttributeDestinations AttributeDestination;
		public readonly AttributeCollection Attributes;

		public FilterAttributesRequest(AttributeCollection attributes, AttributeDestinations attributeDestination)
		{
			AttributeDestination = attributeDestination;
			Attributes = attributes;
		}
	}
}
