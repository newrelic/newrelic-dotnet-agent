using NewRelic.Agent;

namespace AttributeFilterTests.Models
{
	public class Attribute : IAttribute
	{
		public string Key { get; private set; }

		public object Value { get; private set; }

		public AttributeDestinations DefaultDestinations { get; private set; }

		public Attribute(AttributeDestinations defaultDestinations, string key, string value)
		{
			DefaultDestinations = defaultDestinations;
			Key = key;
			Value = value;
		}
	}
}
