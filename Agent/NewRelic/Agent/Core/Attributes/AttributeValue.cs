using System;

namespace NewRelic.Agent.Core.Attributes
{
	public interface IAttributeValue
	{
		AttributeDefinition AttributeDefinition { get; }

		object Value { get; set; }

		Lazy<object> LazyValue { get; set; }
	
		void MakeImmutable();
	}
}
