using System;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.RabbitMq
{
	public class RabbitMqHelper
	{
		public static String VendorName = "RabbitMQ";

		public static MessageBrokerDestinationType GetBrokerDestinationType(String queueNameOrRoutingKey)
		{
			if (queueNameOrRoutingKey.StartsWith("amq."))
				return MessageBrokerDestinationType.TempQueue;

			return queueNameOrRoutingKey.Contains(".") ? MessageBrokerDestinationType.Topic : MessageBrokerDestinationType.Queue;
		}

		public static String ResolveDestinationName(MessageBrokerDestinationType destinationType,  String queueNameOrRoutingKey)
		{
			return (destinationType == MessageBrokerDestinationType.TempQueue || destinationType == MessageBrokerDestinationType.TempTopic)
				? null
				: queueNameOrRoutingKey;
		}
	}
}
