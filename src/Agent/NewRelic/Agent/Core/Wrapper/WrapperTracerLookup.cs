// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Core.Tracer;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.Wrapper;

/// <summary>
/// The tracer lookup shared by agent mode (<see cref="AgentManager"/>) and OTel distro mode
/// (<see cref="OtelDistro.DistroAgentManager"/>): resolves a wrapper for an instrumented call via
/// <see cref="IWrapperService.BeforeWrappedMethod"/> and wraps its delegate in a <see cref="WrapperTracer"/>.
/// This is on the per-instrumented-call hot path -- keep it allocation-free beyond the single
/// <see cref="WrapperTracer"/> construction, and keep the "Tracer invocation error" log text identical, since
/// log lines are integration-test contracts.
/// </summary>
public static class WrapperTracerLookup
{
    public static ITracer GetTracer(IWrapperService wrapperService, string tracerFactoryName, uint tracerArguments, string metricName, Type type, string methodName, string argumentSignature, object invocationTarget, object[] arguments, ulong functionId, Type effectiveReturnType)
    {
        try
        {
            // First try to get a wrapper from the newer WrapperService
            var afterWrappedMethodDelegate = wrapperService.BeforeWrappedMethod(type, methodName, argumentSignature, invocationTarget, arguments, tracerFactoryName, metricName, tracerArguments, functionId, effectiveReturnType);
            return (afterWrappedMethodDelegate != null) ? new WrapperTracer(afterWrappedMethodDelegate) : null;
        }
        catch (Exception e)
        {
            Log.Error(e, "Tracer invocation error");
            return null;
        }
    }
}
