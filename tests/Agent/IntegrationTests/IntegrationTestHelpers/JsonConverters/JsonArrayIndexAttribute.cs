using System;

namespace NewRelic.Agent.IntegrationTestHelpers.JsonConverters
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class JsonArrayIndexAttribute : System.Attribute
    {
        public uint Index { get; set; }
    }
}
