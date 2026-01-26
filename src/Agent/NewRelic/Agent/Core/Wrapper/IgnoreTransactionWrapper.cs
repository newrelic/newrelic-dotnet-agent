// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.Wrapper;

public class IgnoreTransactionWrapper : IWrapper
{
    private static readonly string[] PossibleWrapperNames = {
        "NewRelic.Providers.Wrapper.CustomInstrumentation.IgnoreTransactionWrapper",

        // To support older custom instrumentation we need to also accept the old tracer factory name
        "NewRelic.Agent.Core.Tracer.Factories.IgnoreTransactionTracerFactory"
    };

    public bool IsTransactionRequired => false;

    public CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
    {
        var canWrap = PossibleWrapperNames.Contains(instrumentedMethodInfo.RequestedWrapperName, StringComparer.OrdinalIgnoreCase);
        return new CanWrapResponse(canWrap);
    }

    public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
    {
        agent.CurrentTransaction.Ignore();
        return Delegates.NoOp;
    }
}