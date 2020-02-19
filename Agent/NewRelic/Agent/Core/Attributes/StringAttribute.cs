using NewRelic.SystemExtensions;
using System.Text;

namespace NewRelic.Agent.Core.Attributes
{
	public class StringAttribute : Attribute<string>
	{
		private StringAttribute(AttributeDestinations defaultDestinations, AttributeClassification classification, string key, bool valueIsTruncated, string truncatedValue)
			 : base(key, truncatedValue, classification, defaultDestinations)
		{
			IsValueTruncated = valueIsTruncated;
		}

		public StringAttribute(string key, string value, AttributeClassification classification, AttributeDestinations defaultDestinations)
			: this(defaultDestinations, classification, key, value.TruncateUnicodeStringByBytes(CUSTOM_ATTRIBUTE_VALUE_LENGTH_CLAMP, out var truncatedValue), truncatedValue)
		{
		}
	}
}