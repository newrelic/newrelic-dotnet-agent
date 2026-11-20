// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace NewRelic.Agent.Core.OtelDistro;

[TestFixture]
public class DistroConfigMetricsTests
{
    [SetUp]
    public void SetUp()
    {
        DistroConfigMetrics.ResetForTesting();
    }

    [TearDown]
    public void TearDown()
    {
        DistroConfigMetrics.ResetForTesting();
    }

    [Test]
    public void Record_BeforeSink_QueuedThenFlushedOnceInOrder()
    {
        DistroConfigMetrics.Record("A");
        DistroConfigMetrics.Record("B");

        var first = new List<string>();
        DistroConfigMetrics.SetMetricSink(first.Add);
        var second = new List<string>();
        DistroConfigMetrics.SetMetricSink(second.Add);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(new[] { "A", "B" }));
            Assert.That(second, Is.Empty);
        });
    }

    // Counted per name while queued, so the queue stays bounded however often a name is recorded before connect.
    [Test]
    public void Record_SameNameRepeatedBeforeSink_FlushedOncePerRecordInFirstRecordedOrder()
    {
        DistroConfigMetrics.Record("A");
        DistroConfigMetrics.Record("B");
        DistroConfigMetrics.Record("A");

        var sent = new List<string>();
        DistroConfigMetrics.SetMetricSink(sent.Add);

        Assert.That(sent, Is.EqualTo(new[] { "A", "A", "B" }));
    }

    [Test]
    public void RecordCount_WithSink_SendsNameCountTimes()
    {
        var sent = new List<string>();
        DistroConfigMetrics.SetMetricSink(sent.Add);

        DistroConfigMetrics.Record("A", 3);

        Assert.That(sent, Is.EqualTo(new[] { "A", "A", "A" }));
    }

    [TestCase(0)]
    [TestCase(-5)]
    public void RecordCount_NonPositive_IsIgnored(int count)
    {
        var sent = new List<string>();
        DistroConfigMetrics.SetMetricSink(sent.Add);

        DistroConfigMetrics.Record("A", count);

        Assert.That(sent, Is.Empty);
    }

    [Test]
    public void RecordCount_BeforeSink_QueuesTheBatchAndFlushesThatMany()
    {
        DistroConfigMetrics.Record("A", 2);
        DistroConfigMetrics.Record("A"); // +1 single
        DistroConfigMetrics.Record("B", 3);

        var sent = new List<string>();
        DistroConfigMetrics.SetMetricSink(sent.Add);

        Assert.That(sent, Is.EqualTo(new[] { "A", "A", "A", "B", "B", "B" }));
    }

    [Test]
    public void Record_WithSink_SendsImmediately()
    {
        var sent = new List<string>();
        DistroConfigMetrics.SetMetricSink(sent.Add);

        DistroConfigMetrics.Record("A");

        Assert.That(sent, Is.EqualTo(new[] { "A" }));
    }

    [Test]
    public void SetMetricSink_Null_DetachesAndQueuesAgain()
    {
        var sent = new List<string>();
        DistroConfigMetrics.SetMetricSink(sent.Add);
        DistroConfigMetrics.SetMetricSink(null);

        DistroConfigMetrics.Record("A");
        Assert.That(sent, Is.Empty);

        DistroConfigMetrics.SetMetricSink(sent.Add);
        Assert.That(sent, Is.EqualTo(new[] { "A" }));
    }

    [Test]
    public void ThrowingSink_NeverThrows()
    {
        Action<string> throwing = _ => throw new InvalidOperationException("sink failed");

        DistroConfigMetrics.Record("A");
        Assert.DoesNotThrow(() => DistroConfigMetrics.SetMetricSink(throwing));
        Assert.DoesNotThrow(() => DistroConfigMetrics.Record("B"));
    }

    [Test]
    public void ResetForTesting_DropsQueueAndSink()
    {
        var sent = new List<string>();
        DistroConfigMetrics.SetMetricSink(sent.Add);
        DistroConfigMetrics.ResetForTesting();
        DistroConfigMetrics.Record("A");
        DistroConfigMetrics.ResetForTesting();

        DistroConfigMetrics.SetMetricSink(sent.Add);

        Assert.That(sent, Is.Empty);
    }
}
