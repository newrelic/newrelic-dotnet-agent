// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;
using Telerik.JustMock;
using System.Collections.Generic;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Api;

namespace NewRelic.Agent.Core.Wrapper
{
    [TestFixture]
    public class Class_WrapperService
    {
        private const uint EmptyTracerArgs = 0;

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
            _wrapperService.BeforeWrappedMethod(type, methodName, string.Empty, target, arguments, tracerFactoryName, null, EmptyTracerArgs, 0);

            var method = new Method(type, methodName, string.Empty);
            var expectedMethodCall = new MethodCall(method, target, arguments, false);
            var instrumetedMethodInfo = new InstrumentedMethodInfo(0, expectedMethodCall.Method, tracerFactoryName, false, null, null, false);

            Mock.Assert(() => _wrapperMap.Get(instrumetedMethodInfo));
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

            var action = _wrapperService.BeforeWrappedMethod(type, methodName, string.Empty, target, arguments, tracerFactoryName, null, EmptyTracerArgs, 0);
            action(null, null);

            Assert.AreEqual("foo", result);
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

            var action = wrapperService.BeforeWrappedMethod(type, methodName, string.Empty, target, arguments, tracerFactoryName, null, EmptyTracerArgs, 0);
            action(null, null);

            Assert.AreEqual("foo", result);
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

            var action = wrapperService.BeforeWrappedMethod(type, methodName, string.Empty, target, arguments, tracerFactoryName, null, EmptyTracerArgs, 0);
            action(null, null);

            Assert.AreEqual("foo", result);
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

            Assert.Throws<Exception>(() => _wrapperService.BeforeWrappedMethod(type, methodName, string.Empty, target, arguments, tracerFactoryName, null, EmptyTracerArgs, 0));

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

            Assert.Throws<Exception>(() => wrapperService.BeforeWrappedMethod(type, methodName, argumentSignature, invocationTarget, arguments, tracerFactoryName, metricName, EmptyTracerArgs, 0));
            Assert.DoesNotThrow(() => wrapperService.BeforeWrappedMethod(type, methodName, argumentSignature, invocationTarget, arguments, tracerFactoryName, metricName, EmptyTracerArgs, 0));
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

            var afterWrappedMethod = _wrapperService.BeforeWrappedMethod(type, methodName, string.Empty, target, arguments, tracerFactoryName, null, EmptyTracerArgs, 0);
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

            var afterWrappedMethod1 = wrapperService.BeforeWrappedMethod(type, methodName, argumentSignature, invocationTarget, arguments, tracerFactoryName, metricName, EmptyTracerArgs, 0);
            Assert.Throws<Exception>(() => afterWrappedMethod1(null, null));

            var afterWrappedMethod2 = wrapperService.BeforeWrappedMethod(type, methodName, argumentSignature, invocationTarget, arguments, tracerFactoryName, metricName, EmptyTracerArgs, 0);
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
                var afterWrappedMethod = _wrapperService.BeforeWrappedMethod(type, methodName, string.Empty, target, arguments, tracerFactoryName, null, EmptyTracerArgs, 0);

                Assert.AreEqual(Delegates.NoOp, afterWrappedMethod, "AfterWrappedMethod was not the NoOp delegate.");
                Assert.False(logging.HasMessage("skipping method"));
            }
        }
    }
}
