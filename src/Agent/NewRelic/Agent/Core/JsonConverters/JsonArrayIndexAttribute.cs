using System;

namespace NewRelic.Agent.Core.JsonConverters
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class JsonArrayIndexAttribute : System.Attribute
    {
        public UInt32 Index { get; set; }
    }
}
