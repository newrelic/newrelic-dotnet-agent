// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.Wrapper;

public class AttachToAsyncWrapper : IWrapper
{
    private static readonly string[] PossibleWrapperNames = {
        "AttachToAsyncWrapper"
    };

    public bool IsTransactionRequired => true;

    public CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
    {
        var canWrap = instrumentedMethodInfo.IsAsync
                      && PossibleWrapperNames.Contains(instrumentedMethodInfo.RequestedWrapperName);

        if (!canWrap)
        {
            return new CanWrapResponse(false);
        }

        return TaskFriendlySyncContextValidator.CanWrapAsyncMethod(instrumentedMethodInfo.Method.Type.Assembly.GetName().Name, instrumentedMethodInfo.Method.Type.Name, instrumentedMethodInfo.Method.MethodName);
    }

    public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
    {
        transaction.AttachToAsync();
        return Delegates.NoOp;
    }
}
