// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NewRelic.Agent.Api;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.OpenTelemetryBridge.Tracing;
using NewRelic.Agent.Core.OtelDistro;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.TestUtilities;
using NUnit.Framework;

namespace CompositeTests.HybridAgent;

/// <summary>
/// Pins the OTel distro mode (<see cref="DistroActivitySink"/> fed by a plugin-style ActivityListener) to the behavior of
/// agent mode's <see cref="ActivityBridge"/>: same trace ids, same parenting and same sampling decisions.
/// Agent-mode and distro listeners never run at the same time: each run owns a fresh agent and disposes its listener.
/// </summary>
[TestFixture]
public class DistroModeActivityTests
{
    private const string TraceParent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";
    private const string TraceParentNotSampled = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-00";
    private const string TestSourceName = "DistroModeActivityTests source";
    private const string UnlistenedSourceName = "DistroModeActivityTests unlistened source";

    private static readonly ActivitySource TestSource = new(TestSourceName, "1.0.0");
    private static readonly ActivitySource UnlistenedSource = new(UnlistenedSourceName, "1.0.0");

    [SetUp]
    public void Setup()
    {
        // Agent mode installs a TraceIdGenerator; the distro plugin never does. Make sure one left behind by an
        // agent-mode test (disposed bridge, stale agent) cannot influence these tests.
        Activity.TraceIdGenerator = null;
    }

    [TearDown]
    public void TearDown()
    {
        Activity.TraceIdGenerator = null;
    }

    [Test]
    public void Distro_LocalRootServerActivity_TraceIdsMatch()
    {
        using var distro = new DistroRun();

        string traceId, spanId;
        using (var activity = TestSource.StartActivity("root server", ActivityKind.Server))
        {
            Assert.That(activity, Is.Not.Null, "Server activity should be sampled in distro mode.");
            traceId = activity.TraceId.ToHexString();
            spanId = activity.SpanId.ToHexString();
        }

        distro.Agent.Harvest();

        var transaction = distro.Agent.TransactionEvents.Single();
        var span = distro.Agent.SpanEvents.Single(s => SpanName(s) == "root server");

        Assert.Multiple(() =>
        {
            Assert.That(transaction.IntrinsicAttributes()["traceId"], Is.EqualTo(traceId));
            Assert.That(span.IntrinsicAttributes()["guid"], Is.EqualTo(spanId));
            Assert.That(span.IntrinsicAttributes()["traceId"], Is.EqualTo(traceId));
        });
    }

    [Test]
    public void Distro_RemoteParent_TraceIdsAndParentMatch()
    {
        using var distro = new DistroRun();

        var parentContext = ParseRemoteContext(TraceParent);

        string spanId;
        using (var activity = TestSource.StartActivity("remote parent server", ActivityKind.Server, parentContext))
        {
            Assert.That(activity, Is.Not.Null, "Server activity should be sampled in distro mode.");
            spanId = activity.SpanId.ToHexString();
        }

        distro.Agent.Harvest();

        var transaction = distro.Agent.TransactionEvents.Single();
        var span = distro.Agent.SpanEvents.Single(s => SpanName(s) == "remote parent server");

        Assert.Multiple(() =>
        {
            Assert.That(transaction.IntrinsicAttributes()["traceId"], Is.EqualTo("0af7651916cd43dd8448eb211c80319c"));
            Assert.That(transaction.IntrinsicAttributes()["parentSpanId"], Is.EqualTo("b7ad6b7169203331"));
            Assert.That(span.IntrinsicAttributes()["guid"], Is.EqualTo(spanId));
        });
    }

    [Test]
    public void Distro_ChildClientActivity_ParentIsServerSpan()
    {
        using var distro = new DistroRun();

        using (var server = TestSource.StartActivity("parent server", ActivityKind.Server))
        {
            Assert.That(server, Is.Not.Null);
            using var client = TestSource.StartActivity("child client", ActivityKind.Client);
            Assert.That(client, Is.Not.Null, "Client activity inside a transaction should be sampled in distro mode.");
        }

        distro.Agent.Harvest();

        var serverSpan = distro.Agent.SpanEvents.Single(s => SpanName(s) == "parent server");
        var clientSpan = distro.Agent.SpanEvents.Single(s => SpanName(s) == "child client");

        Assert.Multiple(() =>
        {
            Assert.That(clientSpan.IntrinsicAttributes()["parentId"], Is.EqualTo(serverSpan.IntrinsicAttributes()["guid"]));
            Assert.That(clientSpan.IntrinsicAttributes()["traceId"], Is.EqualTo(serverSpan.IntrinsicAttributes()["traceId"]));
        });
    }

    [TestCaseSource(nameof(GetSamplingCases))]
    public void Sampling_DistroMatchesAgentMode(ActivityKind kind, string parent, bool inTransaction)
    {
        var agentModeOutcome = RunCaseInAgentMode(kind, parent, inTransaction);
        var distroOutcome = RunCaseInDistroMode(kind, parent, inTransaction);

        Assert.Multiple(() =>
        {
            Assert.That(distroOutcome.Result, Is.EqualTo(agentModeOutcome.Result), "ActivitySamplingResult");
            Assert.That(distroOutcome.TransactionCount, Is.EqualTo(agentModeOutcome.TransactionCount), "transaction event count");
            Assert.That(distroOutcome.Sampled, Is.EqualTo(agentModeOutcome.Sampled), "transaction sampled value");
        });
    }

    // A remote parent outside a transaction is sampled for every kind, in both modes.
    [TestCase(ActivityKind.Client)]
    [TestCase(ActivityKind.Internal)]
    [TestCase(ActivityKind.Producer)]
    public void Sampling_RemoteParentOutsideTransaction_IsSampledInBothModes(ActivityKind kind)
    {
        var agentModeOutcome = RunCaseInAgentMode(kind, "remote-sampled", inTransaction: false);
        var distroOutcome = RunCaseInDistroMode(kind, "remote-sampled", inTransaction: false);

        Assert.Multiple(() =>
        {
            Assert.That(agentModeOutcome.Result, Is.EqualTo(ActivitySamplingResult.AllDataAndRecorded), "agent mode");
            Assert.That(distroOutcome.Result, Is.EqualTo(ActivitySamplingResult.AllDataAndRecorded), "distro mode");
        });
    }

    public static IEnumerable<TestCaseData> GetSamplingCases()
    {
        var kinds = new[] { ActivityKind.Server, ActivityKind.Client, ActivityKind.Internal, ActivityKind.Producer, ActivityKind.Consumer };
        var parents = new[] { "none", "local", "remote-sampled", "remote-notsampled" };

        foreach (var kind in kinds)
        {
            foreach (var parent in parents)
            {
                foreach (var inTransaction in new[] { false, true })
                {
                    yield return new TestCaseData(kind, parent, inTransaction)
                        .SetName($"Sampling_DistroMatchesAgentMode({kind},{parent},inTransaction={inTransaction})");
                }
            }
        }
    }

    private static Outcome RunCaseInAgentMode(ActivityKind kind, string parent, bool inTransaction)
    {
        using var agent = CreateAgent();
        using var bridge = new ActivityBridge(agent.GetAgent(), agent.Container.Resolve<IErrorService>());
        try
        {
            Assert.That(bridge.Start(), Is.True, "Agent-mode ActivityBridge should have started.");
            return RunCase(agent, kind, parent, inTransaction);
        }
        finally
        {
            Activity.TraceIdGenerator = null;
        }
    }

    private static Outcome RunCaseInDistroMode(ActivityKind kind, string parent, bool inTransaction)
    {
        using var distro = new DistroRun();
        return RunCase(distro.Agent, kind, parent, inTransaction);
    }

    private static Outcome RunCase(CompositeTestAgent testAgent, ActivityKind kind, string parent, bool inTransaction)
    {
        var agent = testAgent.GetAgent();

        ITransaction transaction = null;
        if (inTransaction)
        {
            transaction = agent.CreateTransaction(isWeb: false, "Custom", "pre-existing", doNotTrackAsUnitOfWork: true);
        }

        ActivitySamplingResult result;
        ActivityListener unlistenedSourceListener = null;
        Activity localParent = null;
        try
        {
            ActivityContext remoteParent = default;
            switch (parent)
            {
                case "local":
                    // A parent Activity from a source the agent does not listen to, recorded by a test-only listener, so
                    // the child has a local (non-remote) parent without that parent creating a transaction.
                    unlistenedSourceListener = new ActivityListener
                    {
                        ShouldListenTo = source => source.Name == UnlistenedSourceName,
                        Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
                    };
                    ActivitySource.AddActivityListener(unlistenedSourceListener);
                    localParent = UnlistenedSource.StartActivity("local parent");
                    Assert.That(localParent, Is.Not.Null);
                    break;
                case "remote-sampled":
                    remoteParent = ParseRemoteContext(TraceParent);
                    break;
                case "remote-notsampled":
                    remoteParent = ParseRemoteContext(TraceParentNotSampled);
                    break;
            }

            using var activity = remoteParent != default
                ? TestSource.StartActivity("sampling case", kind, remoteParent)
                : TestSource.StartActivity("sampling case", kind);

            // StartActivity returns null when no listener sampled the activity (ActivitySamplingResult.None).
            result = activity == null
                ? ActivitySamplingResult.None
                : activity.IsAllDataRequested && activity.Recorded
                    ? ActivitySamplingResult.AllDataAndRecorded
                    : ActivitySamplingResult.PropagationData;
        }
        finally
        {
            localParent?.Dispose();
            unlistenedSourceListener?.Dispose();
            transaction?.End();
        }

        testAgent.Harvest();

        var transactionEvents = testAgent.TransactionEvents;
        var sampled = transactionEvents.Count == 0 ? null : transactionEvents[0].IntrinsicAttributes()["sampled"];
        return new Outcome(result, transactionEvents.Count, sampled);
    }

    // ActivityContext.Parse always yields IsRemote = false; the remote flag is what drives ShouldSample, so use TryParse.
    private static ActivityContext ParseRemoteContext(string traceParent)
    {
        Assert.That(ActivityContext.TryParse(traceParent, null, isRemote: true, out var context), Is.True);
        Assert.That(context.IsRemote, Is.True);
        return context;
    }

    private static CompositeTestAgent CreateAgent()
    {
        // Same configuration HybridAgentTestsBase.Setup applies: OTel + traces enabled, test source included.
        var agent = new CompositeTestAgent();
        agent.LocalConfiguration.log.level = "finest";
        agent.LocalConfiguration.aiMonitoring.basicTelemetry.enabled = false;
        agent.LocalConfiguration.openTelemetry.enabled = true;
        agent.LocalConfiguration.openTelemetry.traces.enabled = true;
        agent.LocalConfiguration.openTelemetry.traces.include = TestSourceName;
        agent.PushConfiguration();
        return agent;
    }

    private static string SpanName(ISpanEventWireModel span) => (string)span.IntrinsicAttributes()["name"];

    private sealed class Outcome
    {
        public Outcome(ActivitySamplingResult result, int transactionCount, object sampled)
        {
            Result = result;
            TransactionCount = transactionCount;
            Sampled = sampled;
        }

        public ActivitySamplingResult Result { get; }
        public int TransactionCount { get; }
        public object Sampled { get; }
    }

    /// <summary>
    /// A fresh agent plus a <see cref="DistroActivitySink"/> wired to an <see cref="ActivityListener"/> exactly like the
    /// plugin's DistroActivityListener: W3C id format, AllDataAndRecorded or None, no TraceIdGenerator.
    /// </summary>
    private sealed class DistroRun : IDisposable
    {
        private readonly DistroActivitySink _sink;
        private readonly ActivityListener _listener;

        public CompositeTestAgent Agent { get; }

        public DistroRun()
        {
            Agent = CreateAgent();
            _sink = new DistroActivitySink(Agent.GetAgent(), Agent.Container.Resolve<IErrorService>());

            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            _sink.InitializeActivityTypes(typeof(Activity), typeof(ActivitySource), typeof(ActivityKind));

            _listener = new ActivityListener
            {
                ShouldListenTo = source => _sink.ShouldListenTo(source.Name),
                Sample = Sample,
                ActivityStarted = _sink.OnStarted,
                ActivityStopped = _sink.OnStopped
            };
            ActivitySource.AddActivityListener(_listener);
        }

        private ActivitySamplingResult Sample(ref ActivityCreationOptions<ActivityContext> options)
        {
            return _sink.ShouldSample((int)options.Kind, options.Parent.IsRemote)
                ? ActivitySamplingResult.AllDataAndRecorded
                : ActivitySamplingResult.None;
        }

        public void Dispose()
        {
            _listener.Dispose();
            _sink.Disable();
            Agent.Dispose();
        }
    }
}
