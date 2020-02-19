namespace NewRelic.Agent.Core.Attributes
{
	public class FloatAttribute : Attribute<float>
	{
		public FloatAttribute(string key, float value, AttributeClassification classification, AttributeDestinations defaultDestinations)
			: base(key, value, classification, defaultDestinations)
		{
		}
	}
}
