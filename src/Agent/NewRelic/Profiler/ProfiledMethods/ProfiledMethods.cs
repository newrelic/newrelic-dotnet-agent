/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System.Collections.Generic;
using NUnit.Framework;
using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace NewRelic.Agent.Tests.ProfiledMethods
{

    [TestFixture]
    [Category("Profiler Instrumentation Tests")]
    public class ProfiledMethods : ProfilerTestsBase
    {
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        private void EmptyMethod()
        {
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        private void ClassParameter(EmptyClass foo)
        {
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        private EmptyClass ClassReturn()
        {
            return new EmptyClass();
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        private void PassByReference(ref SimpleClass parameter)
        {
            parameter.data = 5;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        private void PrimitivePassByReference(ref bool parameter)
        {
            parameter = true;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        private void LotsOfParameters(bool a, bool b, bool c, bool d, bool e, bool f, bool g, bool h, bool i, bool j, bool k, bool l, bool m, bool n)
        {
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        private void ThrowsException()
        {
            throw new MyException();
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        private void UnProfiledThrowsException()
        {
            throw new MyException();
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        private void CallsUnProfiledThrowsException()
        {
            UnProfiledThrowsException();
        }


        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        private void ThrowsAndCatchesException()
        {
            try
            {
                throw new Exception();
            }
            catch (Exception)
            {
                // swallow
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        private void FinallyExecutedOnException(SimpleClass foo)
        {
            try
            {
                throw new Exception();
            }
            catch (Exception)
            {
                // swallow
            }
            finally
            {
                foo.data = 5;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        private static void StaticMethod()
        {
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        private static void StaticMethod2()
        {
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        private void MethodWithOutParameter(out String outputString)
        {
            outputString = "Result";
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        private int InterfaceParameter(IInterface parameter)
        {
            return parameter.ReturnNumber(5);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        private Exception ExecuteStepSimulator(IExecutionStepSimulator step, out bool result)
        {
            Exception exception = null;
            try
            {
                step.DoFirstTry1();
                try
                {
                    step.DoSecondTry1();
                    try
                    {
                        step.DoThirdTry();
                    }
                    finally
                    {
                        step.DoFinally();
                    }
                    step.DoSecondTry2();
                }
                catch (Exception exception2)
                {
                    exception = exception2;
                    step.DoFirstCatch();
                }
                step.DoFirstTry2();
            }
            catch (Exception exception3)
            {
                exception = exception3;
                step.DoSecondCatch();
            }
            result = true;
            return exception;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        private void Overloaded(Object parameter)
        {
            Overloaded(parameter.ToString());
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        private void Overloaded(String parameter)
        {
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        private Guid ReturnsValueClass()
        {
            return Guid.NewGuid();
        }

        [Test]
        public void call_empty_method()
        {
            // setup the callbacks for GetTracer and FinishTracer
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();

            // call the method that will be instrumented
            Assert.DoesNotThrow(EmptyMethod, "Exception should not have been thrown.");

            ValidateTracers(getTracerParameters, finishTracerParameters, "EmptyMethod", "", this, new object[] { }, null, null);
        }

        [Test]
        public void call_class_parameter()
        {
            // setup the callbacks for GetTracer and FinishTracer
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();

            // call the method that will be instrumented
            var parameter = new EmptyClass();
            Assert.DoesNotThrow(() => { ClassParameter(parameter); }, "Exception should not have been thrown.");

            ValidateTracers(getTracerParameters, finishTracerParameters, "ClassParameter", parameter.GetType().ToString(), this, new object[] { parameter }, null, null);
        }

        [Test]
        public void call_class_return()
        {
            // setup the callbacks for GetTracer and FinishTracer
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();

            // call the method that will be instrumented
            EmptyClass result = null;
            Assert.DoesNotThrow(() => { result = ClassReturn(); }, "Exception should not have been thrown.");

            ValidateTracers(getTracerParameters, finishTracerParameters, "ClassReturn", "", this, new object[] { }, result, null);
        }

        [Test]
        public void call_pass_by_reference()
        {
            // setup the callbacks for GetTracer and FinishTracer
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();

            // call the method that will be instrumented
            SimpleClass parameter = new SimpleClass();
            parameter.data = 3;
            Assert.DoesNotThrow(() => { PassByReference(ref parameter); }, "Exception should not have been thrown.");

            ValidateTracers(getTracerParameters, finishTracerParameters, "PassByReference", parameter.GetType().ToString() + "&", this, new object[] { null }, null, null);

            // validate that the function was able to modify the parameter
            Assert.AreEqual(5, parameter.data);
        }

        [Test]
        public void call_primitive_pass_by_reference()
        {
            // setup the callbacks for GetTracer and FinishTracer
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();

            // call the method that will be instrumented
            bool parameter = false;
            Assert.DoesNotThrow(() => { PrimitivePassByReference(ref parameter); }, "Exception should not have been thrown.");

            ValidateTracers(getTracerParameters, finishTracerParameters, "PrimitivePassByReference", parameter.GetType().ToString() + "&", this, new object[] { null }, null, null);

            // validate that the function was able to modify the parameter
            Assert.AreEqual(true, parameter);
        }

        [Test]
        public void call_throws_exception()
        {
            // setup the callbacks for GetTracer and FinishTracer
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();

            // call the method that will be instrumented
            MyException expectedException = null;
            try
            {
                ThrowsException();
                Assert.Fail("Expected exception to be thrown.");
            }
            catch (MyException exception)
            {
                expectedException = exception;
            }
            catch (Exception)
            {
                Assert.Fail("Expected MyException to be thrown.");
            }

            ValidateTracers(getTracerParameters, finishTracerParameters, "ThrowsException", "", this, new object[] { }, null, expectedException);
        }

        [Test]
        public void call_throws_and_catches_exception()
        {
            // setup the callbacks for GetTracer and FinishTracer
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();

            // call the method that will be instrumented
            Assert.DoesNotThrow(ThrowsAndCatchesException, "Exception should not have been thrown.");

            ValidateTracers(getTracerParameters, finishTracerParameters, "ThrowsAndCatchesException", "", this, new object[] { }, null, null);
        }

        [Test]
        public void call_finally_executed_on_exception()
        {
            // setup the callbacks for GetTracer and FinishTracer
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();

            // call the method that will be instrumented
            var parameter = new SimpleClass();
            parameter.data = 1;
            Assert.DoesNotThrow(() => { FinallyExecutedOnException(parameter); }, "Exception should not have been thrown.");

            ValidateTracers(getTracerParameters, finishTracerParameters, "FinallyExecutedOnException", parameter.GetType().ToString(), this, new object[] { parameter }, null, null);

            // validate that the finally clause was executed
            Assert.AreEqual(5, parameter.data, "The finally clause was not executed.");
        }

        [Test]
        public void call_static_method()
        {
            // setup the callbacks for GetTracer and FinishTracer
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();

            // call the method that will be instrumented
            Assert.DoesNotThrow(StaticMethod, "Exception should not have been thrown.");

            ValidateTracers(getTracerParameters, finishTracerParameters, "StaticMethod", "", null, new object[] { }, null, null);
        }

        [Test]
        public void call_method_with_out_parameter()
        {
            // setup the callbacks for GetTracer and FinishTracer
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();

            // call the method that will be instrumented
            var parameter = "MyString";
            Assert.DoesNotThrow(() => { MethodWithOutParameter(out parameter); }, "Exception should not have been thrown.");

            Assert.AreEqual("Result", parameter, "Method did not change the out parameter.");

            ValidateTracers(getTracerParameters, finishTracerParameters, "MethodWithOutParameter", parameter.GetType().ToString() + "&", this, new object[] { null }, null, null);
        }

        [Test]
        public void call_interface_parameter()
        {
            // setup the callbacks for GetTracer and FinishTracer
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();

            // call the method that will be instrumented
            var parameter = new Implementation();
            int result = 0;
            Assert.DoesNotThrow(() => { result = InterfaceParameter(parameter); }, "Exception should not have been thrown.");

            Assert.AreEqual(5, result, "Method did not execute correctly.");

            ValidateTracers(getTracerParameters, finishTracerParameters, "InterfaceParameter", "NewRelic.Agent.Tests.ProfiledMethods.IInterface", this, new object[] { parameter }, 5, null);
        }

        [Test]
        public void call_execute_step_simulator_with_throws_everywhere()
        {
            // setup the callbacks for GetTracer and FinishTracer
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();

            var step = new ExecutionStepSimulator();
            bool output = false;

            step.doFirstTry1 = () => { throw new ArgumentException(); };
            step.doFirstTry2 = () => { throw new ArgumentException(); };
            step.doSecondTry1 = () => { throw new ArgumentException(); };
            step.doSecondTry2 = () => { throw new ArgumentException(); };
            step.doThirdTry = () => { throw new ArgumentException(); };
            step.doFirstCatch = () => { };
            step.doSecondCatch = () => { };
            step.doFinally = () => { };

            Exception result = null;
            Assert.DoesNotThrow(() => { result = ExecuteStepSimulator(step, out output); }, "Exception should not have been thrown.");

            Assert.AreEqual(true, step.firstTry1Hit);
            Assert.AreEqual(true, step.secondCatchHit);
            Assert.AreEqual(false, step.firstTry2Hit);
            Assert.AreEqual(false, step.secondTry1Hit);
            Assert.AreEqual(false, step.secondTry2Hit);
            Assert.AreEqual(false, step.thirdTryHit);
            Assert.AreEqual(false, step.firstCatchHit);
            Assert.AreEqual(false, step.finallyHit);

            ValidateTracers(getTracerParameters, finishTracerParameters, "ExecuteStepSimulator", "NewRelic.Agent.Tests.ProfiledMethods.IExecutionStepSimulator,System.Boolean&", this, new object[] { step, null }, result, null);
        }

        [Test]
        public void instrument_inner_class_method()
        {
            // setup the callbacks for GetTracer and FinishTracer
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();

            // call the method that will be instrumented
            var innerClass = new OuterClass.InnerClass();
            Assert.DoesNotThrow(() => { innerClass.InnerClassMethod(); }, "Exception should not have been thrown.");

            ValidateTracers(getTracerParameters, finishTracerParameters, "InnerClassMethod", "", innerClass, new object[] { }, null, null, "NewRelic.Agent.Tests.ProfiledMethods.OuterClass+InnerClass");
        }

        [Test]
        public void profiled_method_called_from_get_tracer()
        {
            var getTracerParameters = new GetTracerParameters();

            // setup the code to execute when NewRelic.Agent.Core.GetTracer is called
            SetGetTracerDelegate((String tracerFactoryName, UInt32 tracerArguments, String metricName, String assemblyName, Type type, String typeName, String methodName, String argumentSignature, Object invocationTarget, Object[] args, UInt64 functionId) =>
            {
                if (methodName == "StaticMethod")
                {
                    // check to see if we are approaching a stack overflow
                    var stackTrace = new System.Diagnostics.StackTrace();
                    if (stackTrace.FrameCount >= 100)
                    {
                        throw new Exception("Stack Overflow Immenent.");
                    }

                    // if StaticMethod was called, call another profiled method (whose GetTracer won't do anything).
                    StaticMethod2();

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
                }
                else
                {
                    return null;
                }
            });

            var finishTracerParameters = DefaultFinishTracerImplementation();

            Assert.DoesNotThrow(() => { StaticMethod(); }, "Exception should not have been thrown.");

            ValidateTracers(getTracerParameters, finishTracerParameters, "StaticMethod", "", null, new object[] { }, null, null);
        }

        [Test]
        public void api_call_RecordMetric()
        {
            String expectedKey = "foo";
            String expectedValue = "bar";

            // a place to store the key/value that the delegate receives
            String actualKey = String.Empty;
            String actualValue = String.Empty;

            // the delegate that should be called from the instrumented method
            AddCustomParameterDelegate myDelegate = (String key, String value) =>
            {
                actualKey = key;
                actualValue = value;
            };

            // setup the delegate to get called when NewRelic.Agent.Core.AgentApi.AddCustomParameter is called
            Thread.SetData(Thread.GetNamedDataSlot("NewRelic_Test_Api_AddCustomParameter2_Delegate"), myDelegate);

            // make sure it doesn't throw an exception
            Assert.DoesNotThrow(() => { NewRelic.Api.Agent.NewRelic.AddCustomParameter(expectedKey, expectedValue); }, "Exception should not have been thrown.");

            // validate that the delegate received the parameters we gave it
            Assert.AreEqual(expectedKey, actualKey);
            Assert.AreEqual(expectedValue, actualValue);
        }

        [Test]
        public void api_call_GetBrowserTimingHeader()
        {
            String expectedReturnValue = "foo";
            String actualReturnValue = String.Empty;

            // the delegate that should be called from the instrumented method
            GetBrowserTimingHeaderDelegate myDelegate = () =>
            {
                return expectedReturnValue;
            };

            // setup the delegate to get called when NewRelic.Agent.Core.AgentApi.AddCustomParameter is called
            Thread.SetData(Thread.GetNamedDataSlot("NewRelic_Test_Api_GetBrowserTimingHeader_Delegate"), myDelegate);

            // make sure it doesn't throw an exception
            Assert.DoesNotThrow(() => { actualReturnValue = NewRelic.Api.Agent.NewRelic.GetBrowserTimingHeader(); }, "Exception should not have been thrown.");

            // validate that the delegate received the parameters we gave it
            Assert.AreEqual(expectedReturnValue, actualReturnValue);
        }

        [Test]
        public void api_overloads()
        {
            var delegate1WasCalled = false;
            var delegate2WasCalled = false;
            var delegate3WasCalled = false;

            NoticeError1Delegate delegate1 = (_, __) => { delegate1WasCalled = true; };
            NoticeError2Delegate delegate2 = (_) => { delegate2WasCalled = true; };
            NoticeError3Delegate delegate3 = (_, __) => { delegate3WasCalled = true; };

            Thread.SetData(Thread.GetNamedDataSlot("NewRelic_Test_Api_NoticeError1_Delegate"), delegate1);
            Thread.SetData(Thread.GetNamedDataSlot("NewRelic_Test_Api_NoticeError2_Delegate"), delegate2);
            Thread.SetData(Thread.GetNamedDataSlot("NewRelic_Test_Api_NoticeError3_Delegate"), delegate3);

            Api.Agent.NewRelic.NoticeError(new Exception());
            Api.Agent.NewRelic.NoticeError(new Exception(), new Dictionary<string, string>());
            Api.Agent.NewRelic.NoticeError(String.Empty, new Dictionary<string, string>());

            Assert.IsTrue(delegate1WasCalled);
            Assert.IsTrue(delegate2WasCalled);
            Assert.IsTrue(delegate3WasCalled);
        }

        [Test]
        public void nested_overloads()
        {
            // setup the callbacks for GetTracer and FinishTracer
            var getOverloadObjectTracerParameters = new GetTracerParameters();
            var getOverloadStringTracerParameters = new GetTracerParameters();

            // setup the code to execute when NewRelic.Agent.Core.GetTracer is called
            SetGetTracerDelegate((String tracerFactoryName, UInt32 tracerArguments, String metricName, String assemblyName, Type type, String typeName, String methodName, String argumentSignature, Object invocationTarget, Object[] args, UInt64 functionId) =>
            {
                if (argumentSignature == "System.Object")
                {
                    // we can't assert in here so save off the variables and assert later
                    getOverloadObjectTracerParameters.tracerFactoryName = tracerFactoryName;
                    getOverloadObjectTracerParameters.tracerArguments = tracerArguments;
                    getOverloadObjectTracerParameters.metricName = metricName;
                    getOverloadObjectTracerParameters.assemblyName = assemblyName;
                    getOverloadObjectTracerParameters.type = type;
                    getOverloadObjectTracerParameters.typeName = typeName;
                    getOverloadObjectTracerParameters.methodName = methodName;
                    getOverloadObjectTracerParameters.argumentSignature = argumentSignature;
                    getOverloadObjectTracerParameters.invocationTarget = invocationTarget;
                    getOverloadObjectTracerParameters.args = args;

                    // set the flag indicating the tracer was called, we'll be asserting on that later
                    getOverloadObjectTracerParameters.getTracerCalled = true;

                    // create a new GUID for this tracer and push it onto our stack of GUIDs
                    getOverloadObjectTracerParameters.tracer = Guid.NewGuid();

                    // return the GUID as the tracer object, we'll validate that FinishTracer gets it
                    return getOverloadObjectTracerParameters.tracer;
                }
                else if (argumentSignature == "System.String")
                {
                    // we can't assert in here so save off the variables and assert later
                    getOverloadStringTracerParameters.tracerFactoryName = tracerFactoryName;
                    getOverloadStringTracerParameters.tracerArguments = tracerArguments;
                    getOverloadStringTracerParameters.metricName = metricName;
                    getOverloadStringTracerParameters.assemblyName = assemblyName;
                    getOverloadStringTracerParameters.type = type;
                    getOverloadStringTracerParameters.typeName = typeName;
                    getOverloadStringTracerParameters.methodName = methodName;
                    getOverloadStringTracerParameters.argumentSignature = argumentSignature;
                    getOverloadStringTracerParameters.invocationTarget = invocationTarget;
                    getOverloadStringTracerParameters.args = args;

                    // set the flag indicating the tracer was called, we'll be asserting on that later
                    getOverloadStringTracerParameters.getTracerCalled = true;

                    // create a new GUID for this tracer and push it onto our stack of GUIDs
                    getOverloadStringTracerParameters.tracer = Guid.NewGuid();

                    // return the GUID as the tracer object, we'll validate that FinishTracer gets it
                    return getOverloadStringTracerParameters.tracer;
                }
                else
                {
                    return null;
                }
            });

            var finishOverloadObjectTracerParameters = new FinishTracerParameters();
            var finishOverloadStringTracerParameters = new FinishTracerParameters();

            // setup the code to execute when NewRelic.Agent.Core.FinishTracer is called
            SetFinishTracerDelegate((Object tracerObject, Object returnValue, Object exceptionObject) =>
            {
                if ((Guid)(tracerObject) == getOverloadObjectTracerParameters.tracer)
                {
                    // we can't assert in here so save off the variables and assert later
                    finishOverloadObjectTracerParameters.tracerObject = tracerObject;
                    finishOverloadObjectTracerParameters.returnValue = returnValue;
                    finishOverloadObjectTracerParameters.exceptionObject = exceptionObject;

                    // set the flag indicating the tracer was called, we'll be asserting on that later
                    finishOverloadObjectTracerParameters.finishTracerCalled = true;
                }
                else if ((Guid)(tracerObject) == getOverloadStringTracerParameters.tracer)
                {
                    // we can't assert in here so save off the variables and assert later
                    finishOverloadStringTracerParameters.tracerObject = tracerObject;
                    finishOverloadStringTracerParameters.returnValue = returnValue;
                    finishOverloadStringTracerParameters.exceptionObject = exceptionObject;

                    // set the flag indicating the tracer was called, we'll be asserting on that later
                    finishOverloadStringTracerParameters.finishTracerCalled = true;
                }
            });

            // call the method that will be instrumented
            Object parameter = new Object();
            Assert.DoesNotThrow(() => { Overloaded(parameter); }, "Exception should not have been thrown.");

            var expectedOverloadObjectParameters = new object[] { parameter };
            var expectedOverloadStringParameters = new object[] { parameter.ToString() };
            var expectedOverloadObjectArgumentSignature = "System.Object";
            var expectedOverloadStringArgumentSignature = "System.String";

            ValidateTracers(getOverloadObjectTracerParameters, finishOverloadObjectTracerParameters, "Overloaded", expectedOverloadObjectArgumentSignature, this, expectedOverloadObjectParameters, null, null);
            ValidateTracers(getOverloadStringTracerParameters, finishOverloadStringTracerParameters, "Overloaded", expectedOverloadStringArgumentSignature, this, expectedOverloadStringParameters, null, null);
        }

        [Test]
        public void override_instrumented_method()
        {
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();

            var derivedClass = new DerivedClass();
            Assert.DoesNotThrow(() => { derivedClass.Foo(); }, "Exception should not have been thrown.");

            // make sure the code inside base class Foo and derived class Foo was executed once
            Assert.AreEqual(derivedClass.i, 1);
            Assert.AreEqual(derivedClass.j, 1);

            ValidateTracers(getTracerParameters, finishTracerParameters, "Foo", "", derivedClass, new object[] { }, null, null, "NewRelic.Agent.Tests.ProfiledMethods.BaseClass");
        }

        [Test]
        public void returns_value_class()
        {
            // setup the callbacks for GetTracer and FinishTracer
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();

            // call the method that will be instrumented
            var result = Guid.Empty;
            Assert.DoesNotThrow(() => { result = ReturnsValueClass(); }, "Exception should not have been thrown.");

            ValidateTracers(getTracerParameters, finishTracerParameters, "ReturnsValueClass", "", this, new object[] { }, result, null);
        }

        [Test]
        public void exceptions_retain_stack_information()
        {
            // setup the callbacks for GetTracer and FinishTracer
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();

            // call the method that will be instrumented
            MyException bubbledException = null;
            try
            {
                CallsUnProfiledThrowsException();
                Assert.Fail("Expected exception to be thrown.");
            }
            catch (MyException exception)
            {
                bubbledException = exception;
            }
            catch (Exception)
            {
                Assert.Fail("Expected MyException to be thrown.");
            }

            Assert.AreEqual("Void UnProfiledThrowsException()", bubbledException.TargetSite.ToString(), "Expected UnProfiledThrowsException at the top of the stack.");

            ValidateTracers(getTracerParameters, finishTracerParameters, "CallsUnProfiledThrowsException", "", this, new object[] { }, null, bubbledException);
        }

        [Test]
        public void huge_parameter_list_doesnt_blow_stack()
        {
            // setup the callbacks for GetTracer and FinishTracer
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();

            // call the method that will be instrumented
            Assert.DoesNotThrow(() => { LotsOfParameters(true, true, true, true, true, true, true, true, true, true, true, true, true, true); }, "Exception should not have been thrown.");

            var expectedParameters = new object[] { true, true, true, true, true, true, true, true, true, true, true, true, true, true };
            var expectedArgumentSignature = "System.Boolean,System.Boolean,System.Boolean,System.Boolean,System.Boolean,System.Boolean,System.Boolean,System.Boolean,System.Boolean,System.Boolean,System.Boolean,System.Boolean,System.Boolean,System.Boolean";
            ValidateTracers(getTracerParameters, finishTracerParameters, "LotsOfParameters", expectedArgumentSignature, this, expectedParameters, null, null);
        }

    }
}
