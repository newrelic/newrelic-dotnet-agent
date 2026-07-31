// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Api;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;

namespace CompositeTests;

/// <summary>
/// End-to-end coverage for server-side metric_name_rules. The agent must apply the rules and then
/// aggregate the results, so metrics whose names collapse onto one name are sent as a single metric
/// carrying their combined data rather than as duplicates that merely share a name.
/// </summary>
internal class MetricNameRuleTests
{
    private static CompositeTestAgent _compositeTestAgent;

    private IAgent _agent;

    [SetUp]
    public void SetUp()
    {
        _compositeTestAgent = new CompositeTestAgent();
        _agent = _compositeTestAgent.GetAgent();
    }

    [TearDown]
    public static void TearDown()
    {
        _compositeTestAgent.Dispose();
    }

    private void PushMetricNameRules(params ServerConfiguration.RegexRule[] rules)
    {
        _compositeTestAgent.ServerConfiguration.MetricNameRegexRules = rules;
        _compositeTestAgent.PushConfiguration();
    }

    private void RunTransactionWithTwoSegments()
    {
        var tx = _agent.CreateTransaction(
            isWeb: true,
            category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
            transactionDisplayName: "name",
            doNotTrackAsUnitOfWork: true);
        _agent.StartTransactionSegmentOrThrow("segmentA").End();
        _agent.StartTransactionSegmentOrThrow("segmentB").End();
        tx.End();
        _compositeTestAgent.Harvest();
    }

    [Test]
    public void MetricsCollapsedOntoOneNameByARule_AreAggregatedIntoASingleMetric()
    {
        PushMetricNameRules(new ServerConfiguration.RegexRule
        {
            MatchExpression = @"^DotNet/segment[AB]$",
            Replacement = "DotNet/merged",
            EvaluationOrder = 0
        });

        RunTransactionWithTwoSegments();

        // Both segments contribute to one unscoped and one scoped "DotNet/merged" metric, each with a
        // call count of 2 -- not two separate metrics of call count 1 that happen to share a name.
        MetricAssertions.MetricsExist(new List<ExpectedMetric>
        {
            new ExpectedCountMetric { Name = "DotNet/merged", CallCount = 2 },
            new ExpectedCountMetric { Name = "DotNet/merged", Scope = "WebTransaction/Action/name", CallCount = 2 }
        }, _compositeTestAgent.Metrics);

        MetricAssertions.MetricsDoNotExist(new List<ExpectedMetric>
        {
            new ExpectedMetric { Name = "DotNet/segmentA" },
            new ExpectedMetric { Name = "DotNet/segmentB" }
        }, _compositeTestAgent.Metrics);

        var mergedMetrics = _compositeTestAgent.Metrics
            .Where(metric => metric.MetricNameModel.Name == "DotNet/merged")
            .ToList();
        Assert.That(mergedMetrics, Has.Count.EqualTo(2),
            "Expected exactly one unscoped and one scoped 'DotNet/merged' metric. More than that means the renamed metrics were emitted separately instead of being merged.");
    }

    [Test]
    public void MetricsCollapsedOntoOneNameByARule_HaveTheirTimingDataCombined()
    {
        PushMetricNameRules(new ServerConfiguration.RegexRule
        {
            MatchExpression = @"^DotNet/segment[AB]$",
            Replacement = "DotNet/merged",
            EvaluationOrder = 0
        });

        RunTransactionWithTwoSegments();

        var unscopedMerged = _compositeTestAgent.Metrics
            .Single(metric => metric.MetricNameModel.Name == "DotNet/merged" && metric.MetricNameModel.Scope == null);
        var scopedMerged = _compositeTestAgent.Metrics
            .Single(metric => metric.MetricNameModel.Name == "DotNet/merged" && metric.MetricNameModel.Scope == "WebTransaction/Action/name");

        // Value1 is total time and Value2 total exclusive time; both segments' time must be present.
        Assert.Multiple(() =>
        {
            Assert.That(unscopedMerged.DataModel.Value0, Is.EqualTo(2));
            Assert.That(unscopedMerged.DataModel.Value1, Is.GreaterThanOrEqualTo(0));
            Assert.That(scopedMerged.DataModel.Value0, Is.EqualTo(2));
            Assert.That(scopedMerged.DataModel.Value1, Is.EqualTo(unscopedMerged.DataModel.Value1));
            Assert.That(scopedMerged.DataModel.Value2, Is.EqualTo(unscopedMerged.DataModel.Value2));
        });
    }

    [Test]
    public void MetricsMatchingAnIgnoreRule_AreNotSent()
    {
        PushMetricNameRules(new ServerConfiguration.RegexRule
        {
            MatchExpression = @"^DotNet/segmentA$",
            Ignore = true,
            EvaluationOrder = 0
        });

        RunTransactionWithTwoSegments();

        MetricAssertions.MetricsExist(new List<ExpectedMetric>
        {
            new ExpectedCountMetric { Name = "DotNet/segmentB", CallCount = 1 }
        }, _compositeTestAgent.Metrics);

        MetricAssertions.MetricsDoNotExist(new List<ExpectedMetric>
        {
            new ExpectedMetric { Name = "DotNet/segmentA" },
            new ExpectedMetric { Name = "DotNet/segmentA", Scope = "WebTransaction/Action/name" }
        }, _compositeTestAgent.Metrics);
    }

    [Test]
    public void RenameRulesAreAppliedExactlyOnceToEveryMetric()
    {
        // A catch-all, non-idempotent rule: applying it twice to the same name yields a doubled prefix.
        // Supportability metrics are the ones at risk here, because they are built (and previously
        // renamed) before they are aggregated, then renamed again at harvest.
        PushMetricNameRules(new ServerConfiguration.RegexRule
        {
            MatchExpression = "^(.*)$",
            Replacement = "Prefixed/$1",
            EvaluationOrder = 0
        });

        RunTransactionWithTwoSegments();

        var doublePrefixed = _compositeTestAgent.Metrics
            .Where(metric => metric.MetricNameModel.Name.StartsWith("Prefixed/Prefixed/"))
            .Select(metric => metric.MetricNameModel.Name)
            .ToList();

        Assert.That(doublePrefixed, Is.Empty,
            "These metric names were run through the rename rules twice: " + string.Join(", ", doublePrefixed));

        // Sanity check that the rule actually fired, so the assertion above cannot pass vacuously.
        Assert.That(_compositeTestAgent.Metrics.Select(metric => metric.MetricNameModel.Name),
            Has.All.StartsWith("Prefixed/"));
    }
}
