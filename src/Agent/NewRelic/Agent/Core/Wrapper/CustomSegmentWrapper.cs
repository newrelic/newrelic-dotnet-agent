// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.Wrapper;

public class CustomSegmentWrapper : IWrapper
{
    private static readonly string[] PossibleWrapperNames = {
        "NewRelic.Providers.Wrapper.CustomInstrumentation.CustomSegmentWrapper",
        "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.CustomSegmentWrapperAsync",
        "NewRelic.Agent.Core.Tracer.Factories.CustomSegmentTracerFactory"
    };

    public bool IsTransactionRequired => true;

    public CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
    {
        var canWrap = PossibleWrapperNames.Contains(instrumentedMethodInfo.RequestedWrapperName, StringComparer.OrdinalIgnoreCase);

        if (canWrap && instrumentedMethodInfo.IsAsync)
        {
            return TaskFriendlySyncContextValidator.CanWrapAsyncMethod("custom", "custom", instrumentedMethodInfo.Method.MethodName);
        }

        return new CanWrapResponse(canWrap);
    }

    public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
    {
        if (instrumentedMethodCall.IsAsync)
        {
            transaction.AttachToAsync();
        }

        // find the first string argument
        string segmentName = null;
        foreach (var argument in instrumentedMethodCall.MethodCall.MethodArguments)
        {
            segmentName = argument as string;
            if (segmentName != null)
                break;
        }

        if (segmentName == null)
        {
            throw new ArgumentException("The CustomSegmentWrapper can only be applied to a method with a String parameter.");
        }

        var segment = transaction.StartCustomSegment(instrumentedMethodCall.MethodCall, segmentName);

        return instrumentedMethodCall.IsAsync
            ? Delegates.GetAsyncDelegateFor<Task>(agent, segment)
            : Delegates.GetDelegateFor(segment);
    }
}