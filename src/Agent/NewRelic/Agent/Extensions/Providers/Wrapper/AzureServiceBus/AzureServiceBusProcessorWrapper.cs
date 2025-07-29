// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.AzureServiceBus;

public class AzureServiceBusProcessorWrapper : AzureServiceBusWrapperBase
{
    public override bool IsTransactionRequired => true;

    public override CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
    {
        var canWrap = instrumentedMethodInfo.RequestedWrapperName.Equals(nameof(AzureServiceBusProcessorWrapper));
        return new CanWrapResponse(canWrap);
    }

    public override AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
    {
        if (instrumentedMethodCall.IsAsync)
            transaction.AttachToAsync();

        // this call wraps the client event handler callback, so start a method segment that will time the callback
        var segment = transaction.StartMethodSegment(
            instrumentedMethodCall.MethodCall,
            instrumentedMethodCall.MethodCall.Method.Type.Name,
            instrumentedMethodCall.MethodCall.Method.MethodName);

        return instrumentedMethodCall.IsAsync ?
            Delegates.GetAsyncDelegateFor<Task>(agent, segment)
            :
            Delegates.GetDelegateFor(segment);
    }
}
