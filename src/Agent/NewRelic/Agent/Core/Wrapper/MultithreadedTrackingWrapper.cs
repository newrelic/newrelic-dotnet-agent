// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.Wrapper;

public class MultithreadedTrackingWrapper : IWrapper
{
    public bool IsTransactionRequired => false;

    public CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
    {
        var canWrap = "MultithreadedTrackingWrapper".Equals(instrumentedMethodInfo.RequestedWrapperName, StringComparison.OrdinalIgnoreCase);

        if (canWrap && instrumentedMethodInfo.IsAsync)
        {
            return new CanWrapResponse(false, "This instrumentation is not intended to be used with async-await. Use the OtherTransactionWrapperAsync instead.");
        }

        return new CanWrapResponse(canWrap);
    }

    public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
    {
        var typeName = instrumentedMethodCall.MethodCall.Method.Type;
        var methodName = instrumentedMethodCall.MethodCall.Method.MethodName;

        var name = $"{typeName}/{methodName}";

        transaction = agent.CreateTransaction(
            isWeb: instrumentedMethodCall.StartWebTransaction,
            category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Custom),
            transactionDisplayName: name,
            doNotTrackAsUnitOfWork: false);

        transaction.AttachToAsync();

        var segment = !string.IsNullOrEmpty(instrumentedMethodCall.RequestedMetricName)
            ? transaction.StartCustomSegment(instrumentedMethodCall.MethodCall, instrumentedMethodCall.RequestedMetricName)
            : transaction.StartTransactionSegment(instrumentedMethodCall.MethodCall, name);

        var hasMetricName = !string.IsNullOrEmpty(instrumentedMethodCall.RequestedMetricName);
        if (hasMetricName)
        {
            var priority = instrumentedMethodCall.RequestedTransactionNamePriority ?? TransactionNamePriority.Uri;
            transaction.SetCustomTransactionName(instrumentedMethodCall.RequestedMetricName, priority);
        }

        return Delegates.GetDelegateFor(
            onFailure: transaction.NoticeError,
            onComplete: OnComplete);

        void OnComplete()
        {
            segment.End();
            transaction.End();
        }
    }
}
