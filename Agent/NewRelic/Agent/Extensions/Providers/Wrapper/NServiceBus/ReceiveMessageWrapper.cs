using System;
using JetBrains.Annotations;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemExtensions;
using NServiceBus.Pipeline.Contexts;
using NServiceBus.Unicast.Messages;

namespace NewRelic.Providers.Wrapper.NServiceBus
{
	/// <summary>
	/// Factory for NServiceBusReceiveMessage
	/// </summary>
	public class ReceiveMessageWrapper : IWrapper
	{
		public bool IsTransactionRequired => false;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;
			var canWrap = method.MatchesAny(assemblyName: "NServiceBus.Core", typeName: "NServiceBus.InvokeHandlersBehavior", methodName: "Invoke", parameterSignature: "NServiceBus.Pipeline.Contexts.IncomingContext,System.Action");
			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall,
			IAgentWrapperApi agentWrapperApi, ITransactionWrapperApi transactionWrapperApi)
		{
			var incomingContext = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<IncomingContext>(0);
			var logicalMessage = incomingContext.IncomingLogicalMessage;
			if (logicalMessage == null)
				throw new NullReferenceException("logicalMessage");

			var headers = logicalMessage.Headers;
			if (headers == null)
				throw new NullReferenceException("headers");

			const string brokerVendorName = "NServiceBus";
			var queueName = TryGetQueueName(logicalMessage);
			transactionWrapperApi = agentWrapperApi.CreateMessageBrokerTransaction(MessageBrokerDestinationType.Queue, brokerVendorName, queueName);

			var segment = transactionWrapperApi.StartMessageBrokerSegment(instrumentedMethodCall.MethodCall, MessageBrokerDestinationType.Queue, MessageBrokerAction.Consume, brokerVendorName, queueName);

			agentWrapperApi.ProcessInboundRequest(headers, "HTTP");

			return Delegates.GetDelegateFor(
				onFailure: transactionWrapperApi.NoticeError,
				onComplete: () =>
				{
					segment.End();
					transactionWrapperApi.End();
				});
		}

		[CanBeNull]
		private static string TryGetQueueName([NotNull] LogicalMessage logicalMessage)
		{
			if (logicalMessage.MessageType == null)
				return null;

			return logicalMessage.MessageType.FullName;
		}
	}
}
