// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Core.Tracer;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core;

[TestFixture, Category("JustMock"), Category("MockingProfiler")]
public class AgentShimDistroTests
{
    [SetUp]
    public void SetUp()
    {
        AgentShim.ResetDistroStateForTesting();
    }

    [TearDown]
    public void TearDown()
    {
        AgentShim.ResetDistroStateForTesting();
    }

    [Test]
    public void DistroMode_BeforeRegistration_ReturnsNoOpAndNeverInitializes()
    {
        // If TryInitialize ran here, AgentInitializer.InitializeAgent() would build an agent-mode agent
        // and this test would fail or hang -- that is the regression this pins.
        AgentShim.EnterDistroMode();

        var result = AgentShim.GetFinishTracerDelegate("F", 0, "M", "A", typeof(object), "T", "Meth", "", null, new object[0], 1, null);

        Assert.That(result, Is.Not.Null);
        Assert.DoesNotThrow(() => result(null, null));

        var mock = Mock.Create<IAgentManager>();
        Mock.Arrange(() => mock.GetTracerImpl(Arg.AnyString, Arg.IsAny<uint>(), Arg.AnyString, Arg.AnyString, Arg.IsAny<Type>(), Arg.AnyString, Arg.AnyString, Arg.AnyString, Arg.IsAny<object>(), Arg.IsAny<object[]>(), Arg.IsAny<ulong>(), Arg.IsAny<Type>()))
            .Returns((ITracer)null);

        AgentShim.SetDistroAgentManager(mock);

        AgentShim.GetFinishTracerDelegate("F", 0, "M", "A", typeof(object), "T", "Meth", "", null, new object[0], 1, null);

        Mock.Assert(() => mock.GetTracerImpl(Arg.AnyString, Arg.IsAny<uint>(), Arg.AnyString, Arg.AnyString, Arg.IsAny<Type>(), Arg.AnyString, Arg.AnyString, Arg.AnyString, Arg.IsAny<object>(), Arg.IsAny<object[]>(), Arg.IsAny<ulong>(), Arg.IsAny<Type>()), Occurs.Once());
    }

    [Test]
    public void DistroMode_AfterRegistration_RoutesToRegisteredManager()
    {
        AgentShim.EnterDistroMode();

        var mock = Mock.Create<IAgentManager>();
        var tracer = Mock.Create<ITracer>();
        var type = typeof(object);
        var invocationTarget = new object();
        var args = new object[0];

        Mock.Arrange(() => mock.GetTracerImpl("F", 0u, "M", "A", type, "T", "Meth", "", invocationTarget, args, 1ul, null))
            .Returns(tracer);

        AgentShim.SetDistroAgentManager(mock);

        var result = AgentShim.GetFinishTracerDelegate("F", 0, "M", "A", type, "T", "Meth", "", invocationTarget, args, 1, null);
        result("ret", null);

        Mock.Assert(() => mock.GetTracerImpl("F", 0u, "M", "A", type, "T", "Meth", "", invocationTarget, args, 1ul, null), Occurs.Once());
        Mock.Assert(() => tracer.Finish("ret", null), Occurs.Once());
    }

    [Test]
    public void DistroMode_TracerImplReturnsNull_ReturnsNoOp()
    {
        AgentShim.EnterDistroMode();

        var mock = Mock.Create<IAgentManager>();
        Mock.Arrange(() => mock.GetTracerImpl(Arg.AnyString, Arg.IsAny<uint>(), Arg.AnyString, Arg.AnyString, Arg.IsAny<Type>(), Arg.AnyString, Arg.AnyString, Arg.AnyString, Arg.IsAny<object>(), Arg.IsAny<object[]>(), Arg.IsAny<ulong>(), Arg.IsAny<Type>()))
            .Returns((ITracer)null);

        AgentShim.SetDistroAgentManager(mock);

        var result = AgentShim.GetFinishTracerDelegate("F", 0, "M", "A", typeof(object), "T", "Meth", "", null, new object[0], 1, null);

        Assert.That(result, Is.Not.Null);
        Assert.DoesNotThrow(() => result(null, null));
    }

    [Test]
    public void DistroMode_AfterUnregister_UsesDisabledManagerNotAgentMode()
    {
        AgentShim.EnterDistroMode();

        var mock = Mock.Create<IAgentManager>();
        AgentShim.SetDistroAgentManager(mock);
        AgentShim.SetDistroAgentManager(null);

        var result = AgentShim.GetFinishTracerDelegate("F", 0, "M", "A", typeof(object), "T", "Meth", "", null, new object[0], 1, null);

        Assert.That(result, Is.Not.Null);
        Assert.DoesNotThrow(() => result(null, null));
        Mock.Assert(() => mock.GetTracerImpl(Arg.AnyString, Arg.IsAny<uint>(), Arg.AnyString, Arg.AnyString, Arg.IsAny<Type>(), Arg.AnyString, Arg.AnyString, Arg.AnyString, Arg.IsAny<object>(), Arg.IsAny<object[]>(), Arg.IsAny<ulong>(), Arg.IsAny<Type>()), Occurs.Never());
    }

    [Test]
    public void SetDistroAgentManager_WithoutDistroMode_Throws()
    {
        var mock = Mock.Create<IAgentManager>();

        Assert.Throws<InvalidOperationException>(() => AgentShim.SetDistroAgentManager(mock));
    }

    [Test]
    public void SetDistroAgentManager_NullWithoutDistroMode_IsNoOp()
    {
        // Ruling 1: a null unregister outside distro mode is a silent no-op (DistroAgentManager's
        // Shutdown/Dispose call SetDistroAgentManager(null) unconditionally). It must not throw and
        // must leave the shim's state untouched -- so a subsequent instrumented call on a defer-list
        // method still returns a no-op without booting an agent-mode agent.
        Assert.DoesNotThrow(() => AgentShim.SetDistroAgentManager(null));

        var result = AgentShim.GetFinishTracerDelegate("F", 0, "M", "A", typeof(object), "System.Net.Http.HttpClient", "SendAsync", "", null, new object[0], 1, null);

        Assert.That(result, Is.Not.Null);
        Assert.DoesNotThrow(() => result(null, null));
    }

    [Test]
    public void AgentMode_DeferredMethod_StillReturnsNoOpWithoutDistro()
    {
        // No EnterDistroMode: proves the agent-mode branch still reaches TryInitialize's defer-list
        // check -- and that the check alone is enough to avoid initialization -- without distro mode
        // involved at all.
        var result = AgentShim.GetFinishTracerDelegate("F", 0, "M", "A", typeof(object), "System.Net.Http.HttpClient", "SendAsync", "", null, new object[0], 1, null);

        Assert.That(result, Is.Not.Null);
        Assert.DoesNotThrow(() => result(null, null));
    }
}
