using System;
using System.Linq;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemExtensions;
using NServiceBus.Unicast.Messages;

namespace NewRelic.Providers.Wrapper.NServiceBus
{
    /// <summary>
    /// Factory for NServiceBusSendMessage
    /// </summary>
    public class SendMessageWrapper : IWrapper
    {
        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;
            var canWrap = method.MatchesAny(assemblyName: "NServiceBus.Core", typeName: "NServiceBus.Unicast.UnicastBus", methodName: "SendMessage", parameterSignature: "NServiceBus.Unicast.SendOptions,NServiceBus.Unicast.Messages.LogicalMessage");
            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
        {
            var logicalMessage = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<LogicalMessage>(1);

            AttachCatHeaders(agentWrapperApi, logicalMessage);

            const string brokerVendorName = "NServiceBus";
            var queueName = TryGetQueueName(logicalMessage);
            var segment = transaction.StartMessageBrokerSegment(instrumentedMethodCall.MethodCall, MessageBrokerDestinationType.Queue, MessageBrokerAction.Produce, brokerVendorName, queueName);

            return Delegates.GetDelegateFor(segment);
        }

        private static void AttachCatHeaders(IAgentWrapperApi agentWrapperApi, LogicalMessage logicalMessage)
        {
            if (logicalMessage.Headers == null)
                return;

            var headers = agentWrapperApi.CurrentTransaction
                .GetRequestMetadata()
                .Where(header => header.Value != null)
                .Where(header => header.Key != null);

            foreach (var header in headers)
            {
                logicalMessage.Headers[header.Key] = header.Value;
            }
        }

        /// <summary>
        /// Returns a metric name based on the type of message. The source/destination queue isn't always known (depending on the circumstances) and in some cases isn't even relevant. The message type is always known and is always relevant.
        /// </summary>
        /// <param name="logicalMessage"></param>
        /// <returns></returns>
        private static string TryGetQueueName(LogicalMessage logicalMessage)
        {
            if (logicalMessage.MessageType == null)
                return null;

            return logicalMessage.MessageType.FullName;
        }
    }
}
