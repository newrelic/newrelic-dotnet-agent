namespace NewRelic.Agent.Core.Attributes
{
	public class IntAttribute : Attribute<int>
	{
		public IntAttribute(string key, int value, AttributeClassification classification, AttributeDestinations defaultDestinations)
			: base(key, value, classification, defaultDestinations)
		{
		}
	}
}
