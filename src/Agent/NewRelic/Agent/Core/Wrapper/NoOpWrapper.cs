// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.Wrapper;

public interface INoOpWrapper : IWrapper
{
}

public class NoOpWrapper : INoOpWrapper
{
    private static readonly string[] PossibleWrapperNames = {
        "NewRelic.Agent.Core.Wrapper.NoOpWrapper",
        // To support older custom instrumentation we need to also accept the old tracer factory name
        "NewRelic.AgentCore.Tracer.Factories.NoOpTracerFactory"
    };

    public bool IsTransactionRequired => false;

    public CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
    {
        var canWrap = PossibleWrapperNames.Contains(instrumentedMethodInfo.RequestedWrapperName);
        return new CanWrapResponse(canWrap);
    }

    public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
    {
        return Delegates.NoOp;
    }
}