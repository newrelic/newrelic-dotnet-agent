using System;
using System.Threading;

namespace NewRelic.Agent.Core
{
    public delegate Object GetTracerDelegate(String tracerFactoryName, UInt32 tracerArguments, String metricName, String assemblyName, Type type, String typeName, String methodName, String argumentSignature, Object invocationTarget, Object[] args, UInt64 functionId);
    public delegate void FinishTracerDelegate(Object tracerObject, Object returnValue, Object exceptionObject);

    /// <summary>
    /// This class is here to mock the agent for the profiling integration tests.
    /// Put this into NewRelicHome (instead of the real NewRelic.Agent.Core.dll) and execute your test running under a profiler.
    /// The unit tests can set the thread local delegates that will be called when the profiler injects code that calls into NewRelic.Agent.Core
    /// </summary>
    public class AgentShim
    {
        public static GetTracerDelegate GetTracerDelegate;
        public static FinishTracerDelegate FinishTracerDelegate;

        public static object GetTracer(String tracerFactoryName, UInt32 tracerArguments, String metricName, String assemblyName, Type type, String typeName, String methodName, String argumentSignature, Object invocationTarget, Object[] args, UInt64 functionId)
        {
            var result = GetTracerDelegate(tracerFactoryName, tracerArguments, metricName, assemblyName, type, typeName, methodName, argumentSignature, invocationTarget, args, functionId);
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
            FinishTracerDelegate(tracerObject, returnValue, exceptionObject);
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
