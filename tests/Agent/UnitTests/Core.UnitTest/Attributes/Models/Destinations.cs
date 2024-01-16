// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace NewRelic.Agent.Core.Attributes.Tests.Models
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
        public static AttributeDestinations[] ToAttributeDestinations(this IEnumerable<Destinations> testCaseDestinations)
        {
            var attributeDestinations = new List<AttributeDestinations>();

            if (testCaseDestinations == null)
                return attributeDestinations.ToArray();

            foreach (var destination in testCaseDestinations)
            {
                switch (destination)
                {
                    case Destinations.BrowserMonitoring:
                        attributeDestinations.Add(AttributeDestinations.JavaScriptAgent);
                        break;
                    case Destinations.ErrorCollector:
                        attributeDestinations.Add(AttributeDestinations.ErrorTrace);
                        break;
                    case Destinations.TransactionEvents:
                        attributeDestinations.Add(AttributeDestinations.TransactionEvent);
                        break;
                    case Destinations.TransactionTracer:
                        attributeDestinations.Add(AttributeDestinations.TransactionTrace);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            return attributeDestinations.ToArray();
        }
    }
}
