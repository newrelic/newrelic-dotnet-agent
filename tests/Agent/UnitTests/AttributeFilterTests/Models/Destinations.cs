using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using NewRelic.Agent;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace AttributeFilterTests.Models
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum Destinations
    {
        [EnumMember(Value = "browser_monitoring")]
        BrowserMonitoring = 1,
        [EnumMember(Value = "error_collector")]
        ErrorCollector = 2,
        [EnumMember(Value = "transaction_events")]
        TransactionEvents = 4,
        [EnumMember(Value = "transaction_tracer")]
        TransactionTracer = 8,
    }

    public static class DestinationsExtensions
    {
        public static AttributeDestinations ToAttributeDestination(this IEnumerable<Destinations> destinations)
        {
            var attributeDestinations = AttributeDestinations.None;
            if (destinations == null)
                return attributeDestinations;

            foreach (var destination in destinations)
            {
                switch (destination)
                {
                    case Destinations.BrowserMonitoring:
                        attributeDestinations |= AttributeDestinations.JavaScriptAgent;
                        break;
                    case Destinations.ErrorCollector:
                        attributeDestinations |= AttributeDestinations.ErrorTrace;
                        break;
                    case Destinations.TransactionEvents:
                        attributeDestinations |= AttributeDestinations.TransactionEvent;
                        break;
                    case Destinations.TransactionTracer:
                        attributeDestinations |= AttributeDestinations.TransactionTrace;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            return attributeDestinations;
        }
    }
}
