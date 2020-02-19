namespace NewRelic.Agent.Core.Attributes
{
	public class BoolAttribute : Attribute<bool>
	{
		public BoolAttribute(string key, bool value, AttributeClassification classification, AttributeDestinations defaultDestinations)
			   : base(key, value, classification, defaultDestinations)
		{
		}

	}
}
