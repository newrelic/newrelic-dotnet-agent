// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Reflection;
using NewRelic.Agent.Core.Tracer;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.AgentShimTests;

[TestFixture, Category("JustMock"), Category("MockingProfiler")]
public class FinishTracerTests
{
    private TestUtilities.Logging _logger;

    [OneTimeSetUp]
    public void TestFixtureSetUp()
    {
        var propInfo = typeof(AgentInitializer)
            .GetProperty("InitializeAgent", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        propInfo.SetValue(null, new Action(() => { }));

        // Force early static initialization of AgentShim by interacting with it in some arbitrary way
        AgentInitializer.OnExit += (_, __) => { };
    }

    [SetUp]
    public void SetUp()
    {
        _logger = new TestUtilities.Logging();
    }

    [TearDown]
    public void TearDown()
    {
        _logger.Dispose();
    }

    [Test]
    public void returns_null_when_tracer_object_is_null()
    {
        // ARRANGE
        object tracer = null;

        // ACT
        AgentShim.FinishTracer(tracer, null, null);

        // ASSERT
        Assert.That(_logger.MessageCount, Is.EqualTo(0), "Expected no log entries but got: " + _logger.ToString());
    }

    [Test]
    public void does_not_throw_when_tracer_object_is_not_an_ITracer()
    {
        object tracer = new object();
        Assert.DoesNotThrow(() => AgentShim.FinishTracer(tracer, null, null));
    }

    [Test]
    public void returns_without_calling_finish_when_exception_object_is_not_an_exception()
    {
        // ARRANGE
        var tracer = Mock.Create<ITracer>(Behavior.Strict);
        Mock.Arrange(() => tracer.Finish(Arg.AnyObject, Arg.IsAny<Exception>())).OccursNever();
        var exception = new object();

        // ACT
        AgentShim.FinishTracer(tracer, null, exception);

        // ASSERT
        Mock.Assert(tracer);
    }

    [Test]
    public void calls_Finish_on_tracer_with_null_return_and_exception()
    {
        // ARRANGE
        var tracer = Mock.Create<ITracer>(Behavior.Strict);
        var retrn = null as object;
        var exception = null as Exception;
        Mock.Arrange(() => tracer.Finish(retrn, exception)).OccursOnce();

        // ACT
        AgentShim.FinishTracer(tracer, retrn, exception);

        // ASSERT
        Mock.Assert(tracer);
    }

    [Test]
    public void calls_Finish_on_tracer_with_return_passed_through()
    {
        // ARRANGE
        var tracer = Mock.Create<ITracer>(Behavior.Strict);
        var retrn = new object();
        var exception = null as Exception;
        Mock.Arrange(() => tracer.Finish(retrn, exception)).OccursOnce();

        // ACT
        AgentShim.FinishTracer(tracer, retrn, exception);

        // ASSERT
        Mock.Assert(tracer);
    }

    [Test]
    public void calls_Finish_on_tracer_with_exception_passed_through()
    {
        // ARRANGE
        var tracer = Mock.Create<ITracer>(Behavior.Strict);
        var retrn = null as object;
        var exception = new Exception();
        Mock.Arrange(() => tracer.Finish(retrn, exception)).OccursOnce();

        // ACT
        AgentShim.FinishTracer(tracer, retrn, exception);

        // ASSERT
        Mock.Assert(tracer);
    }

    [Test]
    public void Exception_does_not_bubble_up_when_thrown_from_FinishTracerImpl()
    {
        var tracer = Mock.Create<ITracer>(Behavior.Strict);
        Mock.Arrange(() => tracer.Finish(null as object, null as Exception)).Throws(new Exception());
        Assert.DoesNotThrow(() => AgentShim.FinishTracer(tracer, null, null));
    }
}

[TestFixture, Category("JustMock"), Category("MockingProfiler")]
public class GetFinishTracerDelegateFuncTests
{
    [OneTimeSetUp]
    public void TestFixtureSetUp()
    {
        var propInfo = typeof(AgentInitializer)
            .GetProperty("InitializeAgent", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        propInfo.SetValue(null, new Action(() => { }));

        // Force early static initialization of AgentShim by interacting with it in some arbitrary way
        AgentInitializer.OnExit += (_, __) => { };
    }

    [Test]
    public void returns_non_null()
    {
        var result = AgentShim.GetFinishTracerDelegateFunc();

        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public void returns_exactly_a_func_of_object_array_to_action_of_object_and_exception()
    {
        // The profiler's injected bytecode casts the returned object to this exact delegate
        // type. If the cast target here, or the signature of the wrapper it points at, ever
        // drift apart, this fails here at test time instead of at runtime in a customer
        // process with an unhandled InvalidCastException.
        var result = AgentShim.GetFinishTracerDelegateFunc();

        Assert.That(result, Is.InstanceOf<Func<object[], Action<object, Exception>>>());
    }

    [Test]
    public void returned_delegate_targets_GetFinishTracerDelegateParameterWrapper()
    {
        var func = (Func<object[], Action<object, Exception>>)AgentShim.GetFinishTracerDelegateFunc();

        // Exact identity check on the delegate's target method, rather than inferring it from
        // matching runtime behavior (which would only prove coincidental equivalence, not
        // identity: any method that dereferences its argument would satisfy that).
        var expectedMethod = typeof(AgentShim).GetMethod(
            nameof(AgentShim.GetFinishTracerDelegateParameterWrapper), BindingFlags.Public | BindingFlags.Static);

        Assert.That(func.Method, Is.EqualTo(expectedMethod));
    }

    [Test]
    public void invoking_returned_delegate_with_null_parameters_throws_NullReferenceException()
    {
        // Documents observed behavior for a null parameters array. This is not a proof of
        // delegate identity -- see returned_delegate_targets_GetFinishTracerDelegateParameterWrapper
        // above for that.
        var func = (Func<object[], Action<object, Exception>>)AgentShim.GetFinishTracerDelegateFunc();

        Assert.Throws<NullReferenceException>(() => func(null));
    }
}

[TestFixture, Category("JustMock"), Category("MockingProfiler")]
public class GetFinishTracerDelegateParameterWrapperTests
{
    [OneTimeSetUp]
    public void TestFixtureSetUp()
    {
        var propInfo = typeof(AgentInitializer)
            .GetProperty("InitializeAgent", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        propInfo.SetValue(null, new Action(() => { }));

        // Force early static initialization of AgentShim by interacting with it in some arbitrary way
        AgentInitializer.OnExit += (_, __) => { };
    }

    // A well-formed 11-element array matching the positional contract that
    // GetFinishTracerDelegateParameterWrapper expects: index 8 (invocationTarget) is passed
    // through with no cast, so any value is valid there and it is not covered by a
    // wrong-type test below.
    private static object[] BuildValidParameters()
    {
        return new object[]
        {
            "tracerFactoryName",   // 0 string
            (uint)1,                // 1 uint
            "metricName",           // 2 string
            "assemblyName",         // 3 string
            typeof(object),         // 4 Type
            "typeName",             // 5 string
            "methodName",           // 6 string
            "argumentSignature",    // 7 string
            new object(),           // 8 object (invocationTarget, no cast applied)
            new object[0],          // 9 object[]
            (ulong)1                // 10 ulong
        };
    }

    [Test]
    public void well_formed_parameters_array_does_not_throw_and_returns_invokable_delegate()
    {
        var parameters = BuildValidParameters();

        // IgnoreWork suppresses AgentShim's re-entry into GetTracer before it ever reaches
        // AgentManager.Instance, so this exercises only the parameter-unboxing contract
        // without triggering a real, one-time-only agent bootstrap in this test process.
        Action<object, Exception> result;
        using (new IgnoreWork())
        {
            result = AgentShim.GetFinishTracerDelegateParameterWrapper(parameters);
        }

        Assert.That(result, Is.Not.Null);
        Assert.DoesNotThrow(() => result(null, null));
    }

    [Test]
    public void throws_when_parameters_array_is_too_short()
    {
        Assert.Throws<IndexOutOfRangeException>(() => AgentShim.GetFinishTracerDelegateParameterWrapper(new object[0]));
    }

    [Test]
    public void throws_when_parameter_0_tracerFactoryName_is_wrong_type()
    {
        var parameters = BuildValidParameters();
        parameters[0] = 42;

        Assert.Throws<InvalidCastException>(() => AgentShim.GetFinishTracerDelegateParameterWrapper(parameters));
    }

    [Test]
    public void throws_when_parameter_1_tracerArguments_is_wrong_type()
    {
        var parameters = BuildValidParameters();
        parameters[1] = 42;

        Assert.Throws<InvalidCastException>(() => AgentShim.GetFinishTracerDelegateParameterWrapper(parameters));
    }

    [Test]
    public void throws_when_parameter_2_metricName_is_wrong_type()
    {
        var parameters = BuildValidParameters();
        parameters[2] = 42;

        Assert.Throws<InvalidCastException>(() => AgentShim.GetFinishTracerDelegateParameterWrapper(parameters));
    }

    [Test]
    public void throws_when_parameter_3_assemblyName_is_wrong_type()
    {
        var parameters = BuildValidParameters();
        parameters[3] = 42;

        Assert.Throws<InvalidCastException>(() => AgentShim.GetFinishTracerDelegateParameterWrapper(parameters));
    }

    [Test]
    public void throws_when_parameter_4_type_is_wrong_type()
    {
        var parameters = BuildValidParameters();
        parameters[4] = 42;

        Assert.Throws<InvalidCastException>(() => AgentShim.GetFinishTracerDelegateParameterWrapper(parameters));
    }

    [Test]
    public void throws_when_parameter_5_typeName_is_wrong_type()
    {
        var parameters = BuildValidParameters();
        parameters[5] = 42;

        Assert.Throws<InvalidCastException>(() => AgentShim.GetFinishTracerDelegateParameterWrapper(parameters));
    }

    [Test]
    public void throws_when_parameter_6_methodName_is_wrong_type()
    {
        var parameters = BuildValidParameters();
        parameters[6] = 42;

        Assert.Throws<InvalidCastException>(() => AgentShim.GetFinishTracerDelegateParameterWrapper(parameters));
    }

    [Test]
    public void throws_when_parameter_7_argumentSignature_is_wrong_type()
    {
        var parameters = BuildValidParameters();
        parameters[7] = 42;

        Assert.Throws<InvalidCastException>(() => AgentShim.GetFinishTracerDelegateParameterWrapper(parameters));
    }

    [Test]
    public void throws_when_parameter_9_args_is_wrong_type()
    {
        var parameters = BuildValidParameters();
        parameters[9] = 42;

        Assert.Throws<InvalidCastException>(() => AgentShim.GetFinishTracerDelegateParameterWrapper(parameters));
    }

    [Test]
    public void throws_when_parameter_10_functionId_is_wrong_type()
    {
        var parameters = BuildValidParameters();
        parameters[10] = 42;

        Assert.Throws<InvalidCastException>(() => AgentShim.GetFinishTracerDelegateParameterWrapper(parameters));
    }
}
