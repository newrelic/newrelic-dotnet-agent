using System;

namespace NewRelic.Agent.IntegrationTestHelpers.JsonConverters
{
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
	public sealed class TimeSpanSerializesAsMillisecondsAttribute : System.Attribute { }
}
