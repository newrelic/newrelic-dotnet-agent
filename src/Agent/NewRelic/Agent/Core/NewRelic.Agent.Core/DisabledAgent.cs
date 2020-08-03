// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Core.ThreadProfiling;
using NewRelic.Agent.Core.Tracer;

namespace NewRelic.Agent.Core
{
    public sealed class DisabledAgent : IAgent
    {
        public ITracer GetTracerImpl(string tracerFactoryName, uint tracerArguments, string metricName, string assemblyName, Type type, string typename, string methodName, string argumentSignature, object invocationTarget, object[] arguments, ulong functionId)
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
