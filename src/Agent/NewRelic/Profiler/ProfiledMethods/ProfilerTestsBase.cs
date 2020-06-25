/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NewRelic.Agent.Tests.ProfiledMethods;
using NUnit.Framework;

namespace NewRelic.Agent.Tests.ProfiledMethods
{
    public class ProfilerTestsBase
    {
        public delegate Object GetTracerDelegate(String tracerFactoryName, UInt32 tracerArguments, String metricName, String assemblyName, Type type, String typeName, String methodName, String argumentSignature, Object invocationTarget, Object[] args, UInt64 functionId);
        public delegate void FinishTracerDelegate(Object tracerObject, Object returnValue, Object exceptionObject);
        public delegate void AddCustomParameterDelegate(String key, String value);
        public delegate String GetBrowserTimingHeaderDelegate();
        public delegate void NoticeError1Delegate(Exception exception, IDictionary<String, String> parameters);
        public delegate void NoticeError2Delegate(Exception exception);
        public delegate void NoticeError3Delegate(String message, IDictionary<String, String> parameters);

        #region Get and Finish Tracers

        protected void SetGetTracerDelegate(GetTracerDelegate getTracerDelegate)
        {
            Thread.SetData(Thread.GetNamedDataSlot("NEWRELIC_TEST_GET_TRACER_DELEGATE"), getTracerDelegate);
        }

        protected void SetFinishTracerDelegate(FinishTracerDelegate finishTracerDelegate)
        {
            Thread.SetData(Thread.GetNamedDataSlot("NEWRELIC_TEST_FINISH_TRACER_DELEGATE"), finishTracerDelegate);
        }

        protected GetTracerParameters DefaultGetTracerImplementation()
        {
            var getTracerParameters = new GetTracerParameters();

            // setup the code to execute when NewRelic.Agent.Core.GetTracer is called
            SetGetTracerDelegate((String tracerFactoryName, UInt32 tracerArguments, String metricName, String assemblyName, Type type, String typeName, String methodName, String argumentSignature, Object invocationTarget, Object[] args, UInt64 functionId) =>
            {
                // we can't assert in here so save off the variables and assert later
                getTracerParameters.tracerFactoryName = tracerFactoryName;
                getTracerParameters.tracerArguments = tracerArguments;
                getTracerParameters.metricName = metricName;
                getTracerParameters.assemblyName = assemblyName;
                getTracerParameters.type = type;
                getTracerParameters.typeName = typeName;
                getTracerParameters.methodName = methodName;
                getTracerParameters.argumentSignature = argumentSignature;
                getTracerParameters.invocationTarget = invocationTarget;
                getTracerParameters.args = args;

                // set the flag indicating the tracer was called, we'll be asserting on that later
                getTracerParameters.getTracerCalled = true;

                // create a new GUID for this tracer and push it onto our stack of GUIDs
                getTracerParameters.tracer = Guid.NewGuid();

                // return the GUID as the tracer object, we'll validate that FinishTracer gets it
                return getTracerParameters.tracer;
            });

            return getTracerParameters;
        }

        protected FinishTracerParameters DefaultFinishTracerImplementation()
        {
            var finishTracerParameters = new FinishTracerParameters();

            // setup the code to execute when NewRelic.Agent.Core.FinishTracer is called
            SetFinishTracerDelegate((Object tracerObject, Object returnValue, Object exceptionObject) =>
            {
                // we can't assert in here so save off the variables and assert later
                finishTracerParameters.tracerObject = tracerObject;
                finishTracerParameters.returnValue = returnValue;
                finishTracerParameters.exceptionObject = exceptionObject;

                // set the flag indicating the tracer was called, we'll be asserting on that later
                finishTracerParameters.finishTracerCalled = true;
            });

            return finishTracerParameters;
        }
        #endregion

        #region Validate Tracers
        protected void ValidateTracers(GetTracerParameters getTracerParameters, FinishTracerParameters finishTracerParameters, String expectedMethodName, String expectedArgumentSignature, object expectedInvocationTarget, object[] expectedParameters, object expectedReturnValue, Exception expectedException, String expectedTypeName = "NewRelic.Agent.Tests.ProfiledMethods.ProfiledMethods")
        {
            ValidateGetTracer(getTracerParameters, expectedMethodName, expectedArgumentSignature, expectedInvocationTarget, expectedParameters, expectedTypeName);
            ValidateFinishTracer(getTracerParameters, finishTracerParameters, expectedReturnValue, expectedException);
        }

        private void ValidateGetTracer(GetTracerParameters getTracerParameters, String expectedMethodName, String expectedArgumentSignature, object expectedInvocationTarget, object[] parameters, String expectedTypeName)
        {
            // validate that GetTracer was called at all
            Assert.IsTrue(getTracerParameters.getTracerCalled, "NewRelic.Agent.Core.GetTracer was not called.  Are you instrumenting this test suite?");

            // validate that GetTracer received all the right parameters
            Assert.AreEqual("ProfiledMethods", getTracerParameters.assemblyName, "GetTracer did not receive the expected assembly name.");
            Assert.AreEqual(expectedTypeName, getTracerParameters.typeName, "GetTracer did not receive the expected type name.");
            Assert.AreEqual(expectedMethodName, getTracerParameters.methodName, "GetTracer did not receive the expected method name.");
            Assert.AreEqual(expectedArgumentSignature, getTracerParameters.argumentSignature, "GetTracer did not receive the expected argument signature.");
            Assert.AreEqual(expectedInvocationTarget, getTracerParameters.invocationTarget, "GetTracer did not receive the expected invocation target.");
            Assert.AreEqual(parameters.Length, getTracerParameters.args.Length, "GetTracer did not receive the expected parameter array length.");
            int i = 0;
            foreach (var parameter in parameters)
            {
                Assert.AreEqual(parameter, getTracerParameters.args[i], "GetTracer did not receive the expected value of parameter #" + i + ".");
                ++i;
            }
        }

        private void ValidateFinishTracer(GetTracerParameters getTracerParameters, FinishTracerParameters finishTracerParameters, object expectedReturnValue, Exception expectedException)
        {
            // validate that finish tracer was called
            Assert.IsTrue(finishTracerParameters.finishTracerCalled, "NewRelic.Agent.Core.FinishTracer was not called.");

            // validate that FinishTracer received all the right parameters
            Assert.AreEqual(getTracerParameters.tracer, finishTracerParameters.tracerObject, "The tracer received by finish tracer doesn't match the one we returned from GetTracer.");
            Assert.AreEqual(expectedReturnValue, finishTracerParameters.returnValue, "FinishTracer did not receive the correct returnValue.");
            Assert.AreEqual(expectedException, finishTracerParameters.exceptionObject, "FinishTracer did not receive the expected exception.");
        }
        #endregion
    }

}
