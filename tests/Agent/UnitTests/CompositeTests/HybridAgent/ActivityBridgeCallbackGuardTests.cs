// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using System.Linq;
using NewRelic.Agent.Api;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.OpenTelemetryBridge.Tracing;
using NewRelic.Agent.TestUtilities;
using NUnit.Framework;
using Telerik.JustMock;

namespace CompositeTests.HybridAgent;

[TestFixture]
public class ActivityBridgeCallbackGuardTests
{
    private static readonly ActivitySource Source = new("ActivityBridgeCallbackGuardTests");
    private CompositeTestAgent _compositeTestAgent;
    private ActivityBridge _bridge;
    private IAgent _agent;

    [SetUp]
    public void SetUp()
    {
        _compositeTestAgent = new CompositeTestAgent();
        _compositeTestAgent.LocalConfiguration.openTelemetry.enabled = true;
        _compositeTestAgent.LocalConfiguration.openTelemetry.traces.enabled = true;
        _compositeTestAgent.LocalConfiguration.openTelemetry.traces.include = Source.Name;
        _compositeTestAgent.PushConfiguration();

        // A live transaction whose CurrentSegment throws makes ActivityStarted fail after sampling passes.
        var transaction = Mock.Create<ITransaction>();
        Mock.Arrange(() => transaction.IsValid).Returns(true);
        Mock.Arrange(() => transaction.IsFinished).Returns(false);
        Mock.Arrange(() => transaction.CurrentSegment).Throws(new InvalidOperationException("started-callback test failure"));

        _agent = Mock.Create<IAgent>();
        Mock.Arrange(() => _agent.Configuration).Returns(_compositeTestAgent.GetAgent().Configuration);
        Mock.Arrange(() => _agent.CurrentTransaction).Returns(transaction);

        _bridge = new ActivityBridge(_agent, _compositeTestAgent.Container.Resolve<IErrorService>());
        _bridge.Start();
    }

    [TearDown]
    public void TearDown()
    {
        // The process-wide Activity.TraceIdGenerator keeps this bridge, so stop its agent mock from throwing into later tests.
        var realAgent = _compositeTestAgent.GetAgent();
        Mock.Arrange(() => _agent.CurrentTransaction).Returns(() => realAgent.CurrentTransaction);

        _bridge.Dispose();
        _compositeTestAgent.Dispose();
    }

    [Test]
    public void CallbackExceptions_DoNotPropagate_AndAreLoggedOncePerCallback()
    {
        using var logging = new Logging();
        var segment = Mock.Create<ISegment>();
        Mock.Arrange(() => segment.End()).Throws(new InvalidOperationException("stopped-callback test failure"));

        Assert.DoesNotThrow(() =>
        {
            for (var i = 0; i < 2; i++)
            {
                using var activity = Source.StartActivity($"guard-{i}", ActivityKind.Internal);
                Assert.That(activity, Is.Not.Null, "the bridge did not sample the test activity");
                activity.SetCustomProperty(NewRelicActivitySourceProxy.SegmentCustomPropertyName, segment);
            }
        });

        var bridgeErrors = logging.ErrorMessages.Count(m => m.Contains("OpenTelemetry bridge"));
        Assert.That(logging.HasErrorMessageThatContains("started-callback test failure"), Is.True);
        Assert.That(logging.HasErrorMessageThatContains("stopped-callback test failure"), Is.True);
        Assert.That(bridgeErrors, Is.EqualTo(2));
    }

    [Test]
    public void SampleCallbackException_DoesNotPropagate_SkipsActivity_AndIsLoggedOnce()
    {
        using var logging = new Logging();
        Mock.Arrange(() => _agent.CurrentTransaction).Throws(new InvalidOperationException("sample-callback test failure"));

        Activity first = null;
        Activity second = null;
        Assert.DoesNotThrow(() =>
        {
            first = Source.StartActivity("sample-guard-1", ActivityKind.Internal);
            second = Source.StartActivity("sample-guard-2", ActivityKind.Internal);
        });

        Assert.That(first, Is.Null);
        Assert.That(second, Is.Null);
        Assert.That(logging.HasErrorMessageThatContains("sample-callback test failure"), Is.True);
        Assert.That(logging.ErrorMessages.Count(m => m.Contains("OpenTelemetry bridge ShouldSampleActivity")), Is.EqualTo(1));
    }
}
