namespace NewRelic.Agent.Core.Attributes
{
	public class LongAttribute : Attribute<long>
	{
		public LongAttribute(string key, long value, AttributeClassification classification, AttributeDestinations defaultDestinations)
			: base(key, value, classification, defaultDestinations)
		{
		}
	}
}
