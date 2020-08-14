// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Text;
using NewRelic.Agent.Api;
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

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var queue = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<string>(0);
            var destType = RabbitMqHelper.GetBrokerDestinationType(queue);
            var destName = RabbitMqHelper.ResolveDestinationName(destType, queue);

            var segment = transaction.StartMessageBrokerSegment(instrumentedMethodCall.MethodCall, destType, MessageBrokerAction.Consume, RabbitMqHelper.VendorName, destName);

            return Delegates.GetDelegateFor(
                onFailure: transaction.NoticeError,
                onComplete: segment.End
            );
        }
    }
}
