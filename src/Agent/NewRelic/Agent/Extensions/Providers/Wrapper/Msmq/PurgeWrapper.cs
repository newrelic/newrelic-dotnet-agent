using System;
using System.Messaging;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.Msmq
{
    /// <summary>
    /// Instrumentation wrapper that generates the Purge action for a Microsoft Message Queue (MSMQ) operation.
    /// </summary>
    public class PurgeWrapper : IWrapper
    {
        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;
            var canWrap = method.MatchesAny(assemblyName: "System.Messaging", typeName: "System.Messaging.MessageQueue", methodName: "Purge");
            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
        {
            const string queueVendorName = "Msmq";
            var queue = instrumentedMethodCall.MethodCall.InvocationTarget as MessageQueue;
            if (queue == null)
                return null;

            var segment = transaction.StartMessageBrokerSegment(instrumentedMethodCall.MethodCall, MessageBrokerDestinationType.Queue, MessageBrokerAction.Purge, queueVendorName, queue.QueueName);
            return Delegates.GetDelegateFor(segment);
        }
    }
}
