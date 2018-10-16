using System.Collections.Generic;
using System.Text;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemExtensions;

namespace NewRelic.Providers.Wrapper.RabbitMq
{
	public class RabbitMqHelper
	{
		private const string HeaderName = "newrelic";
		private const string TempQueuePrefix = "amq.";
		private const string BasicPropertiesType = "RabbitMQ.Client.Framing.BasicProperties";
		
		public const string VendorName = "RabbitMQ";
		public const string AssemblyName = "RabbitMQ.Client";
		public const string TypeName = "RabbitMQ.Client.Framing.Impl.Model";

		public static MessageBrokerDestinationType GetBrokerDestinationType(string queueNameOrRoutingKey)
		{
			if (queueNameOrRoutingKey.StartsWith(TempQueuePrefix))
				return MessageBrokerDestinationType.TempQueue;

			return queueNameOrRoutingKey.Contains(".") ? MessageBrokerDestinationType.Topic : MessageBrokerDestinationType.Queue;
		}

		public static string ResolveDestinationName(MessageBrokerDestinationType destinationType,
			string queueNameOrRoutingKey)
		{
			return (destinationType == MessageBrokerDestinationType.TempQueue ||
			        destinationType == MessageBrokerDestinationType.TempTopic)
				? null
				: queueNameOrRoutingKey;
		}

		public static bool TryGetPayloadFromHeaders(Dictionary<string, object> messageHeaders,
			out Dictionary<string, string> payloadHeaders)
		{
			if (messageHeaders == null)
			{
				payloadHeaders = null;
				return false;
			}

			foreach (var pair in messageHeaders)
			{
				if (pair.Key.ToLowerInvariant() != HeaderName)
				{
					continue;
				}

				payloadHeaders = new Dictionary<string, string>
				{
					{pair.Key, Encoding.UTF8.GetString((byte[]) pair.Value)}
				};

				return true;
			}

			payloadHeaders = null;
			return false;
		}

		public static ISegment CreateSegmentForPublishWrappers(InstrumentedMethodCall instrumentedMethodCall, ITransactionWrapperApi transactionWrapperApi, int basicPropertiesIndex)
		{
			// ATTENTION: For reasons and data on why we chose dynamic over VisibilityBypasser see Test/Benchmarking/DynamicVsBypasser.cs

			// never null. Headers property can be null.
			var basicProperties = instrumentedMethodCall.MethodCall.MethodArguments.ExtractAs<dynamic>(basicPropertiesIndex);

			var routingKey = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<string>(1);
			var destType = GetBrokerDestinationType(routingKey);
			var destName = ResolveDestinationName(destType, routingKey);

			// Check if we are getting a BasicProperties type and if not bail on DT
			if (basicProperties.GetType().ToString() != BasicPropertiesType)
			{
				return transactionWrapperApi.StartMessageBrokerSegment(instrumentedMethodCall.MethodCall, destType, MessageBrokerAction.Produce, VendorName, destName);
			}

			// if null, setup a new dictionary  and replace the null Headers property with it.
			var headers = (Dictionary<string, object>) basicProperties.Headers;

			if (headers == null)
			{
				headers = new Dictionary<string, object>();
				basicProperties.Headers = headers;
			}

			return transactionWrapperApi.StartRabbitMQSegmentAndCreateDistributedTracePayload(instrumentedMethodCall.MethodCall, destType, MessageBrokerAction.Produce, VendorName, destName, headers);

		}
	}
}
