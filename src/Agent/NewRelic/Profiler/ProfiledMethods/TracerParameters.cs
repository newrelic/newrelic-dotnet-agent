// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0



using System;

namespace NewRelic.Agent.Tests.ProfiledMethods
{
    public class GetTracerParameters
    {
        public bool getTracerCalled = false;
        public Guid tracer = Guid.Empty;

        public String tracerFactoryName = null;
        public UInt32 tracerArguments = 0;
        public String metricName = null;
        public String assemblyName = null;
        public Type type = null;
        public String typeName = null;
        public String methodName = null;
        public String argumentSignature = null;
        public Object invocationTarget = null;
        public Object[] args = null;
    }

    public class FinishTracerParameters
    {
        public bool finishTracerCalled = false;

        public Object tracerObject = null;
        public Object returnValue = null;
        public Object exceptionObject = null;
    }
}
