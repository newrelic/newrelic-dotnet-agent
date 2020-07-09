using System;
using NewRelic.Agent.Core.ThreadProfiling;
using NewRelic.Agent.Core.Tracer;

namespace NewRelic.Agent.Core
{
	public sealed class DisabledAgent : IAgent
	{
		public ITracer GetTracerImpl(string tracerFactoryName, UInt32 tracerArguments, string metricName, String assemblyName, Type type, String typename, String methodName, string argumentSignature, object invocationTarget, object[] arguments, UInt64 functionId)
		{
			return null;
		}

		public ThreadProfilingService ThreadProfilingService
		{
			get { return null; }
		}

		public AgentState State
		{
			get { return AgentState.Stopped; }
		}
	}
}
