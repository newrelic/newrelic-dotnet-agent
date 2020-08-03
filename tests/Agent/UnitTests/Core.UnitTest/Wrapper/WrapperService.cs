// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;
using Telerik.JustMock;

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
        private IConfigurationService _configurationService;
        private IAgentWrapperApi _agentWrapperApi;
        private IAgentHealthReporter _agentHealthReporter;

        [SetUp]
        public void SetUp()
        {
            _wrapperMap = Mock.Create<IWrapperMap>();
            _agentWrapperApi = Mock.Create<IAgentWrapperApi>();
            _configurationService = Mock.Create<IConfigurationService>();
            _agentHealthReporter = Mock.Create<IAgentHealthReporter>();

            Mock.Arrange(() => _configurationService.Configuration.WrapperExceptionLimit).Returns(10);

            _defaultWrapper = Mock.Create<IDefaultWrapper>();
            _noOpWrapper = Mock.Create<INoOpWrapper>();
            _wrapperService = new WrapperService(_configurationService, _wrapperMap, _agentWrapperApi, _agentHealthReporter);
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
            var expectedMethodCall = new MethodCall(method, target, arguments);
            var instrumetedMethodInfo = new InstrumentedMethodInfo(0, expectedMethodCall.Method, tracerFactoryName, false, null, null, false);

            Mock.Assert(() => _wrapperMap.Get(instrumetedMethodInfo));
        }

        [Test]
        public void BeforeWrappedMethod_ReturnsSomethingSimilarToResultOfLazyMap()
        {
            var result = null as string;

            var wrapper = Mock.Create<IWrapper>();
            Mock.Arrange(() => wrapper.BeforeWrappedMethod(Arg.IsAny<InstrumentedMethodCall>(), Arg.IsAny<IAgentWrapperApi>(), Arg.IsAny<ITransaction>())).Returns((_, __) => result = "foo");
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
            Mock.Arrange(() => _defaultWrapper.BeforeWrappedMethod(Arg.IsAny<InstrumentedMethodCall>(), Arg.IsAny<IAgentWrapperApi>(), Arg.IsAny<ITransaction>())).Returns((_, __) => result = "foo");

            var type = typeof(Class_WrapperService);
            const string methodName = "MyMethod";
            const string tracerFactoryName = "MyTracer";
            var target = new object();
            var arguments = new object[0];

            var wrapperService = new WrapperService(_configurationService, wrapperMap, _agentWrapperApi, _agentHealthReporter);

            var action = wrapperService.BeforeWrappedMethod(type, methodName, string.Empty, target, arguments, tracerFactoryName, null, EmptyTracerArgs, 0);
            action(null, null);

            Assert.AreEqual("foo", result);
        }

        [Test]
        public void BeforeWrappedMethod_UsesNoOpWrapper_IfNoMatchingWrapper_AndDefaultWrapperCanWrapReturnsFalse()
        {
            string result = null;
            var wrapperMap = new WrapperMap(new List<IWrapper>(), _defaultWrapper, _noOpWrapper);
            Mock.Arrange(() => _noOpWrapper.BeforeWrappedMethod(Arg.IsAny<InstrumentedMethodCall>(), Arg.IsAny<IAgentWrapperApi>(), Arg.IsAny<ITransaction>())).Returns((_, __) => result = "foo");
            Mock.Arrange(() => _defaultWrapper.CanWrap(Arg.IsAny<InstrumentedMethodInfo>())).Returns(new CanWrapResponse(false));

            var type = typeof(Class_WrapperService);
            const string methodName = "MyMethod";
            const string tracerFactoryName = "MyTracer";
            var target = new object();
            var arguments = new object[0];

            var wrapperService = new WrapperService(_configurationService, wrapperMap, _agentWrapperApi, _agentHealthReporter);

            var action = wrapperService.BeforeWrappedMethod(type, methodName, string.Empty, target, arguments, tracerFactoryName, null, EmptyTracerArgs, 0);
            action(null, null);

            Assert.AreEqual("foo", result);
        }

        [Test]
        public void BeforeWrappedMethod_DoesNotSetNullOnFirstThrownException()
        {
            var wrapper = Mock.Create<IWrapper>();
            var trackedWrapper = new TrackedWrapper(wrapper);
            Mock.Arrange(() => wrapper.BeforeWrappedMethod(Arg.IsAny<InstrumentedMethodCall>(), Arg.IsAny<IAgentWrapperApi>(), Arg.IsAny<ITransaction>())).Throws(new Exception());
            Mock.Arrange(() => _wrapperMap.Get(Arg.IsAny<InstrumentedMethodInfo>())).Returns(trackedWrapper);

            var type = typeof(Class_WrapperService);
            const string methodName = "MyMethod";
            const string tracerFactoryName = "MyTracer";
            var target = new object();
            var arguments = new object[0];

            Assert.Throws<Exception>(() => _wrapperService.BeforeWrappedMethod(type, methodName, string.Empty, target, arguments, tracerFactoryName, null, EmptyTracerArgs, 0));

            Mock.Assert(_wrapperMap);
        }

        [Test]
        public void BeforeWrappedMethod_SetsNoOpWhenThrowsExceptionTooManyTimes()
        {
            var wrapper = Mock.Create<IWrapper>();
            var trackedWrapper = new TrackedWrapper(wrapper);

            var wrapperMap = new WrapperMap(new List<IWrapper> { wrapper }, _defaultWrapper, _noOpWrapper);

            Mock.Arrange(() => wrapper.BeforeWrappedMethod(Arg.IsAny<InstrumentedMethodCall>(), Arg.IsAny<IAgentWrapperApi>(), Arg.IsAny<ITransaction>())).Throws(new Exception());
            Mock.Arrange(() => _configurationService.Configuration.WrapperExceptionLimit).Returns(1);
            Mock.Arrange(() => _noOpWrapper.BeforeWrappedMethod(Arg.IsAny<InstrumentedMethodCall>(), Arg.IsAny<IAgentWrapperApi>(), Arg.IsAny<ITransaction>())).OccursOnce();

            var type = typeof(System.Web.HttpApplication);
            const string methodName = "ExecuteStep";
            const string tracerFactoryName = "NewRelic.Agent.Core.Tracer.Factories.DefaultTracerFactory";
            var invocationTarget = new object();
            var arguments = new object[2];
            var argumentSignature = "IExecutionStep,System.Boolean&";
            var metricName = string.Empty;

            var method = new Method(type, methodName, argumentSignature);
            var methodCall = new MethodCall(method, invocationTarget, arguments);
            var info = new InstrumentedMethodInfo(0, methodCall.Method, tracerFactoryName, false, null, null, false);
            Mock.Arrange(() => wrapper.CanWrap(info)).Returns(new CanWrapResponse(true));

            var wrapperService = new WrapperService(_configurationService, wrapperMap, _agentWrapperApi, _agentHealthReporter);

            Assert.Throws<Exception>(() => wrapperService.BeforeWrappedMethod(type, methodName, argumentSignature, invocationTarget, arguments, tracerFactoryName, metricName, EmptyTracerArgs, 0));
            Assert.DoesNotThrow(() => wrapperService.BeforeWrappedMethod(type, methodName, argumentSignature, invocationTarget, arguments, tracerFactoryName, metricName, EmptyTracerArgs, 0));
            Mock.Assert(_noOpWrapper);
        }

        [Test]
        public void AfterWrappedMethod_DoesNotSetNullOnFirstThrownException()
        {
            var wrapper = Mock.Create<IWrapper>();
            var trackedWrapper = new TrackedWrapper(wrapper);
            Mock.Arrange(() => wrapper.BeforeWrappedMethod(Arg.IsAny<InstrumentedMethodCall>(), Arg.IsAny<IAgentWrapperApi>(), Arg.IsAny<ITransaction>())).Returns((result, exception) => { throw new Exception(); });
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

        [Test]
        public void AfterWrappedMethod_SetsNoOpWhenThrowsExceptionTooManyTimes()
        {
            var wrapper = Mock.Create<IWrapper>();
            var trackedWrapper = new TrackedWrapper(wrapper);

            var wrapperMap = new WrapperMap(new List<IWrapper> { wrapper }, _defaultWrapper, _noOpWrapper);

            Mock.Arrange(() => wrapper.BeforeWrappedMethod(Arg.IsAny<InstrumentedMethodCall>(), Arg.IsAny<IAgentWrapperApi>(), Arg.IsAny<ITransaction>())).Returns((result, exception) => { throw new Exception(); });
            Mock.Arrange(() => _configurationService.Configuration.WrapperExceptionLimit).Returns(1);

            var type = typeof(System.Web.HttpApplication);
            const string methodName = "ExecuteStep";
            const string tracerFactoryName = "NewRelic.Agent.Core.Tracer.Factories.DefaultTracerFactory";
            var invocationTarget = new object();
            var arguments = new object[2];
            var argumentSignature = "IExecutionStep,System.Boolean&";
            var metricName = string.Empty;

            var method = new Method(type, methodName, argumentSignature);
            var methodCall = new MethodCall(method, invocationTarget, arguments);
            var info = new InstrumentedMethodInfo(0, methodCall.Method, tracerFactoryName, false, null, null, false);
            Mock.Arrange(() => wrapper.CanWrap(info)).Returns(new CanWrapResponse(true));

            var wrapperService = new WrapperService(_configurationService, wrapperMap, _agentWrapperApi, _agentHealthReporter);

            var afterWrappedMethod1 = wrapperService.BeforeWrappedMethod(type, methodName, argumentSignature, invocationTarget, arguments, tracerFactoryName, metricName, EmptyTracerArgs, 0);
            Assert.Throws<Exception>(() => afterWrappedMethod1(null, null));

            var afterWrappedMethod2 = wrapperService.BeforeWrappedMethod(type, methodName, argumentSignature, invocationTarget, arguments, tracerFactoryName, metricName, EmptyTracerArgs, 0);
            Assert.DoesNotThrow(() => afterWrappedMethod2(null, null));
        }
    }
}
