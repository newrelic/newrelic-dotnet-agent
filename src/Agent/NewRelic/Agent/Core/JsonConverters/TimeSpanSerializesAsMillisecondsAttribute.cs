using System;

namespace NewRelic.Agent.Core.JsonConverters
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class TimeSpanSerializesAsMillisecondsAttribute : System.Attribute { }
}
