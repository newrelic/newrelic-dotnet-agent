// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.OtelDistro;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.TestUtilities;
using NUnit.Framework;

namespace CompositeTests.HybridAgent;

/// <summary>
/// Drives a real <see cref="DistroActivitySink"/> (through a plugin-style ActivityListener) and a real
/// <see cref="DistroLogSink"/> against a real agent, and checks that harvested log events carry the trace and span ids
/// of the New Relic spans created for the Activities the logs were written in.
/// </summary>
[TestFixture]
public class DistroModeLogTests
{
    private const string TestSourceName = "DistroModeLogTests source";

    private static readonly ActivitySource TestSource = new(TestSourceName, "1.0.0");

    [SetUp]
    public void Setup()
    {
        Activity.TraceIdGenerator = null;
    }

    [TearDown]
    public void TearDown()
    {
        Activity.TraceIdGenerator = null;
    }

    [Test]
    public void Log_InsideServerActivity_CarriesTraceAndSpanIdOfNrSpan()
    {
        using var distro = new DistroRun();

        using (var server = TestSource.StartActivity("server", ActivityKind.Server))
        {
            Assert.That(server, Is.Not.Null);
            distro.LogIn(server, "inside server");
        }

        distro.Agent.Harvest();

        var logEvent = distro.Agent.LogEvents.Single();
        var span = distro.Agent.SpanEvents.Single(s => SpanName(s) == "server");
        var transactionEvent = distro.Agent.TransactionEvents.Single();
        Assert.Multiple(() =>
        {
            Assert.That(logEvent.TraceId, Is.EqualTo((string)span.IntrinsicAttributes()["traceId"]));
            Assert.That(logEvent.SpanId, Is.EqualTo((string)span.IntrinsicAttributes()["guid"]));

            // A log recorded outside a transaction gets a freshly created random priority; a log batched with a transaction
            // takes the transaction's priority when the transaction is transformed.
            Assert.That(logEvent.Priority, Is.EqualTo(transactionEvent.Priority), "log is batched with its transaction");
        });
    }

    [Test]
    public void Log_InsideChildClientActivity_CarriesChildSpanId()
    {
        using var distro = new DistroRun();

        string clientSpanId;
        using (var server = TestSource.StartActivity("server", ActivityKind.Server))
        {
            using (var client = TestSource.StartActivity("client", ActivityKind.Client))
            {
                Assert.That(client, Is.Not.Null);
                clientSpanId = client.SpanId.ToHexString();
                distro.LogIn(client, "inside client");
            }
        }

        distro.Agent.Harvest();

        var logEvent = distro.Agent.LogEvents.Single();
        var serverSpan = distro.Agent.SpanEvents.Single(s => SpanName(s) == "server");
        var clientSpan = distro.Agent.SpanEvents.Single(s => SpanName(s) == "client");
        Assert.Multiple(() =>
        {
            Assert.That((string)clientSpan.IntrinsicAttributes()["guid"], Is.EqualTo(clientSpanId));
            Assert.That(logEvent.SpanId, Is.EqualTo(clientSpanId));
            Assert.That(logEvent.SpanId, Is.Not.EqualTo((string)serverSpan.IntrinsicAttributes()["guid"]));
            Assert.That(logEvent.TraceId, Is.EqualTo((string)clientSpan.IntrinsicAttributes()["traceId"]));
        });
    }

    [Test]
    public void Log_OutsideAnyActivity_HasNoIds()
    {
        using var distro = new DistroRun();

        distro.LogIn(null, "no activity");
        distro.Agent.Harvest();

        var logEvent = distro.Agent.LogEvents.Single();
        Assert.Multiple(() =>
        {
            Assert.That(logEvent.TraceId, Is.Null.Or.Empty);
            Assert.That(logEvent.SpanId, Is.Null.Or.Empty);
        });
    }

    [Test]
    public void Log_UnderCollapsedDuplicateServerActivity_CarriesNrServerSpanId()
    {
        using var distro = new DistroRun();

        // The F59 shape from DistroModeDuplicateServerActivityTests: the framework's HttpRequestIn Activity, then a
        // sibling with the same remote parent that becomes Activity.Current. The handler logs under the sibling.
        var inbound = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded, isRemote: true);
        var framework = TestSource.StartActivity("Microsoft.AspNetCore.Hosting.HttpRequestIn", ActivityKind.Server, inbound);
        var duplicate = TestSource.StartActivity("Microsoft.AspNetCore.Hosting.HttpRequestIn", ActivityKind.Server, inbound);
        Assert.That(Activity.Current, Is.SameAs(duplicate));

        distro.LogIn(Activity.Current, "inside handler");

        duplicate!.Stop();
        framework!.Stop();
        distro.Agent.Harvest();

        var logEvent = distro.Agent.LogEvents.Single();
        var serverSpans = distro.Agent.SpanEvents
            .Where(s => (string)s.IntrinsicAttributes()["guid"] == duplicate.SpanId.ToHexString() || (string)s.IntrinsicAttributes()["guid"] == framework.SpanId.ToHexString())
            .ToList();
        Assert.Multiple(() =>
        {
            Assert.That(serverSpans, Has.Count.EqualTo(1), "one NR server span for the collapsed pair");
            Assert.That(logEvent.SpanId, Is.EqualTo((string)serverSpans.Single().IntrinsicAttributes()["guid"]));
            Assert.That(logEvent.SpanId, Is.EqualTo(duplicate.SpanId.ToHexString()));
            Assert.That(logEvent.TraceId, Is.EqualTo((string)serverSpans.Single().IntrinsicAttributes()["traceId"]));
        });
    }

    [Test]
    public void Log_ForwardingDisabled_NothingHarvested()
    {
        using var distro = new DistroRun(configure: agent => agent.LocalConfiguration.applicationLogging.forwarding.enabled = false);

        using (var server = TestSource.StartActivity("server", ActivityKind.Server))
        {
            distro.LogIn(server, "not forwarded");
        }

        distro.Agent.Harvest();

        Assert.That(distro.Agent.LogEvents, Is.Empty);
    }

    [Test]
    public void Log_FormattedMessage_IsSent()
    {
        using var distro = new DistroRun();

        distro.Sink.Record(new DistroLogRecord
        {
            Timestamp = DateTime.UtcNow,
            LevelText = "Information",
            Body = "Hello {Name}",
            FormattedMessage = "Hello world"
        });
        distro.Agent.Harvest();

        var logEvent = distro.Agent.LogEvents.Single();
        Assert.Multiple(() =>
        {
            Assert.That(logEvent.Message, Is.EqualTo("Hello world"));
            Assert.That(logEvent.Level, Is.EqualTo("INFORMATION"));
        });
    }

    private static string SpanName(ISpanEventWireModel span) => (string)span.IntrinsicAttributes()["name"];

    private sealed class DistroRun : IDisposable
    {
        private readonly DistroActivitySink _activitySink;
        private readonly ActivityListener _listener;

        public CompositeTestAgent Agent { get; }

        public DistroLogSink Sink { get; }

        public DistroRun(Action<CompositeTestAgent> configure = null)
        {
            Agent = new CompositeTestAgent();
            Agent.ServerConfiguration.TrustedAccountKey = "1";
            Agent.LocalConfiguration.openTelemetry.enabled = true;
            Agent.LocalConfiguration.openTelemetry.traces.enabled = true;
            Agent.LocalConfiguration.openTelemetry.traces.include = TestSourceName;
            configure?.Invoke(Agent);
            Agent.PushConfiguration();

            _activitySink = new DistroActivitySink(Agent.GetAgent(), Agent.Container.Resolve<IErrorService>());
            Sink = new DistroLogSink(Agent.GetAgent());

            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            _activitySink.InitializeActivityTypes(typeof(Activity), typeof(ActivitySource), typeof(ActivityKind));

            _listener = new ActivityListener
            {
                ShouldListenTo = source => _activitySink.ShouldListenTo(source.Name),
                Sample = Sample,
                ActivityStarted = _activitySink.OnStarted,
                ActivityStopped = _activitySink.OnStopped
            };
            ActivitySource.AddActivityListener(_listener);
        }

        /// <summary>
        /// Records a log the way the plugin does: the ids are copied from the Activity the log was written in.
        /// </summary>
        public void LogIn(Activity activity, string message)
        {
            Sink.Record(new DistroLogRecord
            {
                Timestamp = DateTime.UtcNow,
                LevelText = "Information",
                Body = message,
                FormattedMessage = message,
                TraceId = activity?.TraceId.ToHexString(),
                SpanId = activity?.SpanId.ToHexString(),
                Attributes = new List<KeyValuePair<string, object>>()
            });
        }

        private ActivitySamplingResult Sample(ref ActivityCreationOptions<ActivityContext> options)
        {
            return _activitySink.ShouldSample((int)options.Kind, options.Parent.IsRemote)
                ? ActivitySamplingResult.AllDataAndRecorded
                : ActivitySamplingResult.None;
        }

        public void Dispose()
        {
            _listener.Dispose();
            _activitySink.Disable();
            Sink.Disable();
            Agent.Dispose();
        }
    }
}
