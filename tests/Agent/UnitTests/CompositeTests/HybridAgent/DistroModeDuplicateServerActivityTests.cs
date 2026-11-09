// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.OtelDistro;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.TestUtilities;
using NUnit.Framework;

namespace CompositeTests.HybridAgent;

/// <summary>
/// F59: with an inbound traceparent, upstream's ASP.NET Core instrumentation starts a second Server Activity (same
/// source, same TraceId, same remote parent) right after the framework's HttpRequestIn Activity and makes it
/// Activity.Current. Drives a real <see cref="DistroActivitySink"/> through a listener wired like the distro plugin's
/// DistroActivityListener, against a real agent, and checks the harvested spans and transaction.
/// </summary>
[TestFixture]
public class DistroModeDuplicateServerActivityTests
{
    private static readonly ActivitySource Source = new("DistroModeDuplicateServerActivityTests");

    private CompositeTestAgent _compositeTestAgent;
    private DistroActivitySink _sink;
    private ActivityListener _listener;

    [TearDown]
    public void TearDown()
    {
        _listener?.Dispose();
        _sink?.Disable();
        _compositeTestAgent?.Dispose();
    }

    [Test]
    public void DuplicateServerActivity_ProducesOneServerSegment()
    {
        Start(SamplerType.Adaptive);
        var inbound = RemoteParent(ActivityTraceFlags.Recorded);

        var request = RunRequest(inbound, withDuplicate: true);
        _compositeTestAgent.Harvest();

        var spans = SpansFor(inbound.TraceId);
        var guids = spans.Select(Guid).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(_compositeTestAgent.TransactionEvents, Has.Count.EqualTo(1));
            // NR root span + ONE server span + the client span.
            Assert.That(spans, Has.Count.EqualTo(3), string.Join(", ", spans.Select(s => s.IntrinsicAttributes()["name"])));
            Assert.That(guids, Does.Contain(request.Duplicate.SpanId.ToHexString()), "the surviving server span is the duplicate (Activity.Current)");
            Assert.That(guids, Does.Not.Contain(request.Framework.SpanId.ToHexString()), "no span for the framework Activity");
        });
    }

    [Test]
    public void DuplicateServerActivity_ChildAndOutboundLinksResolve()
    {
        Start(SamplerType.Adaptive);
        var inbound = RemoteParent(ActivityTraceFlags.Recorded);

        var request = RunRequest(inbound, withDuplicate: true);
        _compositeTestAgent.Harvest();

        var spans = SpansFor(inbound.TraceId);
        var guids = spans.Select(Guid).ToList();
        var root = spans.Single(s => s.IntrinsicAttributes().ContainsKey("nr.entryPoint"));
        var client = spans.Single(s => Guid(s) == request.Client.SpanId.ToHexString());
        var transaction = _compositeTestAgent.TransactionEvents.Single();
        Assert.Multiple(() =>
        {
            Assert.That(request.CurrentAtOutboundPoint, Is.SameAs(request.Duplicate));
            Assert.That(guids, Does.Contain(request.CurrentAtOutboundPoint.SpanId.ToHexString()), "Activity.Current at the outbound point names an NR span");
            Assert.That(guids, Does.Contain(request.Client.SpanId.ToHexString()), "the span id sent downstream names an NR span");
            Assert.That(ParentId(client), Is.EqualTo(request.Duplicate.SpanId.ToHexString()));
            Assert.That(request.Client.ParentSpanId, Is.EqualTo(request.Duplicate.SpanId), "OTel and NR agree on the client's parent");
            foreach (var span in spans.Where(s => !ReferenceEquals(s, root)))
            {
                Assert.That(guids, Does.Contain(ParentId(span)), $"parentId of {span.IntrinsicAttributes()["name"]} is an NR span");
            }

            Assert.That(ParentId(root), Is.EqualTo(inbound.SpanId.ToHexString()));
            Assert.That(transaction.IntrinsicAttributes()["traceId"], Is.EqualTo(inbound.TraceId.ToHexString()));
            Assert.That(transaction.IntrinsicAttributes()["parentSpanId"], Is.EqualTo(inbound.SpanId.ToHexString()));
            Assert.That(request.Duplicate.TraceStateString, Does.Contain(request.Duplicate.SpanId.ToHexString()), "tracestate names the surviving span");
        });
    }

    [TestCase(SamplerType.AlwaysOn)]
    [TestCase(SamplerType.AlwaysOff)]
    [TestCase(SamplerType.Adaptive)]
    public void DuplicateServerActivity_NotSampledParent_KeepsDecisionAndIds(SamplerType remoteParentNotSampledSampler)
    {
        Start(remoteParentNotSampledSampler);

        var withoutInbound = RemoteParent(ActivityTraceFlags.None);
        var without = RunRequest(withoutInbound, withDuplicate: false);
        _compositeTestAgent.Harvest();
        var withoutTransaction = TransactionFor(withoutInbound.TraceId);
        var withoutSpanCount = SpansFor(withoutInbound.TraceId).Count;
        _compositeTestAgent.ResetHarvestData();

        var withInbound = RemoteParent(ActivityTraceFlags.None);
        var with = RunRequest(withInbound, withDuplicate: true);
        _compositeTestAgent.Harvest();
        var withTransaction = TransactionFor(withInbound.TraceId);
        var withSpans = SpansFor(withInbound.TraceId);

        Assert.Multiple(() =>
        {
            Assert.That(withTransaction["sampled"], Is.EqualTo(withoutTransaction["sampled"]));
            Assert.That(with.Duplicate.ActivityTraceFlags, Is.EqualTo(without.Framework.ActivityTraceFlags), "Activity.Current carries the same decision");
            Assert.That(with.Framework.ActivityTraceFlags, Is.EqualTo(without.Framework.ActivityTraceFlags));
            Assert.That(with.Client.ActivityTraceFlags, Is.EqualTo(without.Client.ActivityTraceFlags), "outbound flags unchanged");
            Assert.That(withSpans, Has.Count.EqualTo(withoutSpanCount), "no extra span");
            Assert.That(withTransaction["traceId"], Is.EqualTo(withInbound.TraceId.ToHexString()));
            Assert.That(withTransaction["parentSpanId"], Is.EqualTo(withInbound.SpanId.ToHexString()));
            Assert.That(with.Duplicate.TraceId, Is.EqualTo(withInbound.TraceId));
            if (remoteParentNotSampledSampler == SamplerType.AlwaysOff)
            {
                Assert.That(withSpans, Is.Empty, "not sampled: no spans");
            }
            else
            {
                Assert.That(withSpans.Select(Guid), Does.Contain(with.Duplicate.SpanId.ToHexString()), "sampled: the surviving server span is the duplicate");
            }
        });
    }

    [Test]
    public void DuplicateServerActivity_SiblingNeverStopped_FrameworkStopEndsTheTransaction()
    {
        // Upstream stops its sibling only if it is still Activity.Current at HttpRequestIn.Stop.
        Start(SamplerType.Adaptive);
        var inbound = RemoteParent(ActivityTraceFlags.Recorded);

        var framework = Source.StartActivity("Microsoft.AspNetCore.Hosting.HttpRequestIn", ActivityKind.Server, inbound);
        var duplicate = Source.StartActivity("Microsoft.AspNetCore.Hosting.HttpRequestIn", ActivityKind.Server, inbound);
        var client = Source.StartActivity("System.Net.Http.HttpRequestOut", ActivityKind.Client);
        client!.Stop();
        framework!.Stop(); // the sibling is never stopped by its owner
        _compositeTestAgent.Harvest();

        var spans = SpansFor(inbound.TraceId);
        Assert.Multiple(() =>
        {
            Assert.That(duplicate!.IsStopped, Is.True);
            Assert.That(TransactionFor(inbound.TraceId), Is.Not.Null);
            Assert.That(spans, Has.Count.EqualTo(3), string.Join(", ", spans.Select(s => s.IntrinsicAttributes()["name"])));
            Assert.That(spans.Select(Guid), Does.Contain(duplicate.SpanId.ToHexString()));
            Assert.That(spans.Select(Guid), Does.Not.Contain(framework.SpanId.ToHexString()));
        });
    }

    [Test]
    public void DuplicateServerActivity_FrameworkActivityTagsAndStatusAreKept()
    {
        Start(SamplerType.Adaptive);
        var inbound = RemoteParent(ActivityTraceFlags.Recorded);

        var framework = Source.StartActivity("Microsoft.AspNetCore.Hosting.HttpRequestIn", ActivityKind.Server, inbound);
        var duplicate = Source.StartActivity("Microsoft.AspNetCore.Hosting.HttpRequestIn", ActivityKind.Server, inbound);
        framework!.SetTag("framework.only", "f");
        framework.SetTag("both", "framework");
        framework.SetStatus(ActivityStatusCode.Error, "framework failed");
        duplicate!.SetTag("both", "duplicate");
        duplicate.Stop();
        framework.Stop();
        _compositeTestAgent.Harvest();

        var server = SpansFor(inbound.TraceId).Single(s => Guid(s) == duplicate.SpanId.ToHexString());
        var user = server.UserAttributes();
        var agentAttributes = server.AgentAttributes();
        Assert.Multiple(() =>
        {
            Assert.That(user["framework.only"], Is.EqualTo("f"));
            Assert.That(user["both"], Is.EqualTo("duplicate"));
            Assert.That(agentAttributes["status.code"], Is.EqualTo("error"));
            Assert.That(agentAttributes["status.description"], Is.EqualTo("framework failed"));
        });
    }

    private void Start(SamplerType remoteParentNotSampledSampler)
    {
        _compositeTestAgent = new CompositeTestAgent(shouldAllowThreads: false, includeAsyncLocalStorage: false, remoteParentNotSampledSamplerType: remoteParentNotSampledSampler);
        _compositeTestAgent.ServerConfiguration.TrustedAccountKey = "1";
        _compositeTestAgent.LocalConfiguration.openTelemetry.enabled = true;
        _compositeTestAgent.LocalConfiguration.openTelemetry.traces.enabled = true;
        _compositeTestAgent.LocalConfiguration.openTelemetry.traces.include = Source.Name;
        _compositeTestAgent.PushConfiguration();

        _sink = new DistroActivitySink(_compositeTestAgent.GetAgent(), _compositeTestAgent.Container.Resolve<IErrorService>());
        _sink.InitializeActivityTypes(typeof(Activity), typeof(ActivitySource), typeof(ActivityKind));

        // Same wiring as the plugin's DistroActivityListener.
        _listener = new ActivityListener
        {
            ShouldListenTo = source => _sink.ShouldListenTo(source.Name),
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => _sink.ShouldSample((int)options.Kind, options.Parent.IsRemote)
                ? ActivitySamplingResult.AllDataAndRecorded
                : ActivitySamplingResult.None,
            ActivityStarted = _sink.OnStarted,
            ActivityStopped = _sink.OnStopped
        };
        ActivitySource.AddActivityListener(_listener);
    }

    private static ActivityContext RemoteParent(ActivityTraceFlags flags) =>
        new(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), flags, isRemote: true);

    // The upstream shape: framework Activity, then (synchronously, same thread) the sibling with the same remote
    // parent, which becomes Activity.Current; the handler's outbound call; sibling stops first, then the framework.
    private static Request RunRequest(ActivityContext inbound, bool withDuplicate)
    {
        var request = new Request();
        request.Framework = Source.StartActivity("Microsoft.AspNetCore.Hosting.HttpRequestIn", ActivityKind.Server, inbound);
        if (withDuplicate)
        {
            request.Duplicate = Source.StartActivity("Microsoft.AspNetCore.Hosting.HttpRequestIn", ActivityKind.Server, inbound);
            request.Duplicate!.DisplayName = "GET /f59";
        }

        request.CurrentAtOutboundPoint = Activity.Current;
        request.Client = Source.StartActivity("System.Net.Http.HttpRequestOut", ActivityKind.Client);
        request.Client!.Stop();

        request.Duplicate?.Stop();
        request.Framework!.Stop();
        return request;
    }

    private List<ISpanEventWireModel> SpansFor(ActivityTraceId traceId) =>
        _compositeTestAgent.SpanEvents.Where(s => (string)s.IntrinsicAttributes()["traceId"] == traceId.ToHexString()).ToList();

    private IDictionary<string, object> TransactionFor(ActivityTraceId traceId)
    {
        var transactions = _compositeTestAgent.TransactionEvents.Select(t => t.IntrinsicAttributes()).ToList();
        Assert.That(transactions.Count(t => (string)t["traceId"] == traceId.ToHexString()), Is.EqualTo(1),
            "one transaction for the request; harvested: " + string.Join(", ", transactions.Select(t => $"{t["name"]}/{t["traceId"]}")));
        return transactions.Single(t => (string)t["traceId"] == traceId.ToHexString());
    }

    private static string Guid(ISpanEventWireModel span) => (string)span.IntrinsicAttributes()["guid"];

    private static string ParentId(ISpanEventWireModel span) =>
        span.IntrinsicAttributes().TryGetValue("parentId", out var parentId) ? (string)parentId : null;

    private sealed class Request
    {
        public Activity Framework { get; set; }
        public Activity Duplicate { get; set; }
        public Activity CurrentAtOutboundPoint { get; set; }
        public Activity Client { get; set; }
    }
}
