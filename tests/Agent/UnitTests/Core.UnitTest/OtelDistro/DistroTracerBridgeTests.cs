// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Core.Tracer;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.OtelDistro;

[TestFixture, Category("JustMock"), Category("MockingProfiler")]
public class DistroTracerBridgeTests
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

    private static DistroInstrumentationSites ReadSites()
    {
        var read = InstrumentationDefinitionsTable.TryRead(InstrumentationDefinitionsTableTestData.GoldenTwoAssemblies, out _, out var sites, out _, out var reason);
        Assert.That(read, Is.True, reason);
        return sites;
    }

    [Test]
    public void Begin_NullArguments_PassesEmptyArray()
    {
        AgentShim.EnterDistroMode();

        var sites = ReadSites();
        var resolved = new DistroResolvedMethod(DistroResolutionOutcome.Resolved, false, 1ul, typeof(string), "System.Int32,System.String");
        var mock = Mock.Create<IAgentManager>();
        Mock.Arrange(() => mock.GetTracerImpl(Arg.AnyString, Arg.IsAny<uint>(), Arg.AnyString, Arg.AnyString, Arg.IsAny<Type>(), Arg.AnyString, Arg.AnyString, Arg.AnyString, Arg.IsAny<object>(), Arg.Matches<object[]>(a => a != null && a.Length == 0), Arg.IsAny<ulong>(), Arg.IsAny<Type>()))
            .Returns((ITracer)null);

        AgentShim.SetDistroAgentManager(mock);

        var result = DistroTracerBridge.Begin(sites, 2, resolved, "x", null);

        Assert.That(result, Is.Not.Null);
        Mock.Assert(() => mock.GetTracerImpl(Arg.AnyString, Arg.IsAny<uint>(), Arg.AnyString, Arg.AnyString, Arg.IsAny<Type>(), Arg.AnyString, Arg.AnyString, Arg.AnyString, Arg.IsAny<object>(), Arg.Matches<object[]>(a => a != null && a.Length == 0), Arg.IsAny<ulong>(), Arg.IsAny<Type>()), Occurs.Once());
    }

    [Test]
    public void Begin_ManagerThrows_DoesNotThrow()
    {
        AgentShim.EnterDistroMode();

        var sites = ReadSites();
        var resolved = new DistroResolvedMethod(DistroResolutionOutcome.Resolved, false, 1ul, typeof(string), "System.Int32,System.String");
        var mock = Mock.Create<IAgentManager>();
        Mock.Arrange(() => mock.GetTracerImpl(Arg.AnyString, Arg.IsAny<uint>(), Arg.AnyString, Arg.AnyString, Arg.IsAny<Type>(), Arg.AnyString, Arg.AnyString, Arg.AnyString, Arg.IsAny<object>(), Arg.IsAny<object[]>(), Arg.IsAny<ulong>(), Arg.IsAny<Type>()))
            .Throws(new InvalidOperationException("boom"));

        AgentShim.SetDistroAgentManager(mock);

        Assert.DoesNotThrow(() => DistroTracerBridge.Begin(sites, 2, resolved, "x", new object[0]));
    }

    [Test]
    public void Begin_ResolvedAsync_SetsAsyncBitAndPassesResolvedIdentity()
    {
        AgentShim.EnterDistroMode();

        var read = InstrumentationDefinitionsTable.TryRead(InstrumentationDefinitionsTableTestData.GoldenTwoAssemblies, out _, out var sites, out _, out var reason);
        Assert.That(read, Is.True, reason);
        sites.TryGet(2, out var site);
        var resolved = new DistroResolvedMethod(DistroResolutionOutcome.Resolved, true, 0xABCDEFul, typeof(DistroTracerBridgeTests), "Resolved.Signature");
        var expectedArguments = site.TracerArguments | (uint)TracerFlags.Async;

        var mock = Mock.Create<IAgentManager>();
        var tracer = Mock.Create<ITracer>();
        var instance = "x";
        var args = new object[] { 3, "y" };
        Mock.Arrange(() => mock.GetTracerImpl("TracerB", expectedArguments, "Custom/B", "AsmB", typeof(DistroTracerBridgeTests), "NS.TypeB", "MethodC", "Resolved.Signature", instance, args, 0xABCDEFul, null))
            .Returns(tracer);
        AgentShim.SetDistroAgentManager(mock);

        var result = DistroTracerBridge.Begin(sites, 2, resolved, instance, args);

        Assert.That(result, Is.Not.Null);
        Assert.That(TracerArgument.IsAsync(expectedArguments), Is.True);
        Mock.Assert(() => mock.GetTracerImpl("TracerB", expectedArguments, "Custom/B", "AsmB", typeof(DistroTracerBridgeTests), "NS.TypeB", "MethodC", "Resolved.Signature", instance, args, 0xABCDEFul, null), Occurs.Once());
    }

    [Test]
    public void Begin_ResolvedNotAsync_LeavesTracerArgumentsAsTheSiteHasThem()
    {
        AgentShim.EnterDistroMode();

        var read = InstrumentationDefinitionsTable.TryRead(InstrumentationDefinitionsTableTestData.GoldenTwoAssemblies, out _, out var sites, out _, out var reason);
        Assert.That(read, Is.True, reason);
        sites.TryGet(2, out var site);
        var resolved = new DistroResolvedMethod(DistroResolutionOutcome.Resolved, false, 0xABCDEFul, typeof(DistroTracerBridgeTests), "Resolved.Signature");

        var mock = Mock.Create<IAgentManager>();
        var tracer = Mock.Create<ITracer>();
        Mock.Arrange(() => mock.GetTracerImpl("TracerB", site.TracerArguments, "Custom/B", "AsmB", typeof(DistroTracerBridgeTests), "NS.TypeB", "MethodC", "Resolved.Signature", Arg.IsAny<object>(), Arg.IsAny<object[]>(), 0xABCDEFul, null))
            .Returns(tracer);
        AgentShim.SetDistroAgentManager(mock);

        var result = DistroTracerBridge.Begin(sites, 2, resolved, "x", new object[0]);

        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public void Begin_Unresolved_PassesSiteValues()
    {
        AgentShim.EnterDistroMode();

        var read = InstrumentationDefinitionsTable.TryRead(InstrumentationDefinitionsTableTestData.GoldenTwoAssemblies, out _, out var sites, out _, out var reason);
        Assert.That(read, Is.True, reason);
        sites.TryGet(2, out var site);
        var resolved = new DistroResolvedMethod(DistroResolutionOutcome.NotFound, false, site.FunctionId, typeof(string), site.ArgumentSignature);

        var mock = Mock.Create<IAgentManager>();
        var tracer = Mock.Create<ITracer>();
        Mock.Arrange(() => mock.GetTracerImpl("TracerB", 0u, "Custom/B", "AsmB", typeof(string), "NS.TypeB", "MethodC", "System.Int32,System.String", "x", Arg.IsAny<object[]>(), 1UL, null))
            .Returns(tracer);
        AgentShim.SetDistroAgentManager(mock);

        var result = DistroTracerBridge.Begin(sites, 2, resolved, "x", new object[] { 3, "y" });

        Assert.That(result, Is.Not.Null);
        Assert.That(TracerArgument.IsAsync(site.TracerArguments), Is.False);
    }

    [Test]
    public void Begin_NullResolved_DoesNotThrow()
    {
        AgentShim.EnterDistroMode();

        var read = InstrumentationDefinitionsTable.TryRead(InstrumentationDefinitionsTableTestData.GoldenTwoAssemblies, out _, out var sites, out _, out var reason);
        Assert.That(read, Is.True, reason);
        var mock = Mock.Create<IAgentManager>();
        AgentShim.SetDistroAgentManager(mock);

        Action<object, Exception> result = null;
        Assert.DoesNotThrow(() => result = DistroTracerBridge.Begin(sites, 2, (DistroResolvedMethod)null, "x", new object[0]));

        Assert.That(result, Is.Null);
        Mock.Assert(() => mock.GetTracerImpl(Arg.AnyString, Arg.IsAny<uint>(), Arg.AnyString, Arg.AnyString, Arg.IsAny<Type>(), Arg.AnyString, Arg.AnyString, Arg.AnyString, Arg.IsAny<object>(), Arg.IsAny<object[]>(), Arg.IsAny<ulong>(), Arg.IsAny<Type>()), Occurs.Never());
    }

    [Test]
    public void StartedTracer_NullAndNoOp_False_RealDelegate_True()
    {
        AgentShim.EnterDistroMode(); // distro mode, no manager registered -> GetFinishTracerDelegate returns the shared no-op

        var noOp = AgentShim.GetFinishTracerDelegate("f", 0u, null, "asm", typeof(string), "type", "method", string.Empty, new object(), new object[0], 1ul, null);

        Assert.Multiple(() =>
        {
            Assert.That(noOp, Is.Not.Null, "AgentShim never returns null");
            Assert.That(AgentShim.IsNoOpFinishTracer(noOp), Is.True);
            Assert.That(DistroTracerBridge.StartedTracer(null), Is.False);
            Assert.That(DistroTracerBridge.StartedTracer(noOp), Is.False);
            Assert.That(DistroTracerBridge.StartedTracer((Action<object, Exception>)((a, b) => { })), Is.True);
        });
    }

    [Test]
    public void Begin_Resolved_NullSites_ReturnsNull()
    {
        var resolved = new DistroResolvedMethod(DistroResolutionOutcome.Resolved, true, 1ul, typeof(string), string.Empty);

        Action<object, Exception> result = null;
        Assert.DoesNotThrow(() => result = DistroTracerBridge.Begin(null, 0, resolved, "x", new object[0]));
        Assert.That(result, Is.Null);
    }

    [TestCase(-1)]
    [TestCase(3)]
    public void Begin_Resolved_SiteIndexOutOfRange_ReturnsNull(int siteIndex)
    {
        var read = InstrumentationDefinitionsTable.TryRead(InstrumentationDefinitionsTableTestData.GoldenTwoAssemblies, out _, out var sites, out _, out var reason);
        Assert.That(read, Is.True, reason);
        var resolved = new DistroResolvedMethod(DistroResolutionOutcome.Resolved, true, 1ul, typeof(string), string.Empty);

        Action<object, Exception> result = null;
        Assert.DoesNotThrow(() => result = DistroTracerBridge.Begin(sites, siteIndex, resolved, "x", new object[0]));
        Assert.That(result, Is.Null);
    }

    [Test]
    public void End_InvokesDelegateWithReturnAndException()
    {
        object capturedReturn = null;
        Exception capturedException = null;
        Action<object, Exception> finishDelegate = (ret, ex) =>
        {
            capturedReturn = ret;
            capturedException = ex;
        };
        var exception = new Exception("test");

        DistroTracerBridge.End(finishDelegate, "ret-val", exception);

        Assert.That(capturedReturn, Is.EqualTo("ret-val"));
        Assert.That(capturedException, Is.SameAs(exception));
    }

    [Test]
    public void End_NullDelegate_DoesNothing()
    {
        Assert.DoesNotThrow(() => DistroTracerBridge.End(null, "ret", null));
    }

    [Test]
    public void End_NonDelegateObject_DoesNothing()
    {
        Assert.DoesNotThrow(() => DistroTracerBridge.End("not a delegate", "ret", null));
    }

    [Test]
    public void End_DelegateThrows_Swallowed()
    {
        Action<object, Exception> finishDelegate = (ret, ex) => throw new InvalidOperationException("boom");

        Assert.DoesNotThrow(() => DistroTracerBridge.End(finishDelegate, "ret", null));
    }
}
