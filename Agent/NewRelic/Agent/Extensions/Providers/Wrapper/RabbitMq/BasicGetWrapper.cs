using System;
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
			var canWrap = method.MatchesAny(assemblyName: "RabbitMQ.Client", typeName: "RabbitMQ.Client.Framing.Impl.Model", methodName: "_Private_BasicGet");
			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
		{
			// (IModel) BasicGetResult BasicGet(string queue, bool noAck)
			var queue = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<String>(0);
			var destType = RabbitMqHelper.GetBrokerDestinationType(queue);
			var destName = RabbitMqHelper.ResolveDestinationName(destType, queue);

			var segment = transaction.StartMessageBrokerSegment(instrumentedMethodCall.MethodCall, destType, MessageBrokerAction.Consume, RabbitMqHelper.VendorName, destName);
			return Delegates.GetDelegateFor(segment);
		}
	}
}
