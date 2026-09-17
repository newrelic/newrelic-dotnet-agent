// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Api;
using NewRelic.Agent.Core.ContinuousProfiling;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.TestUtilities;
using NUnit.Framework;
using Telerik.JustMock;

namespace CompositeTests;

[TestFixture]
public class WrapperServiceTests
{
    private static CompositeTestAgent _compositeTestAgent;
    private IAgent _agent;
    private const uint AttributeInstrumentation = 1 << 20;

    [SetUp]
    public void SetUp()
    {
        _compositeTestAgent = new CompositeTestAgent(false, true);
        _agent = _compositeTestAgent.GetAgent();
    }

    [TearDown]
    public static void TearDown()
    {
        _compositeTestAgent.Dispose();
    }

    [Test]
    public void BeforeWrappedMethod_ReturnsNoOp_IfTheRequiredTransactionIsFinished()
    {
        var transaction = _agent.CreateTransaction(true, "category", "name", true);
        transaction.End();
        _compositeTestAgent.SetTransactionOnPrimaryContextStorage(transaction);

        var type = typeof(WrapperServiceTests);
        var methodName = "MyMethod";
        var tracerFactoryName = "NewRelic.Agent.Core.Wrapper.DefaultWrapper";
        var target = new object();
        var arguments = new object[0];

        using (var logging = new Logging())
        {
            var wrapperService = _compositeTestAgent.GetWrapperService();
            var afterWrappedMethod = wrapperService.BeforeWrappedMethod(type, methodName, string.Empty, target, arguments, tracerFactoryName, null, AttributeInstrumentation, 0, null);

            Assert.Multiple(() =>
            {
                Assert.That(afterWrappedMethod, Is.EqualTo(Delegates.NoOp), "AfterWrappedMethod was not the NoOp delegate.");
                Assert.That(logging.HasMessageThatContains("Transaction has already ended, skipping method"), Is.True, "Expected log message was not found.");
            });
        }
    }

    [Test]
    public void EndingATransaction_ResetsTheContinuousProfilingContextOnTheCompletingThread()
    {
        // Bug A regression: a finished transaction must clear this thread's native trace/span context so a
        // later unrelated CPU sample on the (often pooled) thread is not misattributed to the finished span.
        var original = ContinuousProfilingContext.Instance;
        var cpContext = Mock.Create<IContinuousProfilingContext>();
        ContinuousProfilingContext.Instance = cpContext;
        try
        {
            var transaction = _agent.CreateTransaction(true, "category", "name", true);
            transaction.End();

            Mock.Assert(() => cpContext.ResetTraceContext(), Occurs.Once());
        }
        finally
        {
            ContinuousProfilingContext.Instance = original;
        }
    }

    [Test]
    public void EndingATransaction_RetiresItsMaterializedSegmentSpanIds()
    {
        // Bug B regression: a finished transaction's spans must be retired so a sample on ANY thread that
        // pushed one -- not just the completing thread -- stops being linked to the finished transaction.
        var original = ContinuousProfilingContext.Instance;
        var originalAnyEnabled = ContinuousProfilingContext.AnyEnabled;
        var cpContext = Mock.Create<IContinuousProfilingContext>();
        Mock.Arrange(() => cpContext.IsEnabled).Returns(true);
        IReadOnlyList<string> retired = null;
        Mock.Arrange(() => cpContext.RetireSpans(Arg.IsAny<IReadOnlyList<string>>()))
            .DoInstead((IReadOnlyList<string> ids) => retired = ids);
        ContinuousProfilingContext.Instance = cpContext;
        ContinuousProfilingContext.AnyEnabled = true;
        try
        {
            var transaction = _agent.CreateTransaction(true, "category", "name", true);
            var segment = _agent.StartTransactionSegmentOrThrow("segment");
            var spanId = segment.SpanId; // materialize it, exactly as a continuous-profiling push does
            segment.End();
            transaction.End();

            Assert.That(retired, Is.Not.Null);
            Assert.That(retired, Contains.Item(spanId));
        }
        finally
        {
            ContinuousProfilingContext.Instance = original;
            ContinuousProfilingContext.AnyEnabled = originalAnyEnabled;
        }
    }
}