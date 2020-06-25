/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.Threading;

namespace NewRelic.Agent.Core
{
    delegate Object GetTracerDelegate(String tracerFactoryName, UInt32 tracerArguments, String metricName, String assemblyName, Type type, String typeName, String methodName, String argumentSignature, Object invocationTarget, Object[] args, UInt64 functionId);
    delegate void FinishTracerDelegate(Object tracerObject, Object returnValue, Object exceptionObject);

    /// <summary>
    /// This class is here to mock the agent for the profiling integration tests.
    /// Put this into NewRelicHome (instead of the real NewRelic.Agent.Core.dll) and execute your test running under a profiler.
    /// The unit tests can set the thread local delegates that will be called when the profiler injects code that calls into NewRelic.Agent.Core
    /// </summary>
    public class AgentShim
    {
        public static object GetTracer(String tracerFactoryName, UInt32 tracerArguments, String metricName, String assemblyName, Type type, String typeName, String methodName, String argumentSignature, Object invocationTarget, Object[] args, UInt64 functionId)
        {
            var delegateDataSlot = Thread.GetNamedDataSlot("NEWRELIC_TEST_GET_TRACER_DELEGATE");
            var getTracerDelegate = (Delegate)Thread.GetData(delegateDataSlot);
            var result = getTracerDelegate.DynamicInvoke(new object[] { tracerFactoryName, tracerArguments, metricName, assemblyName, type, typeName, methodName, argumentSignature, invocationTarget, args, functionId });
            return result;
        }

        public static Action<object, Exception> GetFinishTracerDelegate(
            String tracerFactoryName,
            UInt32 tracerArguments,
            String metricName,
            String assemblyName,
            Type type,
            String typeName,
            String methodName,
            String argumentSignature,
            Object invocationTarget,
            Object[] args,
            UInt64 functionId)
        {
            var tracer = GetTracer(
                tracerFactoryName,
                tracerArguments,
                metricName,
                assemblyName,
                type,
                typeName,
                methodName,
                argumentSignature,
                invocationTarget,
                args,
                functionId);

            return new TracerWrapper(tracer).FinishTracer;
        }

        public static void FinishTracer(Object tracerObject, Object returnValue, Object exceptionObject)
        {
            var delegateDataSlot = Thread.GetNamedDataSlot("NEWRELIC_TEST_FINISH_TRACER_DELEGATE");
            var finishTracerDelegate = (Delegate)Thread.GetData(delegateDataSlot);
            finishTracerDelegate.DynamicInvoke(new object[] { tracerObject, returnValue, exceptionObject });
        }
    }

    public class TracerWrapper
    {
        private readonly Object tracer;
        public TracerWrapper(Object tracer)
        {
            this.tracer = tracer;
        }

        public void FinishTracer(object returnValue, Exception exception)
        {
            AgentShim.FinishTracer(tracer, returnValue, exception);
        }
    }
}
