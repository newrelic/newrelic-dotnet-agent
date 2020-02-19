namespace NewRelic.Agent.Core.Attributes
{
	public class DoubleAttribute : Attribute<double>
	{
		public DoubleAttribute(string key, double value, AttributeClassification classification, AttributeDestinations defaultDestinations)
			   : base(key, value, classification, defaultDestinations)
		{
		}
	}
}
