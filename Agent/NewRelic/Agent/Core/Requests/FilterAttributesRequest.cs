using JetBrains.Annotations;
using NewRelic.Agent.Core.Transactions;

namespace NewRelic.Agent.Core.Requests
{
	public class FilterAttributesRequest
	{
		public readonly AttributeDestinations AttributeDestination;
		[NotNull] public readonly Attributes Attributes;

		public FilterAttributesRequest([NotNull] Attributes attributes, AttributeDestinations attributeDestination)
		{
			AttributeDestination = attributeDestination;
			Attributes = attributes;
		}
	}
}
