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
            IAgentWrapperApi agentWrapperApi, ITransaction transaction)
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
            transaction = agentWrapperApi.CreateMessageBrokerTransaction(MessageBrokerDestinationType.Queue, brokerVendorName, queueName);

            var segment = transaction.StartMessageBrokerSegment(instrumentedMethodCall.MethodCall, MessageBrokerDestinationType.Queue, MessageBrokerAction.Consume, brokerVendorName, queueName);

            agentWrapperApi.ProcessInboundRequest(headers);

            return Delegates.GetDelegateFor(
                onFailure: transaction.NoticeError,
                onComplete: () =>
                {
                    segment.End();
                    transaction.End();
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
