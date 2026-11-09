// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using NewRelic.Agent.Api;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.OpenTelemetryBridge.Tracing;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Segments.Tests;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.OtelDistro;

[TestFixture]
public class DistroActivitySinkTests
{
    private const int KindInternal = 0;
    private const int KindServer = 1;
    private const int KindClient = 2;
    private const int KindConsumer = 4;

    private IAgent _mockAgent;
    private IErrorService _mockErrorService;
    private IConfiguration _mockConfig;
    private ITransaction _mockTransaction;
    private DistroActivitySink _sink;

    [SetUp]
    public void SetUp()
    {
        _mockAgent = Mock.Create<IAgent>();
        _mockErrorService = Mock.Create<IErrorService>();
        _mockConfig = Mock.Create<IConfiguration>();
        _mockTransaction = Mock.Create<ITransaction>();

        Mock.Arrange(() => _mockAgent.Configuration).Returns(_mockConfig);
        Mock.Arrange(() => _mockAgent.CurrentTransaction).Returns(_mockTransaction);
        Mock.Arrange(() => _mockTransaction.IsValid).Returns(false);
        Mock.Arrange(() => _mockTransaction.IsFinished).Returns(false);

        Mock.Arrange(() => _mockConfig.OpenTelemetryTracingDefaultExcludedActivitySources).Returns(new List<string> { "Default.Excluded" });
        Mock.Arrange(() => _mockConfig.OpenTelemetryTracingIncludedActivitySources).Returns(new List<string>());
        Mock.Arrange(() => _mockConfig.OpenTelemetryTracingExcludedActivitySources).Returns(new List<string> { "Customer.Excluded" });

        _sink = new DistroActivitySink(_mockAgent, _mockErrorService);
    }

    [Test]
    public void ShouldSample_WhenTransactionInProgress_ReturnsTrue()
    {
        Mock.Arrange(() => _mockTransaction.IsValid).Returns(true);
        Mock.Arrange(() => _mockTransaction.IsFinished).Returns(false);

        Assert.That(_sink.ShouldSample(KindInternal, parentIsRemote: false), Is.True);
    }

    [TestCase(KindServer)]
    [TestCase(KindConsumer)]
    public void ShouldSample_ServerOrConsumerKind_ReturnsTrue(int kind)
    {
        Assert.That(_sink.ShouldSample(kind, parentIsRemote: false), Is.True);
    }

    [Test]
    public void ShouldSample_RemoteParent_ReturnsTrue()
    {
        Assert.That(_sink.ShouldSample(KindClient, parentIsRemote: true), Is.True);
    }

    [TestCase(KindInternal)]
    [TestCase(KindClient)]
    public void ShouldSample_LocalInternalOrClientWithoutTransaction_ReturnsFalse(int kind)
    {
        Assert.That(_sink.ShouldSample(kind, parentIsRemote: false), Is.False);
    }

    [Test]
    public void ShouldSample_WhenCurrentTransactionThrows_ReturnsFalse()
    {
        Mock.Arrange(() => _mockAgent.CurrentTransaction).Throws(new InvalidOperationException("boom"));

        var result = true;
        Assert.DoesNotThrow(() => result = _sink.ShouldSample(KindServer, parentIsRemote: true));
        Assert.That(result, Is.False);
    }

    [Test]
    public void ShouldSample_WhenTransactionValidButFinished_FallsThroughToKindCheck()
    {
        Mock.Arrange(() => _mockTransaction.IsValid).Returns(true);
        Mock.Arrange(() => _mockTransaction.IsFinished).Returns(true);

        Assert.Multiple(() =>
        {
            Assert.That(_sink.ShouldSample(KindInternal, parentIsRemote: false), Is.False);
            Assert.That(_sink.ShouldSample(KindServer, parentIsRemote: false), Is.True);
        });
    }

    [Test]
    public void ShouldSample_WhenCurrentTransactionThrowsRepeatedly_LogsErrorOnce()
    {
        Mock.Arrange(() => _mockAgent.CurrentTransaction).Throws(new InvalidOperationException("sample-boom"));

        using (var logging = new TestUtilities.Logging())
        {
            Assert.DoesNotThrow(() => _sink.ShouldSample(KindServer, parentIsRemote: false));
            Assert.DoesNotThrow(() => _sink.ShouldSample(KindServer, parentIsRemote: false));

            Assert.Multiple(() =>
            {
                Assert.That(logging.ErrorCount, Is.EqualTo(1));
                Assert.That(logging.HasErrorMessageThatContains("sample-boom"), Is.True);
            });
        }
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("\t")]
    public void ShouldListenTo_NullEmptyOrWhiteSpaceName_ReturnsFalse(string name)
    {
        Assert.That(_sink.ShouldListenTo(name), Is.False);
    }

    [Test]
    public void ShouldListenTo_DelegatesToActivitySourceExtensions()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_sink.ShouldListenTo("Customer.Excluded"), Is.False);
            Assert.That(_sink.ShouldListenTo("Default.Excluded"), Is.False);
            Assert.That(_sink.ShouldListenTo("Some.Other.Source"), Is.True);
        });
    }

    [Test]
    public void ShouldListenTo_WhenConfigurationThrows_ReturnsFalse()
    {
        Mock.Arrange(() => _mockAgent.Configuration).Throws(new InvalidOperationException("boom"));

        var result = true;
        Assert.DoesNotThrow(() => result = _sink.ShouldListenTo("Some.Other.Source"));
        Assert.That(result, Is.False);
    }

    [Test]
    public void ShouldListenTo_WhenConfigurationThrowsRepeatedly_LogsErrorOnce()
    {
        Mock.Arrange(() => _mockAgent.Configuration).Throws(new InvalidOperationException("listen-boom"));

        using (var logging = new TestUtilities.Logging())
        {
            Assert.DoesNotThrow(() => _sink.ShouldListenTo("Some.Other.Source"));
            Assert.DoesNotThrow(() => _sink.ShouldListenTo("Another.Source"));

            Assert.Multiple(() =>
            {
                Assert.That(logging.ErrorCount, Is.EqualTo(1));
                Assert.That(logging.HasErrorMessageThatContains("listen-boom"), Is.True);
            });
        }
    }

    [TestCase(true, true, true)]
    [TestCase(true, false, false)]
    [TestCase(false, true, false)]
    [TestCase(false, false, false)]
    public void TracingEnabled_RequiresOpenTelemetryAndTracingEnabled(bool openTelemetryEnabled, bool tracingEnabled, bool expected)
    {
        Mock.Arrange(() => _mockConfig.OpenTelemetryEnabled).Returns(openTelemetryEnabled);
        Mock.Arrange(() => _mockConfig.OpenTelemetryTracingEnabled).Returns(tracingEnabled);

        Assert.That(_sink.TracingEnabled, Is.EqualTo(expected));
    }

    [Test]
    public void Disable_SetsIsDisabled()
    {
        Assert.That(_sink.IsDisabled, Is.False);

        _sink.Disable();
        _sink.Disable();

        Assert.That(_sink.IsDisabled, Is.True);
    }

    [Test]
    public void TracingEnabled_WhenDisabled_ReturnsFalseWithoutReadingConfiguration()
    {
        Mock.Arrange(() => _mockConfig.OpenTelemetryEnabled).Returns(true);
        Mock.Arrange(() => _mockConfig.OpenTelemetryTracingEnabled).Returns(true);
        _sink.Disable();

        Assert.That(_sink.TracingEnabled, Is.False);
        Mock.Assert(() => _mockAgent.Configuration, Occurs.Never());
    }

    [Test]
    public void ShouldListenTo_WhenDisabled_ReturnsFalse()
    {
        _sink.Disable();

        Assert.That(_sink.ShouldListenTo("Some.Other.Source"), Is.False);
        Mock.Assert(() => _mockAgent.Configuration, Occurs.Never());
    }

    [TestCase(KindServer, false)]
    [TestCase(KindConsumer, false)]
    [TestCase(KindClient, true)]
    public void ShouldSample_WhenDisabled_ReturnsFalse(int kind, bool parentIsRemote)
    {
        Mock.Arrange(() => _mockTransaction.IsValid).Returns(true);
        _sink.Disable();

        Assert.That(_sink.ShouldSample(kind, parentIsRemote), Is.False);
        Mock.Assert(() => _mockAgent.CurrentTransaction, Occurs.Never());
    }

    [Test]
    public void OnStarted_OnStopped_WhenDisabled_DoNotTouchAgent()
    {
        _sink.Disable();
        var activity = new Activity("distro-sink-disabled-test");

        Assert.DoesNotThrow(() => _sink.OnStarted(activity));
        Assert.DoesNotThrow(() => _sink.OnStopped(activity));
        Mock.Assert(() => _mockAgent.CurrentTransaction, Occurs.Never());
    }

    [Test]
    public void OnStarted_OnStopped_WithFinishedTransaction_DoNotThrow()
    {
        Mock.Arrange(() => _mockTransaction.IsFinished).Returns(true);
        var activity = new Activity("distro-sink-test");

        Assert.DoesNotThrow(() => _sink.OnStarted(activity));
        Assert.DoesNotThrow(() => _sink.OnStopped(activity));
    }

    #region F59 duplicate server Activity

    private static readonly ActivitySource DuplicateTestSource = new("DistroActivitySinkTests.Duplicate");
    private static ActivityListener _duplicateTestListener;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        // Lets DuplicateTestSource create real Server/Client Activities with remote parents. Not wired to any sink.
        _duplicateTestListener = new ActivityListener
        {
            ShouldListenTo = source => ReferenceEquals(source, DuplicateTestSource),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(_duplicateTestListener);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _duplicateTestListener.Dispose();
    }

    private static ActivityContext NewRemoteParent() =>
        new(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded, isRemote: true);

    private static Activity StartActivity(ActivityKind kind, ActivityContext parent)
    {
        var activity = DuplicateTestSource.StartActivity("HttpRequestIn", kind, parent);
        Assert.That(activity, Is.Not.Null);
        return activity;
    }

    private static Segment NewSegment(ITransactionSegmentState transactionSegmentState = null) =>
        new(transactionSegmentState ?? TransactionSegmentStateHelpers.GetItransactionSegmentState(), new MethodCallData("Type", "Method", 1));

    private static ITransactionSegmentState NewHybridTransactionSegmentState() =>
        Mock.Create<ITransactionSegmentState>(config => config.Implements<ITransaction>().Implements<IHybridAgentTransaction>());

    [Test]
    public void IsDuplicateServerActivity_SameTraceIdAndRemoteParentWhileFirstRunning_ReturnsTrue()
    {
        var parent = NewRemoteParent();
        using var first = StartActivity(ActivityKind.Server, parent);
        using var second = StartActivity(ActivityKind.Server, parent);

        Assert.That(_sink.IsDuplicateServerActivity(first, second), Is.True);
    }

    [Test]
    public void IsDuplicateServerActivity_DifferentTraceId_ReturnsFalse()
    {
        var parent = NewRemoteParent();
        using var first = StartActivity(ActivityKind.Server, parent);
        using var second = StartActivity(ActivityKind.Server, new ActivityContext(ActivityTraceId.CreateRandom(), parent.SpanId, parent.TraceFlags, isRemote: true));

        Assert.That(_sink.IsDuplicateServerActivity(first, second), Is.False);
    }

    [Test]
    public void IsDuplicateServerActivity_DifferentParentSpanId_ReturnsFalse()
    {
        var parent = NewRemoteParent();
        using var first = StartActivity(ActivityKind.Server, parent);
        using var second = StartActivity(ActivityKind.Server, new ActivityContext(parent.TraceId, ActivitySpanId.CreateRandom(), parent.TraceFlags, isRemote: true));

        Assert.That(_sink.IsDuplicateServerActivity(first, second), Is.False);
    }

    [TestCase(ActivityKind.Client)]
    [TestCase(ActivityKind.Internal)]
    [TestCase(ActivityKind.Consumer)]
    public void IsDuplicateServerActivity_SecondNotServer_ReturnsFalse(ActivityKind secondKind)
    {
        var parent = NewRemoteParent();
        using var first = StartActivity(ActivityKind.Server, parent);
        using var second = StartActivity(secondKind, parent);

        Assert.That(_sink.IsDuplicateServerActivity(first, second), Is.False);
    }

    [Test]
    public void IsDuplicateServerActivity_FirstNotServer_ReturnsFalse()
    {
        var parent = NewRemoteParent();
        using var first = StartActivity(ActivityKind.Consumer, parent);
        using var second = StartActivity(ActivityKind.Server, parent);

        Assert.That(_sink.IsDuplicateServerActivity(first, second), Is.False);
    }

    [Test]
    public void IsDuplicateServerActivity_FirstAlreadyStopped_ReturnsFalse()
    {
        var parent = NewRemoteParent();
        var first = StartActivity(ActivityKind.Server, parent);
        first.Stop();
        using var second = StartActivity(ActivityKind.Server, parent);

        Assert.That(_sink.IsDuplicateServerActivity(first, second), Is.False);
    }

    [Test]
    public void IsDuplicateServerActivity_LocalParent_ReturnsFalse()
    {
        // Same TraceId and ParentSpanId, but the parent is local (not a propagated request).
        using var localParent = StartActivity(ActivityKind.Internal, NewRemoteParent());
        using var first = DuplicateTestSource.StartActivity("HttpRequestIn", ActivityKind.Server, localParent.Context);
        using var second = DuplicateTestSource.StartActivity("HttpRequestIn", ActivityKind.Server, localParent.Context);

        Assert.That(_sink.IsDuplicateServerActivity(first, second), Is.False);
    }

    [Test]
    public void IsDuplicateServerActivity_NullOrSameInstance_ReturnsFalse()
    {
        using var activity = StartActivity(ActivityKind.Server, NewRemoteParent());

        Assert.Multiple(() =>
        {
            Assert.That(_sink.IsDuplicateServerActivity(null, activity), Is.False);
            Assert.That(_sink.IsDuplicateServerActivity(activity, activity), Is.False);
        });
    }

    [Test]
    public void OnStarted_DuplicateServerActivity_RebindsTheFirstSegmentToTheDuplicate()
    {
        var transactionSegmentState = NewHybridTransactionSegmentState();
        var hybridTransaction = (IHybridAgentTransaction)transactionSegmentState;
        var sampled = false; // the transaction's decision; the duplicate starts Recorded (sampled by the test listener)
        var traceStateString = "1@nr=rebuilt";
        Mock.Arrange(() => hybridTransaction.TryGetTraceFlagsAndState(out sampled, out traceStateString)).Returns(true);
        var parent = NewRemoteParent();
        using var first = StartActivity(ActivityKind.Server, parent);
        _sink.OnStarted(first);
        var segment = NewSegment(transactionSegmentState);
        new RuntimeNewRelicActivity(first).SetSegment(segment);

        using var duplicate = StartActivity(ActivityKind.Server, parent);
        _sink.OnStarted(duplicate);

        Assert.Multiple(() =>
        {
            Assert.That(RuntimeNewRelicActivity.GetSegmentFromActivity(duplicate), Is.SameAs(segment));
            Assert.That(RuntimeNewRelicActivity.GetSegmentFromActivity(first), Is.Null);
            Assert.That(segment.SpanId, Is.EqualTo(duplicate.SpanId.ToHexString()));
            Assert.That(duplicate.TraceStateString, Is.EqualTo(traceStateString));
            Assert.That(duplicate.ActivityTraceFlags, Is.EqualTo(ActivityTraceFlags.None));
        });
        // The bridge saw only the first: one transaction start.
        Mock.Assert(() => _mockAgent.CreateTransaction(Arg.AnyBool, Arg.AnyString, Arg.AnyString, Arg.AnyBool, Arg.IsAny<Action>()), Occurs.Once());

        // The segment now ends with the duplicate; the framework Activity's later stop no longer reaches it.
        duplicate.Stop();
        _sink.OnStopped(duplicate);
        Assert.That(segment.IsDone, Is.True);
        first.Stop();
        Assert.DoesNotThrow(() => _sink.OnStopped(first));
    }

    [Test]
    public void OnStarted_Duplicate_WhenTransactionIsNotHybrid_StillRebindsWithoutTouchingTraceState()
    {
        var parent = NewRemoteParent();
        using var first = StartActivity(ActivityKind.Server, parent);
        _sink.OnStarted(first);
        var segment = NewSegment();
        new RuntimeNewRelicActivity(first).SetSegment(segment);

        using var duplicate = StartActivity(ActivityKind.Server, parent);
        duplicate.TraceStateString = "untouched";
        _sink.OnStarted(duplicate);

        Assert.Multiple(() =>
        {
            Assert.That(RuntimeNewRelicActivity.GetSegmentFromActivity(duplicate), Is.SameAs(segment));
            Assert.That(duplicate.TraceStateString, Is.EqualTo("untouched"));
        });
    }

    [Test]
    public void OnStarted_Duplicate_WhenDistributedTracingOff_DoesNotTouchTraceStateOrFlags()
    {
        var transactionSegmentState = NewHybridTransactionSegmentState();
        var hybridTransaction = (IHybridAgentTransaction)transactionSegmentState;
        var sampled = false;
        string traceStateString = null;
        Mock.Arrange(() => hybridTransaction.TryGetTraceFlagsAndState(out sampled, out traceStateString)).Returns(false);
        var parent = NewRemoteParent();
        using var first = StartActivity(ActivityKind.Server, parent);
        _sink.OnStarted(first);
        new RuntimeNewRelicActivity(first).SetSegment(NewSegment(transactionSegmentState));
        first.ActivityTraceFlags = ActivityTraceFlags.None;

        using var duplicate = StartActivity(ActivityKind.Server, parent);
        duplicate.TraceStateString = "untouched";
        _sink.OnStarted(duplicate);

        Assert.Multiple(() =>
        {
            Assert.That(duplicate.TraceStateString, Is.EqualTo("untouched"));
            Assert.That(duplicate.ActivityTraceFlags, Is.EqualTo(ActivityTraceFlags.Recorded));
        });
    }

    [Test]
    public void OnStarted_Duplicate_WhenRebindFails_LogsOnceAndDoesNotFallBackToTheBridge()
    {
        var transactionSegmentState = NewHybridTransactionSegmentState();
        var hybridTransaction = (IHybridAgentTransaction)transactionSegmentState;
        var sampled = false;
        string traceStateString = null;
        Mock.Arrange(() => hybridTransaction.TryGetTraceFlagsAndState(out sampled, out traceStateString)).Throws(new InvalidOperationException("rebind-boom"));

        using (var logging = new TestUtilities.Logging())
        {
            for (var i = 0; i < 2; i++)
            {
                var parent = NewRemoteParent();
                using var first = StartActivity(ActivityKind.Server, parent);
                _sink.OnStarted(first);
                var segment = NewSegment(transactionSegmentState);
                new RuntimeNewRelicActivity(first).SetSegment(segment);

                using var duplicate = StartActivity(ActivityKind.Server, parent);
                Assert.DoesNotThrow(() => _sink.OnStarted(duplicate));
                Assert.That(RuntimeNewRelicActivity.GetSegmentFromActivity(duplicate), Is.SameAs(segment));
            }

            Assert.Multiple(() =>
            {
                Assert.That(logging.HasErrorMessageThatContains("rebind-boom"), Is.True);
                Assert.That(CountSinkOnStartedErrors(logging), Is.EqualTo(1));
            });
        }

        // Only the two first Activities reached the bridge.
        Mock.Assert(() => _mockAgent.CreateTransaction(Arg.AnyBool, Arg.AnyString, Arg.AnyString, Arg.AnyBool, Arg.IsAny<Action>()), Occurs.Exactly(2));
    }

    [Test]
    public void OnStarted_NotADuplicate_GoesToTheBridge()
    {
        using var first = StartActivity(ActivityKind.Server, NewRemoteParent());
        _sink.OnStarted(first);
        new RuntimeNewRelicActivity(first).SetSegment(NewSegment());

        using var otherTrace = StartActivity(ActivityKind.Server, NewRemoteParent());
        _sink.OnStarted(otherTrace);

        Assert.That(RuntimeNewRelicActivity.GetSegmentFromActivity(first), Is.Not.Null, "first keeps its segment");
        otherTrace.Stop();
        Assert.DoesNotThrow(() => _sink.OnStopped(otherTrace)); // a Server stop with no takeover links
        Mock.Assert(() => _mockAgent.CreateTransaction(Arg.AnyBool, Arg.AnyString, Arg.AnyString, Arg.AnyBool, Arg.IsAny<Action>()), Occurs.Exactly(2));
    }

    [Test]
    public void OnStarted_DuplicateWhoseFirstHasNoLiveSegment_GoesToTheBridge()
    {
        var parent = NewRemoteParent();

        // No segment on the first.
        using var first = StartActivity(ActivityKind.Server, parent);
        _sink.OnStarted(first);
        using var second = StartActivity(ActivityKind.Server, parent);
        _sink.OnStarted(second);

        // An ended segment on the (now remembered) second.
        var ended = NewSegment();
        ended.End();
        new RuntimeNewRelicActivity(second).SetSegment(ended);
        using var third = StartActivity(ActivityKind.Server, parent);
        _sink.OnStarted(third);

        Assert.That(RuntimeNewRelicActivity.GetSegmentFromActivity(second), Is.SameAs(ended));
        Mock.Assert(() => _mockAgent.CreateTransaction(Arg.AnyBool, Arg.AnyString, Arg.AnyString, Arg.AnyBool, Arg.IsAny<Action>()), Occurs.Exactly(3));
    }

    [Test]
    public void OnStarted_WhenDuplicateCheckFails_LogsOnceAndFallsBackToTheBridge()
    {
        using (var logging = new TestUtilities.Logging())
        {
            // Not an Activity: the Kind read fails.
            Assert.DoesNotThrow(() => _sink.OnStarted(new object()));
            Assert.DoesNotThrow(() => _sink.OnStarted(new object()));

            Assert.That(CountSinkOnStartedErrors(logging), Is.EqualTo(1));
        }
    }

    [Test]
    public void OnStarted_SameTraceparentInAnotherExecutionContext_DoesNotCollapse()
    {
        // A request with no sibling must not be matched by a concurrent request on the
        // same thread that carries the same inbound traceparent. Each request runs in its own ExecutionContext.
        var otherRequestContext = ExecutionContext.Capture();
        var parent = NewRemoteParent();
        using var first = StartActivity(ActivityKind.Server, parent);
        _sink.OnStarted(first);
        var segment = NewSegment();
        new RuntimeNewRelicActivity(first).SetSegment(segment);
        var originalSpanId = segment.SpanId;

        Activity other = null;
        ExecutionContext.Run(otherRequestContext, _ =>
        {
            other = StartActivity(ActivityKind.Server, parent);
            _sink.OnStarted(other);
        }, null);

        Assert.Multiple(() =>
        {
            Assert.That(RuntimeNewRelicActivity.GetSegmentFromActivity(first), Is.SameAs(segment), "first keeps its segment");
            Assert.That(RuntimeNewRelicActivity.GetSegmentFromActivity(other), Is.Not.SameAs(segment));
            Assert.That(segment.SpanId, Is.EqualTo(originalSpanId), "not re-bound");
        });
        Mock.Assert(() => _mockAgent.CreateTransaction(Arg.AnyBool, Arg.AnyString, Arg.AnyString, Arg.AnyBool, Arg.IsAny<Action>()), Occurs.Exactly(2));
        other.Dispose();
    }

    [Test]
    public void OnStopped_FirstStopsWhileDuplicateNeverStopped_StopsTheDuplicateAndEndsTheSegment()
    {
        // Upstream stops its sibling only when it is still Activity.Current at
        // HttpRequestIn.Stop. If it is not, the framework Activity's stop must still finish the request.
        var parent = NewRemoteParent();
        using var first = StartActivity(ActivityKind.Server, parent);
        _sink.OnStarted(first);
        var segment = NewSegment();
        new RuntimeNewRelicActivity(first).SetSegment(segment);
        using var duplicate = StartActivity(ActivityKind.Server, parent);
        _sink.OnStarted(duplicate);
        // The plugin's listener routes every stop to the sink; do the same here so the duplicate's stop reaches it.
        using var stopListener = new ActivityListener
        {
            ShouldListenTo = source => ReferenceEquals(source, DuplicateTestSource),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.None,
            ActivityStopped = _sink.OnStopped
        };
        ActivitySource.AddActivityListener(stopListener);

        first.Stop();
        _sink.OnStopped(first);

        Assert.Multiple(() =>
        {
            Assert.That(duplicate.IsStopped, Is.True);
            Assert.That(segment.IsDone, Is.True);
        });
    }

    [Test]
    public void OnStopped_Duplicate_CarriesOverTheFrameworkActivitysTagsEventsAndStatus()
    {
        var parent = NewRemoteParent();
        using var first = StartActivity(ActivityKind.Server, parent);
        _sink.OnStarted(first);
        new RuntimeNewRelicActivity(first).SetSegment(NewSegment());
        using var duplicate = StartActivity(ActivityKind.Server, parent);
        _sink.OnStarted(duplicate);

        first.SetTag("framework.only", "f");
        first.SetTag("both", "framework");
        first.AddEvent(new ActivityEvent("framework-event"));
        first.SetStatus(ActivityStatusCode.Error, "framework failed");
        duplicate.SetTag("both", "duplicate");

        duplicate.Stop();
        _sink.OnStopped(duplicate);

        Assert.Multiple(() =>
        {
            Assert.That(duplicate.GetTagItem("framework.only"), Is.EqualTo("f"));
            Assert.That(duplicate.GetTagItem("both"), Is.EqualTo("duplicate"), "the duplicate's own value wins");
            Assert.That(duplicate.Events, Has.One.Matches<ActivityEvent>(e => e.Name == "framework-event"));
            Assert.That(duplicate.Status, Is.EqualTo(ActivityStatusCode.Error));
            Assert.That(duplicate.StatusDescription, Is.EqualTo("framework failed"));
        });
    }

    [Test]
    public void OnStopped_Duplicate_KeepsItsOwnStatus()
    {
        var parent = NewRemoteParent();
        using var first = StartActivity(ActivityKind.Server, parent);
        _sink.OnStarted(first);
        new RuntimeNewRelicActivity(first).SetSegment(NewSegment());
        using var duplicate = StartActivity(ActivityKind.Server, parent);
        _sink.OnStarted(duplicate);

        first.SetStatus(ActivityStatusCode.Error, "framework failed");
        duplicate.SetStatus(ActivityStatusCode.Ok);

        duplicate.Stop();
        _sink.OnStopped(duplicate);

        Assert.That(duplicate.Status, Is.EqualTo(ActivityStatusCode.Ok));
    }

    [Test]
    public void OnStopped_WhenCollapseCheckFails_LogsOnceAndStillCallsTheBridge()
    {
        using (var logging = new TestUtilities.Logging())
        {
            // Accessors built for Activity first, then a different type: not an Activity, so they cannot be rebuilt.
            Assert.DoesNotThrow(() => _sink.OnStopped(new Activity("distro-sink-warm-up")));
            Assert.DoesNotThrow(() => _sink.OnStopped(new object()));
            Assert.DoesNotThrow(() => _sink.OnStopped(new object()));

            Assert.That(CountSinkErrors(logging, "OnStopped"), Is.EqualTo(1));
        }
    }

    private static int CountSinkErrors(TestUtilities.Logging logging, string callbackName)
    {
        var count = 0;
        foreach (var message in logging.ErrorMessages)
        {
            if (message.Contains($"OTel distro activity sink {callbackName} callback failed"))
            {
                count++;
            }
        }

        return count;
    }

    private static int CountSinkOnStartedErrors(TestUtilities.Logging logging)
    {
        var count = 0;
        foreach (var message in logging.ErrorMessages)
        {
            if (message.Contains("OTel distro activity sink OnStarted callback failed"))
            {
                count++;
            }
        }

        return count;
    }

    #endregion
}
