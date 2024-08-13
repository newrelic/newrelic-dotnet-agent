using System;

namespace NewRelic.Providers.Wrapper.AzureFunction
{
    public static class TriggerTypeExtensions
    {
        public static string ResolveTriggerType(this string triggerTypeName)
        {
            // triggerTypeName is a short typename; we want everything to the left of "TriggerAttribute"
            var trigger = triggerTypeName.Substring(0, triggerTypeName.IndexOf("TriggerAttribute", StringComparison.Ordinal));

            // TODO: this logic may need some tweaking. The return values are based on
            // https://opentelemetry.io/docs/specs/semconv/attributes-registry/faas/ (scroll to the bottom)
            switch (trigger)
            {
                case "Kafka":
                case "Timer":
                    return "timer";

                case "Blob":
                case "CosmosDB":
                    return "datasource";

                case "SignalR":
                case "EventGrid":
                case "EventHub":
                case "ServiceBus":
                case "Queue":
                case "RabbitMQ":
                    return "pubsub";

                case "Http":
                    return "http";

                default:
                    return "other";
            }
        }
    }
}
