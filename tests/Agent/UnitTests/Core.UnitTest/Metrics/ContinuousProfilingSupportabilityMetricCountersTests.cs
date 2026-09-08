// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.WireModels;
using NUnit.Framework;

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
}
