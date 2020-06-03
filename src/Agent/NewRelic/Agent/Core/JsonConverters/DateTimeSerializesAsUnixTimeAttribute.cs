using System;

namespace NewRelic.Agent.Core.JsonConverters
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class DateTimeSerializesAsUnixTimeSecondsAttribute : System.Attribute { }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class DateTimeSerializesAsUnixTimeMillisecondsAttribute : System.Attribute { }

}
