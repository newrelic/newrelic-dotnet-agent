using System;

namespace NewRelic.Agent.Core.JsonConverters
{
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
	public class SerializationStandInAttribute : System.Attribute { }
}