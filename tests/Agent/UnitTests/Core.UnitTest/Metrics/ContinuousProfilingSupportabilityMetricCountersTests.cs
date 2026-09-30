// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.WireModels;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.UnitTests.Metrics;

[TestFixture]
public class ContinuousProfilingSupportabilityMetricCountersTests
{
    private ContinuousProfilingSupportabilityMetricCounters _metricCounters;
    private List<MetricWireModel> _publishedMetrics;

    [SetUp]
    public void SetUp()
    {
        var metricBuilder = WireModels.Utilities.GetSimpleMetricBuilder();
        _metricCounters = new ContinuousProfilingSupportabilityMetricCounters(metricBuilder);

        _publishedMetrics = new List<MetricWireModel>();
        _metricCounters.RegisterPublishMetricHandler(metric => _publishedMetrics.Add(metric));
    }

    [Test]
    public void CollectMetrics_PublishesNothing_WhenNothingRecorded()
    {
        _metricCounters.CollectMetrics();
        Assert.That(_publishedMetrics, Is.Empty);
    }

    [Test]
    public void RecordExportSuccess_PublishesTheDedicatedCpSuccessMetric()
    {
        _metricCounters.RecordExportSuccess();
        _metricCounters.CollectMetrics();

        Assert.That(_publishedMetrics, Has.Count.EqualTo(1));
        var metric = _publishedMetrics.Single();
        Assert.Multiple(() =>
        {
            Assert.That(metric.MetricNameModel.Name, Is.EqualTo(MetricNames.SupportabilityContinuousProfilingExportSuccess));
            Assert.That(metric.DataModel.Value0, Is.EqualTo(1));
        });
    }

    [Test]
    public void RecordExportRetry_PublishesTheDedicatedCpRetryMetric()
    {
        _metricCounters.RecordExportRetry();
        _metricCounters.RecordExportRetry();
        _metricCounters.CollectMetrics();

        Assert.That(_publishedMetrics, Has.Count.EqualTo(1));
        var metric = _publishedMetrics.Single();
        Assert.Multiple(() =>
        {
            Assert.That(metric.MetricNameModel.Name, Is.EqualTo(MetricNames.SupportabilityContinuousProfilingExportRetry));
            Assert.That(metric.DataModel.Value0, Is.EqualTo(2));
        });
    }

    [Test]
    public void RecordExportFailure_PublishesTheDedicatedCpFailureMetric()
    {
        _metricCounters.RecordExportFailure();
        _metricCounters.CollectMetrics();

        Assert.That(_publishedMetrics, Has.Count.EqualTo(1));
        var metric = _publishedMetrics.Single();
        Assert.That(metric.MetricNameModel.Name, Is.EqualTo(MetricNames.SupportabilityContinuousProfilingExportFailure));
    }

    [Test]
    public void CollectMetrics_ResetsCountersAfterPublishing()
    {
        _metricCounters.RecordExportSuccess();
        _metricCounters.CollectMetrics();

        _publishedMetrics.Clear();
        _metricCounters.CollectMetrics();

        Assert.That(_publishedMetrics, Is.Empty);
    }

    [Test]
    public void CollectMetrics_OnlyPublishesNonZeroCounters()
    {
        _metricCounters.RecordExportSuccess();

        _metricCounters.CollectMetrics();

        Assert.That(_publishedMetrics, Has.Count.EqualTo(1));
        Assert.That(_publishedMetrics.Single().MetricNameModel.Name, Is.EqualTo(MetricNames.SupportabilityContinuousProfilingExportSuccess));
    }

    [Test]
    public void RecordPayloadDropped_PublishesTheDedicatedCpPayloadDroppedMetric()
    {
        _metricCounters.RecordPayloadDropped();
        _metricCounters.CollectMetrics();

        Assert.That(_publishedMetrics, Has.Count.EqualTo(1));
        var metric = _publishedMetrics.Single();
        Assert.Multiple(() =>
        {
            Assert.That(metric.MetricNameModel.Name, Is.EqualTo(MetricNames.SupportabilityContinuousProfilingExportPayloadDropped));
            Assert.That(metric.DataModel.Value0, Is.EqualTo(1));
        });
    }

    [Test]
    public void RecordFullRejection_PublishesTheDedicatedCpFullRejectionMetric()
    {
        _metricCounters.RecordFullRejection();
        _metricCounters.CollectMetrics();

        Assert.That(_publishedMetrics, Has.Count.EqualTo(1));
        var metric = _publishedMetrics.Single();
        Assert.Multiple(() =>
        {
            Assert.That(metric.MetricNameModel.Name, Is.EqualTo(MetricNames.SupportabilityContinuousProfilingExportFullRejection));
            Assert.That(metric.DataModel.Value0, Is.EqualTo(1));
        });
    }

    [Test]
    public void RecordTimeoutFailure_PublishesTheTimeoutFailureMetric()
    {
        _metricCounters.RecordTimeoutFailure();
        _metricCounters.RecordTimeoutFailure();
        _metricCounters.CollectMetrics();

        var metric = _publishedMetrics.Single();
        Assert.Multiple(() =>
        {
            Assert.That(metric.MetricNameModel.Name, Is.EqualTo("Supportability/DotNET/ContinuousProfiling/Export/failure_timeout"));
            Assert.That(metric.DataModel.Value0, Is.EqualTo(2));
        });
    }

    [Test]
    public void RecordNetworkFailure_PublishesTheNetworkFailureMetric()
    {
        _metricCounters.RecordNetworkFailure();
        _metricCounters.CollectMetrics();

        var metric = _publishedMetrics.Single();
        Assert.Multiple(() =>
        {
            Assert.That(metric.MetricNameModel.Name, Is.EqualTo("Supportability/DotNET/ContinuousProfiling/Export/failure_network"));
            Assert.That(metric.DataModel.Value0, Is.EqualTo(1));
        });
    }

    [Test]
    public void RecordTlsFailure_PublishesTheTlsFailureMetric()
    {
        _metricCounters.RecordTlsFailure();
        _metricCounters.CollectMetrics();

        var metric = _publishedMetrics.Single();
        Assert.Multiple(() =>
        {
            Assert.That(metric.MetricNameModel.Name, Is.EqualTo("Supportability/DotNET/ContinuousProfiling/Export/failure_tls"));
            Assert.That(metric.DataModel.Value0, Is.EqualTo(1));
        });
    }

    [Test]
    public void RecordHttpError_PublishesOneMetricPerStatusCodeWithItsCount()
    {
        _metricCounters.RecordHttpError(401);
        _metricCounters.RecordHttpError(401);
        _metricCounters.RecordHttpError(503);
        _metricCounters.CollectMetrics();

        var byName = _publishedMetrics.ToDictionary(m => m.MetricNameModel.Name, m => m.DataModel.Value0);
        Assert.That(byName, Is.EquivalentTo(new Dictionary<string, long>
        {
            ["Supportability/DotNET/ContinuousProfiling/HTTPError/401"] = 2,
            ["Supportability/DotNET/ContinuousProfiling/HTTPError/503"] = 1,
        }));
    }

    [Test]
    public void CollectMetrics_ResetsHttpErrorCountersAfterPublishing()
    {
        _metricCounters.RecordHttpError(500);
        _metricCounters.CollectMetrics();
        _publishedMetrics.Clear();

        _metricCounters.CollectMetrics();

        Assert.That(_publishedMetrics, Is.Empty);
    }

    [Test]
    public void RecordSendDuration_PublishesASummaryOfCountTotalMinAndMaxInSeconds()
    {
        _metricCounters.RecordSendDuration(TimeSpan.FromSeconds(2));
        _metricCounters.RecordSendDuration(TimeSpan.FromSeconds(0.5));
        _metricCounters.RecordSendDuration(TimeSpan.FromSeconds(1));
        _metricCounters.CollectMetrics();

        var metric = _publishedMetrics.Single();
        Assert.Multiple(() =>
        {
            Assert.That(metric.MetricNameModel.Name, Is.EqualTo("Supportability/DotNET/ContinuousProfiling/Duration"));
            Assert.That(metric.DataModel.Value0, Is.EqualTo(3), "call count");
            Assert.That(metric.DataModel.Value1, Is.EqualTo(3.5f).Within(0.001f), "total");
            Assert.That(metric.DataModel.Value3, Is.EqualTo(0.5f).Within(0.001f), "min");
            Assert.That(metric.DataModel.Value4, Is.EqualTo(2f).Within(0.001f), "max");
        });
    }

    [Test]
    public void CollectMetrics_ResetsTheDurationSummaryAfterPublishing()
    {
        _metricCounters.RecordSendDuration(TimeSpan.FromSeconds(9));
        _metricCounters.CollectMetrics();
        _publishedMetrics.Clear();

        _metricCounters.RecordSendDuration(TimeSpan.FromSeconds(1));
        _metricCounters.CollectMetrics();

        var metric = _publishedMetrics.Single();
        Assert.Multiple(() =>
        {
            Assert.That(metric.DataModel.Value0, Is.EqualTo(1));
            Assert.That(metric.DataModel.Value3, Is.EqualTo(1f).Within(0.001f), "min must not carry over the previous window's 9s");
            Assert.That(metric.DataModel.Value4, Is.EqualTo(1f).Within(0.001f));
        });
    }

    [Test]
    public void Published_metric_names_match_the_documented_literal_strings()
    {
        // The other tests here assert against MetricNames.* -- the same constants production uses to build
        // the metric -- so a typo in the constant would ship to the collector uncaught. Pin the exact wire
        // strings literally: a rename/typo of any CP export metric name now fails a test.
        _metricCounters.RecordExportSuccess();
        _metricCounters.RecordExportFailure();
        _metricCounters.RecordExportRetry();
        _metricCounters.RecordPayloadDropped();
        _metricCounters.RecordFullRejection();
        _metricCounters.CollectMetrics();

        var publishedNames = _publishedMetrics.Select(m => m.MetricNameModel.Name).ToList();

        Assert.That(publishedNames, Is.EquivalentTo(new[]
        {
            "Supportability/DotNET/ContinuousProfiling/Export/success",
            "Supportability/DotNET/ContinuousProfiling/Export/failure",
            "Supportability/DotNET/ContinuousProfiling/Export/retry",
            "Supportability/DotNET/ContinuousProfiling/Export/payload_dropped",
            "Supportability/DotNET/ContinuousProfiling/Export/full_rejection",
        }));
    }

    [Test]
    public void RegisterPublishMetricHandler_DoesNotThrowWhenCalledTwice()
    {
        Assert.DoesNotThrow(() => _metricCounters.RegisterPublishMetricHandler(metric => { }));
    }

    [Test]
    public void CollectMetrics_DoesNotThrow_WhenNoPublishDelegateIsRegistered()
    {
        var metricBuilder = WireModels.Utilities.GetSimpleMetricBuilder();
        var countersWithoutDelegate = new ContinuousProfilingSupportabilityMetricCounters(metricBuilder);

        countersWithoutDelegate.RecordExportSuccess();

        Assert.DoesNotThrow(() => countersWithoutDelegate.CollectMetrics());
    }

    [Test]
    public void CollectMetrics_SkipsPublish_WhenBuilderReturnsNullMetric()
    {
        // The builder returning null (e.g. metric name/value rejected) must be tolerated silently -- TrySend's
        // null check -- rather than publishing a null MetricWireModel or throwing.
        var metricBuilder = Mock.Create<IMetricBuilder>();
        Mock.Arrange(() => metricBuilder.TryBuildSupportabilityCountMetric(Arg.AnyString, Arg.AnyLong)).Returns((MetricWireModel)null);
        var counters = new ContinuousProfilingSupportabilityMetricCounters(metricBuilder);
        counters.RegisterPublishMetricHandler(metric => _publishedMetrics.Add(metric));

        counters.RecordExportSuccess();

        Assert.DoesNotThrow(() => counters.CollectMetrics());
        Assert.That(_publishedMetrics, Is.Empty);
    }

    [Test]
    public void CollectMetrics_LogsError_WhenPublishDelegateThrows()
    {
        // TrySend's catch around the registered delegate must swallow the exception rather than let it
        // propagate out of CollectMetrics.
        _metricCounters.RegisterPublishMetricHandler(metric => throw new InvalidOperationException("boom"));

        _metricCounters.RecordExportSuccess();

        Assert.DoesNotThrow(() => _metricCounters.CollectMetrics());
    }
}
