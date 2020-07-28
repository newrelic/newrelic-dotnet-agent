using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.RabbitMq
{
    public class RabbitMqHelper
    {
        public static string VendorName = "RabbitMQ";

        public static MessageBrokerDestinationType GetBrokerDestinationType(string queueNameOrRoutingKey)
        {
            if (queueNameOrRoutingKey.StartsWith("amq."))
                return MessageBrokerDestinationType.TempQueue;

            return queueNameOrRoutingKey.Contains(".") ? MessageBrokerDestinationType.Topic : MessageBrokerDestinationType.Queue;
        }

        public static string ResolveDestinationName(MessageBrokerDestinationType destinationType, string queueNameOrRoutingKey)
        {
            return (destinationType == MessageBrokerDestinationType.TempQueue || destinationType == MessageBrokerDestinationType.TempTopic)
                ? null
                : queueNameOrRoutingKey;
        }
    }
}
