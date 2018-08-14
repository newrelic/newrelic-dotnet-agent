using System;
using System.Collections.Generic;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemExtensions;

namespace NewRelic.Providers.Wrapper.RabbitMq
{
	public class BasicGetWrapper : IWrapper
	{
		private const string TransportType = "AMQP";

		public bool IsTransactionRequired => true;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;
			var canWrap = method.MatchesAny(assemblyName: RabbitMqHelper.AssemblyName, typeName: RabbitMqHelper.TypeName, methodName: "_Private_BasicGet");
			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
		{
			// (IModel) BasicGetResult BasicGet(string queue, bool noAck)
			var queue = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<string>(0);
			var destType = RabbitMqHelper.GetBrokerDestinationType(queue);
			var destName = RabbitMqHelper.ResolveDestinationName(destType, queue);

			var segment = transaction.StartMessageBrokerSegment(instrumentedMethodCall.MethodCall, destType, MessageBrokerAction.Consume, RabbitMqHelper.VendorName, destName);

			// new to capture BasicGetResult 
			return Delegates.GetDelegateFor<dynamic>(
				onFailure: segment.End,
				onSuccess: AfterWrapped
			);

			void AfterWrapped(dynamic result)
			{
				segment.RemoveSegmentFromCallStack();

				if (result == null)
					return;

				var basicProperties = result.BasicProperties;
				var headers = (Dictionary<string, object>)basicProperties.Headers;
				if (RabbitMqHelper.TryGetPayloadFromHeaders(headers, out var payload))
				{
					agentWrapperApi.ProcessInboundRequest(payload, TransportType);
				}

				segment.End();
			}
		}
	}
}
