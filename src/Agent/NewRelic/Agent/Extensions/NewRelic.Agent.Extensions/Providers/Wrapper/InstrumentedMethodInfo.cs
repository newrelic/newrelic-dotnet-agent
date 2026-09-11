// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Extensions.Providers.Wrapper;

/// <summary>
/// The immutable details about an instrumented method.
/// </summary>
public class InstrumentedMethodInfo
{
    private readonly long _functionId;
    public readonly Method Method;
    public readonly string RequestedWrapperName;
    public readonly bool IsAsync;

    /// <summary>
    /// True for a runtime-async method. Distinct from <see cref="IsAsync"/>, which
    /// WrapperService also sets for these methods once it can restore the Task their body does not
    /// return; this flag says the method is runtime-async specifically.
    ///
    /// Two wrappers read it, both while deciding what to do rather than when cleaning up:
    ///
    /// OtherTransactionWrapper, in BeforeWrappedMethod immediately after AttachToAsync(), to decide
    /// whether to also call DetachFromPrimary() -- and only when this wrapper is what created the
    /// transaction. Clearing primary storage that early is the whole point: a runtime-async method's
    /// after-delegate does not fire until true completion, so deferring it would leave the
    /// transaction in the creating thread's thread-local slot for the method's entire life.
    ///
    /// MultithreadedTrackingWrapper, in CanWrap, to refuse these methods outright, because it calls
    /// AttachToAsync() with no paired DetachFromPrimary().
    ///
    /// See those two wrappers for the full reasoning.
    /// </summary>
    public readonly bool IsRuntimeAsync;
    public readonly string RequestedMetricName;
    public readonly TransactionNamePriority? RequestedTransactionNamePriority;
    public readonly bool StartWebTransaction;

    public InstrumentedMethodInfo(long functionId, Method method, string requestedWrapperName, bool isAsync, string requestedMetricName, TransactionNamePriority? requestedTransactionNamePriority, bool startWebTransaction, bool isRuntimeAsync = false)
    {
        Method = method;
        RequestedWrapperName = requestedWrapperName;
        _functionId = functionId;
        IsAsync = isAsync;
        IsRuntimeAsync = isRuntimeAsync;
        RequestedMetricName = requestedMetricName;
        RequestedTransactionNamePriority = requestedTransactionNamePriority;
        StartWebTransaction = startWebTransaction;
    }

    public override int GetHashCode()
    {
        return _functionId.GetHashCode();
    }

    public override bool Equals(object other)
    {
        if (!(other is InstrumentedMethodInfo))
            return false;

        var otherMethod = (InstrumentedMethodInfo)other;
        return _functionId == otherMethod._functionId;
    }

    public override string ToString()
    {
        return $"Method: {Method}, RequestedWrapperName: {RequestedWrapperName}, IsAsync: {IsAsync}, RequestedMetricName: {RequestedMetricName}, RequestedTransactionNamePriority: {RequestedTransactionNamePriority}, StartWebTransaction: {StartWebTransaction}, IsRuntimeAsync: {IsRuntimeAsync}";
    }
}
