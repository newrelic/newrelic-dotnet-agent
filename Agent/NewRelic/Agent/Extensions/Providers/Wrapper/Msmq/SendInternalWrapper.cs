using System;
using System.Messaging;
using System.Runtime.Remoting.Messaging;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using MethodCall = NewRelic.Agent.Extensions.Providers.Wrapper.MethodCall;

namespace NewRelic.Providers.Wrapper.Msmq
{
	/// <summary>
	/// Instrumentation wrapper that generates the Produce action for a Microsoft Message Queue (MSMQ) operation.
	/// </summary>
	public class SendInternalWrapper : IWrapper
	{
		public bool IsTransactionRequired => true;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;
			var canWrap = method.MatchesAny(assemblyName: "System.Messaging", typeName: "System.Messaging.MessageQueue", methodName: "SendInternal");
			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransactionWrapperApi transactionWrapperApi)
		{
			const string queueVendorName = "Msmq";
			var queue = instrumentedMethodCall.MethodCall.InvocationTarget as MessageQueue;
			if (queue == null)
				throw new NullReferenceException("Method's invocationTarget is not a valid MessageQueue");

			var segment = transactionWrapperApi.StartMessageBrokerSegment(instrumentedMethodCall.MethodCall, MessageBrokerDestinationType.Queue, MessageBrokerAction.Produce, queueVendorName, queue.QueueName);
			return Delegates.GetDelegateFor(segment);
		}
	}
}
