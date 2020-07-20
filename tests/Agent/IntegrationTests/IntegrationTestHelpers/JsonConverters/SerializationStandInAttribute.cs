using System;

namespace NewRelic.Agent.IntegrationTestHelpers.JsonConverters
{
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
	public class SerializationStandInAttribute : System.Attribute { }
}