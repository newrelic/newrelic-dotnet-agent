/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System.Messaging;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.Msmq
{
    /// <summary>
    /// Instrumentation wrapper that generates the Consume and Peek actions for the respective Microsoft Message Queue (MSMQ) operations.
    /// </summary>
    public class ReceiveCurrentWrapper : IWrapper
    {
        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;
            var canWrap = method.MatchesAny(assemblyName: "System.Messaging", typeName: "System.Messaging.MessageQueue", methodName: "ReceiveCurrent");
            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
        {
            const string queueVendorName = "Msmq";
            var queue = instrumentedMethodCall.MethodCall.InvocationTarget as MessageQueue;
            if (queue == null)
                return null;

            var operation = MessageBrokerAction.Consume;
            var args = instrumentedMethodCall.MethodCall.MethodArguments;

            // System.Messaging.MessageQueue.ReceiveCurrent takes more than two arguments but the second argument is an integer action.
            // If the action is 0, the message will be consumed. Otherwise, it will only be peeked at.
            if (args.Length >= 2 && (args[1] is int))
            {
                operation = (((int)args[1]) != 0) ? MessageBrokerAction.Peek : MessageBrokerAction.Consume;
            }

            var segment = transaction.StartMessageBrokerSegment(instrumentedMethodCall.MethodCall, MessageBrokerDestinationType.Queue, operation, queueVendorName, queue.QueueName);
            return Delegates.GetDelegateFor(segment);
        }
    }
}
