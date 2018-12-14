using System.Collections.Generic;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemExtensions;

namespace NewRelic.Providers.Wrapper.RabbitMq
{
	public class BasicGetWrapper : IWrapper
	{
		public bool IsTransactionRequired => true;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;
			var canWrap = method.MatchesAny(assemblyName: RabbitMqHelper.AssemblyName, typeName: RabbitMqHelper.TypeName, methodName: "_Private_BasicGet");
			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransactionWrapperApi transactionWrapperApi)
		{
			// (IModel) BasicGetResult BasicGet(string queue, bool noAck)
			var queue = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<string>(0);
			var destType = RabbitMqHelper.GetBrokerDestinationType(queue);
			var destName = RabbitMqHelper.ResolveDestinationName(destType, queue);

			var segment = transactionWrapperApi.StartMessageBrokerSegment(instrumentedMethodCall.MethodCall, destType, MessageBrokerAction.Consume, RabbitMqHelper.VendorName, destName);

			// ATTENTION: We have validated that the use of dynamic, here and below, is appropriate based on the visibility of the data we're working with.
			// If we implement newer versions of the API or new methods we'll need to re-evaluate.
			// new to capture BasicGetResult 
			return Delegates.GetDelegateFor<dynamic>(
				onFailure: segment.End,
				onSuccess: AfterWrapped
			);

			void AfterWrapped(dynamic result)
			{
				segment.End();

				if (result != null)
				{
					var basicProperties = result.BasicProperties;
					var headers = (Dictionary<string, object>)basicProperties.Headers;
					if (RabbitMqHelper.TryGetPayloadFromHeaders(headers, agentWrapperApi, out var payload))
					{
						transactionWrapperApi.AcceptDistributedTracePayload(payload, TransportType.AMQP);
					}
				}
				
			}
		}
	}
}
