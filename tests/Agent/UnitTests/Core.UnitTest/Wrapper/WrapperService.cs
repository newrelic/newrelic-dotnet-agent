// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Wrapper;

[TestFixture]
public class Class_WrapperService
{
    private const uint EmptyTracerArgs = 0;
    private const uint AsyncTracerArgs = 1 << 23;
    private const uint AttributeInstrumentation = 1 << 20;
    private const uint RuntimeAsyncTracerArgs = 1 << 19;

    private WrapperService _wrapperService;

    private IWrapperMap _wrapperMap;

    private IDefaultWrapper _defaultWrapper;

    private INoOpWrapper _noOpWrapper;

    private IWrapper _transactionRequiredWrapper;

    private IConfigurationService _configurationService;

    private IAgent _agent;

    private IAgentHealthReporter _agentHealthReporter;

    private IAgentTimerService _agentTimerService;

    [SetUp]
    public void SetUp()
    {
        _wrapperMap = Mock.Create<IWrapperMap>();
        _agent = Mock.Create<IAgent>();
        _configurationService = Mock.Create<IConfigurationService>();
        _agentHealthReporter = Mock.Create<IAgentHealthReporter>();
        _agentTimerService = Mock.Create<IAgentTimerService>();

        Mock.Arrange(() => _configurationService.Configuration.WrapperExceptionLimit).Returns(10);

        _defaultWrapper = Mock.Create<IDefaultWrapper>();
        _noOpWrapper = Mock.Create<INoOpWrapper>();

        _transactionRequiredWrapper = Mock.Create<IWrapper>();
        Mock.Arrange(() => _transactionRequiredWrapper.IsTransactionRequired).Returns(true);

        _wrapperService = new WrapperService(_configurationService, _wrapperMap, _agent, _agentHealthReporter, _agentTimerService);
    }

    [Test]
    public void BeforeWrappedMethod_PassesCorrectParametersToWrapperLoader()
    {
        Mock.Arrange(() => _wrapperMap.Get(Arg.IsAny<InstrumentedMethodInfo>())).Returns(() => new TrackedWrapper(Mock.Create<IWrapper>()));

        var type = typeof(Class_WrapperService);
        const string methodName = "MyMethod";
        const string tracerFactoryName = "MyTracer";
        var target = new object();
        var arguments = new object[0];
        _wrapperService.BeforeWrappedMethod(type, methodName, string.Empty, target, arguments, tracerFactoryName, null, AttributeInstrumentation, 0, null);

        var method = new Method(type, methodName, string.Empty);
        var expectedMethodCall = new MethodCall(method, target, arguments, false);
        var instrumetedMethodInfo = new InstrumentedMethodInfo(0, expectedMethodCall.Method, tracerFactoryName, false, null, null, false);

        Mock.Assert(() => _wrapperMap.Get(instrumetedMethodInfo));
        Mock.Assert(() => _agentHealthReporter.ReportLibraryVersion(Arg.IsAny<string>(), Arg.IsAny<string>()), Occurs.Once());
        Mock.Assert(() => _agentHealthReporter.ReportCustomInstrumentation(Arg.IsAny<string>(), Arg.IsAny<string>(), Arg.IsAny<string>()), Occurs.Once());
    }

    [Test]
    public void BeforeWrappedMethod_ReturnsSomethingSimilarToResultOfLazyMap()
    {
        var result = null as string;

        var wrapper = Mock.Create<IWrapper>();
        Mock.Arrange(() => wrapper.BeforeWrappedMethod(Arg.IsAny<InstrumentedMethodCall>(), Arg.IsAny<IAgent>(), Arg.IsAny<ITransaction>())).Returns((_, __) => result = "foo");
        Mock.Arrange(() => _wrapperMap.Get(Arg.IsAny<InstrumentedMethodInfo>())).Returns(new TrackedWrapper(wrapper));

        var type = typeof(Class_WrapperService);
        const string methodName = "MyMethod";
        const string tracerFactoryName = "MyTracer";
        var target = new object();
        var arguments = new object[0];

        var action = _wrapperService.BeforeWrappedMethod(type, methodName, string.Empty, target, arguments, tracerFactoryName, null, EmptyTracerArgs, 0, null);
        action(null, null);

        Assert.That(result, Is.EqualTo("foo"));
    }

    [Test]
    public void BeforeWrappedMethod_UsesDefaultWrapper_IfNoMatchingWrapper_ButDefaultWrapperCanWrapReturnsTrue()
    {
        var result = null as string;
        var wrapperMap = new WrapperMap(new List<IWrapper>(), _defaultWrapper, _noOpWrapper);

        Mock.Arrange(() => _defaultWrapper.CanWrap(Arg.IsAny<InstrumentedMethodInfo>())).Returns(new CanWrapResponse(true));
        Mock.Arrange(() => _defaultWrapper.BeforeWrappedMethod(Arg.IsAny<InstrumentedMethodCall>(), Arg.IsAny<IAgent>(), Arg.IsAny<ITransaction>())).Returns((_, __) => result = "foo");

        var type = typeof(Class_WrapperService);
        const string methodName = "MyMethod";
        const string tracerFactoryName = "MyTracer";
        var target = new object();
        var arguments = new object[0];

        var wrapperService = new WrapperService(_configurationService, wrapperMap, _agent, _agentHealthReporter, _agentTimerService);

        var action = wrapperService.BeforeWrappedMethod(type, methodName, string.Empty, target, arguments, tracerFactoryName, null, EmptyTracerArgs, 0, null);
        action(null, null);

        Assert.That(result, Is.EqualTo("foo"));
    }

    [Test]
    public void BeforeWrappedMethod_UsesNoOpWrapper_IfNoMatchingWrapper_AndDefaultWrapperCanWrapReturnsFalse()
    {
        string result = null;
        var wrapperMap = new WrapperMap(new List<IWrapper>(), _defaultWrapper, _noOpWrapper);
        Mock.Arrange(() => _noOpWrapper.BeforeWrappedMethod(Arg.IsAny<InstrumentedMethodCall>(), Arg.IsAny<IAgent>(), Arg.IsAny<ITransaction>())).Returns((_, __) => result = "foo");
        Mock.Arrange(() => _defaultWrapper.CanWrap(Arg.IsAny<InstrumentedMethodInfo>())).Returns(new CanWrapResponse(false));

        var type = typeof(Class_WrapperService);
        const string methodName = "MyMethod";
        const string tracerFactoryName = "MyTracer";
        var target = new object();
        var arguments = new object[0];

        var wrapperService = new WrapperService(_configurationService, wrapperMap, _agent, _agentHealthReporter, _agentTimerService);

        var action = wrapperService.BeforeWrappedMethod(type, methodName, string.Empty, target, arguments, tracerFactoryName, null, EmptyTracerArgs, 0, null);
        action(null, null);

        Assert.That(result, Is.EqualTo("foo"));
    }

    [Test]
    public void BeforeWrappedMethod_DoesNotSetNullOnFirstThrownException()
    {
        var wrapper = Mock.Create<IWrapper>();
        var trackedWrapper = new TrackedWrapper(wrapper);
        Mock.Arrange(() => wrapper.BeforeWrappedMethod(Arg.IsAny<InstrumentedMethodCall>(), Arg.IsAny<IAgent>(), Arg.IsAny<ITransaction>())).Throws(new Exception());
        Mock.Arrange(() => _wrapperMap.Get(Arg.IsAny<InstrumentedMethodInfo>())).Returns(trackedWrapper);

        var type = typeof(Class_WrapperService);
        const string methodName = "MyMethod";
        const string tracerFactoryName = "MyTracer";
        var target = new object();
        var arguments = new object[0];

        Assert.Throws<Exception>(() => _wrapperService.BeforeWrappedMethod(type, methodName, string.Empty, target, arguments, tracerFactoryName, null, EmptyTracerArgs, 0, null));

        Mock.Assert(_wrapperMap);
    }

#if NETFRAMEWORK //TODO: update this test to use something other than `System.Web.HttpApplication`
    [Test]
    public void BeforeWrappedMethod_SetsNoOpWhenThrowsExceptionTooManyTimes()
    {
        var wrapper = Mock.Create<IWrapper>();
        var trackedWrapper = new TrackedWrapper(wrapper);

        var wrapperMap = new WrapperMap(new List<IWrapper> { wrapper }, _defaultWrapper, _noOpWrapper);

        Mock.Arrange(() => wrapper.BeforeWrappedMethod(Arg.IsAny<InstrumentedMethodCall>(), Arg.IsAny<IAgent>(), Arg.IsAny<ITransaction>())).Throws(new Exception());
        Mock.Arrange(() => _configurationService.Configuration.WrapperExceptionLimit).Returns(1);
        Mock.Arrange(() => _noOpWrapper.BeforeWrappedMethod(Arg.IsAny<InstrumentedMethodCall>(), Arg.IsAny<IAgent>(), Arg.IsAny<ITransaction>())).OccursOnce();

        var type = typeof(System.Web.HttpApplication);
        const string methodName = "ExecuteStep";
        const string tracerFactoryName = "NewRelic.Agent.Core.Tracer.Factories.DefaultTracerFactory";
        var invocationTarget = new object();
        var arguments = new object[2];
        var argumentSignature = "IExecutionStep,System.Boolean&";
        var metricName = string.Empty;

        var method = new Method(type, methodName, argumentSignature);
        var methodCall = new MethodCall(method, invocationTarget, arguments, false);
        var info = new InstrumentedMethodInfo(0, methodCall.Method, tracerFactoryName, false, null, null, false);
        Mock.Arrange(() => wrapper.CanWrap(info)).Returns(new CanWrapResponse(true));

        var wrapperService = new WrapperService(_configurationService, wrapperMap, _agent, _agentHealthReporter, _agentTimerService);

        Assert.Throws<Exception>(() => wrapperService.BeforeWrappedMethod(type, methodName, argumentSignature, invocationTarget, arguments, tracerFactoryName, metricName, EmptyTracerArgs, 0, null));
        Assert.DoesNotThrow(() => wrapperService.BeforeWrappedMethod(type, methodName, argumentSignature, invocationTarget, arguments, tracerFactoryName, metricName, EmptyTracerArgs, 0, null));
        Mock.Assert(_noOpWrapper);
    }
#endif
    [Test]
    public void AfterWrappedMethod_DoesNotSetNullOnFirstThrownException()
    {
        var wrapper = Mock.Create<IWrapper>();
        var trackedWrapper = new TrackedWrapper(wrapper);
        Mock.Arrange(() => wrapper.BeforeWrappedMethod(Arg.IsAny<InstrumentedMethodCall>(), Arg.IsAny<IAgent>(), Arg.IsAny<ITransaction>())).Returns((result, exception) => { throw new Exception(); });
        Mock.Arrange(() => _wrapperMap.Get(Arg.IsAny<InstrumentedMethodInfo>())).Returns(trackedWrapper);

        var type = typeof(Class_WrapperService);
        const string methodName = "MyMethod";
        const string tracerFactoryName = "MyTracer";
        var target = new object();
        var arguments = new object[0];

        var afterWrappedMethod = _wrapperService.BeforeWrappedMethod(type, methodName, string.Empty, target, arguments, tracerFactoryName, null, EmptyTracerArgs, 0, null);
        Assert.Throws<Exception>(() => afterWrappedMethod(null, null));

        Mock.Assert(_wrapperMap);
    }

#if NETFRAMEWORK //TODO: update this test to use something other than `System.Web.HttpApplication`
    [Test]
    public void AfterWrappedMethod_SetsNoOpWhenThrowsExceptionTooManyTimes()
    {
        var wrapper = Mock.Create<IWrapper>();
        var trackedWrapper = new TrackedWrapper(wrapper);

        var wrapperMap = new WrapperMap(new List<IWrapper> { wrapper }, _defaultWrapper, _noOpWrapper);

        Mock.Arrange(() => wrapper.BeforeWrappedMethod(Arg.IsAny<InstrumentedMethodCall>(), Arg.IsAny<IAgent>(), Arg.IsAny<ITransaction>())).Returns((result, exception) => { throw new Exception(); });
        Mock.Arrange(() => _configurationService.Configuration.WrapperExceptionLimit).Returns(1);

        var type = typeof(System.Web.HttpApplication);
        const string methodName = "ExecuteStep";
        const string tracerFactoryName = "NewRelic.Agent.Core.Tracer.Factories.DefaultTracerFactory";
        var invocationTarget = new object();
        var arguments = new object[2];
        var argumentSignature = "IExecutionStep,System.Boolean&";
        var metricName = string.Empty;

        var method = new Method(type, methodName, argumentSignature);
        var methodCall = new MethodCall(method, invocationTarget, arguments, false);
        var info = new InstrumentedMethodInfo(0, methodCall.Method, tracerFactoryName, false, null, null, false);
        Mock.Arrange(() => wrapper.CanWrap(info)).Returns(new CanWrapResponse(true));

        var wrapperService = new WrapperService(_configurationService, wrapperMap, _agent, _agentHealthReporter, _agentTimerService);

        var afterWrappedMethod1 = wrapperService.BeforeWrappedMethod(type, methodName, argumentSignature, invocationTarget, arguments, tracerFactoryName, metricName, EmptyTracerArgs, 0, null);
        Assert.Throws<Exception>(() => afterWrappedMethod1(null, null));

        var afterWrappedMethod2 = wrapperService.BeforeWrappedMethod(type, methodName, argumentSignature, invocationTarget, arguments, tracerFactoryName, metricName, EmptyTracerArgs, 0, null);
        Assert.DoesNotThrow(() => afterWrappedMethod2(null, null));
    }
#endif

    [Test]
    public void BeforeWrappedMethod_ReturnsNoOp_IfTheCurrentSegmentIsLeaf()
    {
        Mock.Arrange(() => _wrapperMap.Get(Arg.IsAny<InstrumentedMethodInfo>())).Returns(new TrackedWrapper(_transactionRequiredWrapper));

        var transaction = Mock.Create<ITransaction>();
        var segment = Mock.Create<ISegment>();

        Mock.Arrange(() => transaction.IsValid).Returns(true);
        Mock.Arrange(() => transaction.CurrentSegment).Returns(segment);
        Mock.Arrange(() => segment.IsLeaf).Returns(true);
        Mock.Arrange(() => _agent.CurrentTransaction).Returns(transaction);


        var type = typeof(Class_WrapperService);
        const string methodName = "MyMethod";
        const string tracerFactoryName = "MyTracer";
        var target = new object();
        var arguments = new object[0];

        using (var logging = new TestUtilities.Logging())
        {
            var afterWrappedMethod = _wrapperService.BeforeWrappedMethod(type, methodName, string.Empty, target, arguments, tracerFactoryName, null, EmptyTracerArgs, 0, null);

            Assert.Multiple(() =>
            {
                Assert.That(afterWrappedMethod, Is.EqualTo(Delegates.NoOp), "AfterWrappedMethod was not the NoOp delegate.");
                Assert.That(logging.HasMessage("skipping method"), Is.False);
            });
        }
    }

    private class ValueTaskTestClass
    {
        public async ValueTask<int> SomeAsyncWork()
        {
            await Task.Delay(1);
            return 0;
        }
    }

    private class TaskTestClass
    {
        public async Task<int> SomeOtherAsyncWork()
        {
            await Task.Delay(1);
            return 0;
        }
    }

    [Test]
    public void BeforeWrappedMethod_WarningAboutValueTask()
    {
        Mock.Arrange(() => _wrapperMap.Get(Arg.IsAny<InstrumentedMethodInfo>())).Returns(new TrackedWrapper(_transactionRequiredWrapper));

        var type = typeof(ValueTaskTestClass);
        const string methodName = "SomeAsyncWork";
        const string tracerFactoryName = "MyTracer";
        var target = new object();
        var arguments = new object[0];

        using (var logging = new TestUtilities.Logging())
        {
            var afterWrappedMethod = _wrapperService.BeforeWrappedMethod(type, methodName, string.Empty, target, arguments, tracerFactoryName, null, AsyncTracerArgs, 0, null);

            Assert.That(logging.HasMessageThatContains("is not supported"), Is.True);
        }
    }
    [Test]
    public void BeforeWrappedMethod_NoWarningAboutTask()
    {
        Mock.Arrange(() => _wrapperMap.Get(Arg.IsAny<InstrumentedMethodInfo>())).Returns(new TrackedWrapper(_transactionRequiredWrapper));

        var type = typeof(TaskTestClass);
        const string methodName = "SomeOtherAsyncWork";
        const string tracerFactoryName = "MyTracer";
        var target = new object();
        var arguments = new object[0];

        using (var logging = new TestUtilities.Logging())
        {
            var afterWrappedMethod = _wrapperService.BeforeWrappedMethod(type, methodName, string.Empty, target, arguments, tracerFactoryName, null, AsyncTracerArgs, 0, null);

            Assert.That(logging.HasMessageThatContains("is not supported"), Is.False);
        }
    }

    // Runtime-async support tests

    // Test subjects for the runtime-async tests. Only the method NAME reaches the normalizer now --
    // the result type is supplied by the caller as effectiveReturnType -- so these stand in for
    // runtime-async methods without needing to be compiled as any particular shape.
    public Task<int> RuntimeAsyncTaskOfIntMethod() => Task.FromResult(0);

    public Task RuntimeAsyncTaskMethod() => Task.CompletedTask;

    [Test]
    public void BeforeWrappedMethod_TreatsClassifiableRuntimeAsyncMethodAsAsync()
    {
        InstrumentedMethodInfo capturedInfo = null;
        Mock.Arrange(() => _wrapperMap.Get(Arg.IsAny<InstrumentedMethodInfo>()))
            .Returns((InstrumentedMethodInfo info) =>
            {
                capturedInfo = info;
                return new TrackedWrapper(Mock.Create<IWrapper>());
            });

        _wrapperService.BeforeWrappedMethod(typeof(Class_WrapperService), nameof(RuntimeAsyncTaskOfIntMethod),
            string.Empty, new object(), new object[0], "MyTracer", null, RuntimeAsyncTracerArgs, 100, typeof(int));

        Assert.That(capturedInfo.IsAsync, Is.True);
    }

    [Test]
    public void BeforeWrappedMethod_DoesNotTreatRuntimeAsyncMethodAsAsync_WhenTheTypeCannotBeBound()
    {
        // void is not a valid generic argument, so no normalizer can be built. IsAsync must stay
        // false: it is a promise about the result slot, and nothing is going to fill it with a Task.
        InstrumentedMethodInfo capturedInfo = null;
        Mock.Arrange(() => _wrapperMap.Get(Arg.IsAny<InstrumentedMethodInfo>()))
            .Returns((InstrumentedMethodInfo info) =>
            {
                capturedInfo = info;
                return new TrackedWrapper(Mock.Create<IWrapper>());
            });

        _wrapperService.BeforeWrappedMethod(typeof(Class_WrapperService), nameof(RuntimeAsyncTaskOfIntMethod),
            string.Empty, new object(), new object[0], "MyTracer", null, RuntimeAsyncTracerArgs, 101, typeof(void));

        Assert.That(capturedInfo.IsAsync, Is.False);
    }

    [Test]
    public void BeforeWrappedMethod_NormalizesRuntimeAsyncResultToACompletedTask()
    {
        object capturedResult = null;
        ArrangeWrapperCapturingAfterDelegateResult(r => capturedResult = r);

        var afterWrappedMethod = _wrapperService.BeforeWrappedMethod(typeof(Class_WrapperService),
            nameof(RuntimeAsyncTaskOfIntMethod), string.Empty, new object(), new object[0],
            "MyTracer", null, RuntimeAsyncTracerArgs, 102, typeof(int));

        afterWrappedMethod(42, null);

        Assert.Multiple(() =>
        {
            Assert.That(capturedResult, Is.TypeOf<Task<int>>());
            Assert.That(((Task<int>)capturedResult).Result, Is.EqualTo(42));
            Assert.That(((Task<int>)capturedResult).IsCompleted, Is.True);
        });
    }

    [Test]
    public void BeforeWrappedMethod_NormalizesVoidEffectiveRuntimeAsyncResult()
    {
        object capturedResult = null;
        ArrangeWrapperCapturingAfterDelegateResult(r => capturedResult = r);

        var afterWrappedMethod = _wrapperService.BeforeWrappedMethod(typeof(Class_WrapperService),
            nameof(RuntimeAsyncTaskMethod), string.Empty, new object(), new object[0],
            "MyTracer", null, RuntimeAsyncTracerArgs, 103, null);

        afterWrappedMethod(null, null);

        Assert.That(capturedResult, Is.SameAs(Task.CompletedTask));
    }

    [Test]
    public void BeforeWrappedMethod_LeavesTheResultAlone_WhenAnExceptionWasThrown()
    {
        // exception != null routes to onFailure, which ends the segment synchronously. That is
        // already correct for runtime-async, so the result must not be rewritten.
        object capturedResult = null;
        var thrown = new InvalidOperationException("boom");
        ArrangeWrapperCapturingAfterDelegateResult(r => capturedResult = r);

        var afterWrappedMethod = _wrapperService.BeforeWrappedMethod(typeof(Class_WrapperService),
            nameof(RuntimeAsyncTaskOfIntMethod), string.Empty, new object(), new object[0],
            "MyTracer", null, RuntimeAsyncTracerArgs, 104, typeof(int));

        afterWrappedMethod(null, thrown);

        Assert.That(capturedResult, Is.Null);
    }

    [Test]
    public void BeforeWrappedMethod_LeavesTheResultAlone_WhenNotRuntimeAsync()
    {
        object capturedResult = null;
        ArrangeWrapperCapturingAfterDelegateResult(r => capturedResult = r);

        var afterWrappedMethod = _wrapperService.BeforeWrappedMethod(typeof(Class_WrapperService),
            nameof(RuntimeAsyncTaskOfIntMethod), string.Empty, new object(), new object[0],
            "MyTracer", null, EmptyTracerArgs, 105, typeof(int));

        afterWrappedMethod(42, null);

        Assert.That(capturedResult, Is.EqualTo(42));
    }

    [Test]
    public void BeforeWrappedMethod_PropagatesIsRuntimeAsyncToInstrumentedMethodInfo()
    {
        InstrumentedMethodInfo capturedInfo = null;
        Mock.Arrange(() => _wrapperMap.Get(Arg.IsAny<InstrumentedMethodInfo>()))
            .Returns((InstrumentedMethodInfo info) =>
            {
                capturedInfo = info;
                return new TrackedWrapper(Mock.Create<IWrapper>());
            });

        _wrapperService.BeforeWrappedMethod(typeof(Class_WrapperService), nameof(RuntimeAsyncTaskOfIntMethod),
            string.Empty, new object(), new object[0], "MyTracer", null, RuntimeAsyncTracerArgs, 106, typeof(int));

        Assert.That(capturedInfo.IsRuntimeAsync, Is.True);
    }

    [Test]
    public void BeforeWrappedMethod_IsRuntimeAsyncIsFalse_ForStateMachineAsyncMethods()
    {
        InstrumentedMethodInfo capturedInfo = null;
        Mock.Arrange(() => _wrapperMap.Get(Arg.IsAny<InstrumentedMethodInfo>()))
            .Returns((InstrumentedMethodInfo info) =>
            {
                capturedInfo = info;
                return new TrackedWrapper(Mock.Create<IWrapper>());
            });

        _wrapperService.BeforeWrappedMethod(typeof(Class_WrapperService), nameof(RuntimeAsyncTaskOfIntMethod),
            string.Empty, new object(), new object[0], "MyTracer", null, AsyncTracerArgs, 107, typeof(int));

        Assert.Multiple(() =>
        {
            Assert.That(capturedInfo.IsRuntimeAsync, Is.False);
            Assert.That(capturedInfo.IsAsync, Is.True);
        });
    }

    [Test]
    public void BeforeWrappedMethod_NormalizesEachInstantiationOfAGenericRuntimeAsyncMethod_ToItsOwnType()
    {
        // One functionId, two instantiations. The second call hits the functionId cache, so if the
        // normalizer were stored in that cache entry the string instantiation would be handed the
        // int delegate and its result would silently become default(int) wrapped in a Task<int>.
        object capturedResult = null;
        ArrangeWrapperCapturingAfterDelegateResult(r => capturedResult = r);

        var asInt = _wrapperService.BeforeWrappedMethod(typeof(Class_WrapperService),
            nameof(RuntimeAsyncTaskOfIntMethod), string.Empty, new object(), new object[0],
            "MyTracer", null, RuntimeAsyncTracerArgs, 200, typeof(int));

        asInt(42, null);
        var intResult = capturedResult;

        var asString = _wrapperService.BeforeWrappedMethod(typeof(Class_WrapperService),
            nameof(RuntimeAsyncTaskOfIntMethod), string.Empty, new object(), new object[0],
            "MyTracer", null, RuntimeAsyncTracerArgs, 200, typeof(string));

        asString("forty-two", null);
        var stringResult = capturedResult;

        Assert.Multiple(() =>
        {
            Assert.That(intResult, Is.TypeOf<Task<int>>());
            Assert.That(((Task<int>)intResult).Result, Is.EqualTo(42));
            Assert.That(stringResult, Is.TypeOf<Task<string>>());
            Assert.That(((Task<string>)stringResult).Result, Is.EqualTo("forty-two"));
        });
    }

    private void ArrangeWrapperCapturingAfterDelegateResult(Action<object> capture)
    {
        var wrapper = Mock.Create<IWrapper>();
        Mock.Arrange(() => wrapper.BeforeWrappedMethod(Arg.IsAny<InstrumentedMethodCall>(),
                Arg.IsAny<IAgent>(), Arg.IsAny<ITransaction>()))
            .Returns(new AfterWrappedMethodDelegate((result, _) => capture(result)));
        Mock.Arrange(() => _wrapperMap.Get(Arg.IsAny<InstrumentedMethodInfo>()))
            .Returns(new TrackedWrapper(wrapper));
    }
}
