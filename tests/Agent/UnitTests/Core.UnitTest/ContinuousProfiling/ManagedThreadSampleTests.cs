// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Core.ContinuousProfiling;
using NUnit.Framework;

namespace NewRelic.Agent.Core.UnitTest.ContinuousProfiling;

[TestFixture]
public class ManagedThreadSampleTests
{
    [Test]
    public void Constructor_assigns_all_properties()
    {
        var frames = new List<string> { "Leaf", "Root" };

        var sample = new ManagedThreadSample("ThreadName", 42, 1, 2, 3, frames, true, true);

        Assert.Multiple(() =>
        {
            Assert.That(sample.ThreadName, Is.EqualTo("ThreadName"));
            Assert.That(sample.OsThreadId, Is.EqualTo(42));
            Assert.That(sample.TraceIdHigh, Is.EqualTo(1));
            Assert.That(sample.TraceIdLow, Is.EqualTo(2));
            Assert.That(sample.SpanId, Is.EqualTo(3));
            Assert.That(sample.Frames, Is.SameAs(frames));
            Assert.That(sample.OnCpu, Is.True);
            Assert.That(sample.IsAgentWork, Is.True);
        });
    }

    [Test]
    public void IsAgentWork_defaults_to_false_when_not_specified()
    {
        var sample = new ManagedThreadSample("ThreadName", 1, 0, 0, 0, new List<string>(), false);

        Assert.That(sample.IsAgentWork, Is.False);
    }
}
