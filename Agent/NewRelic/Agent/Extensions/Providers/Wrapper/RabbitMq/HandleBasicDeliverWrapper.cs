using System.Collections.Generic;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemExtensions;

namespace NewRelic.Providers.Wrapper.RabbitMq
{
	public class HandleBasicDeliverWrapper : IWrapper
	{
		public bool IsTransactionRequired => false;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;
			var canWrap = method.MatchesAny(assemblyName: RabbitMqHelper.AssemblyName, typeName: "RabbitMQ.Client.Events.EventingBasicConsumer", methodName: "HandleBasicDeliver");
			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
		{
			// (IBasicConsumer) void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, byte[] body)
			var routingKey = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<string>(4);
			var destType = RabbitMqHelper.GetBrokerDestinationType(routingKey);
			var destName = RabbitMqHelper.ResolveDestinationName(destType, routingKey);

			transaction = agent.CreateMessageBrokerTransaction(destType, RabbitMqHelper.VendorName, routingKey);

			// ATTENTION: We have validated that the use of dynamic here is appropriate based on the visibility of the data we're working with.
			// If we implement newer versions of the API or new methods we'll need to re-evaluate.
			// basicProperties is never null (framework supplies it), though the Headers property could be
			var basicProperties = instrumentedMethodCall.MethodCall.MethodArguments.ExtractAs<dynamic>(5);
			var headers = (Dictionary<string, object>)basicProperties.Headers;
			if (RabbitMqHelper.TryGetPayloadFromHeaders(headers, agent, out var payload))
			{
				transaction.AcceptDistributedTracePayload(payload, TransportType.AMQP);
			}

			var segment = transaction.StartMessageBrokerSegment(instrumentedMethodCall.MethodCall, destType, MessageBrokerAction.Consume, RabbitMqHelper.VendorName, destName);

			return Delegates.GetDelegateFor(
				onFailure: transaction.NoticeError,
				onComplete: () =>
				{
					segment.End();
					transaction.End();
				});
		}
	}
}
