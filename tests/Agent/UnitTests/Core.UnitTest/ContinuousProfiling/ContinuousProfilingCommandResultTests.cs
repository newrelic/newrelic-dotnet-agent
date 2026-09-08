// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Core.ContinuousProfiling;
using NUnit.Framework;

namespace NewRelic.Agent.Core.UnitTest.ContinuousProfiling;

[TestFixture]
public class ContinuousProfilingCommandResultTests
{
    [Test]
    public void Constructor_assigns_all_properties()
    {
        var activeTypes = new List<string> { "cpu" };
        var exceptions = new Dictionary<string, string> { { "heap", "not supported" } };

        var result = new ContinuousProfilingCommandResult(activeTypes, 500, 60000, exceptions);

        Assert.Multiple(() =>
        {
            Assert.That(result.ActiveTypes, Is.SameAs(activeTypes));
            Assert.That(result.SampleIntervalMs, Is.EqualTo(500));
            Assert.That(result.CpuReportIntervalMs, Is.EqualTo(60000));
            Assert.That(result.Exceptions, Is.SameAs(exceptions));
        });
    }

    [Test]
    public void Constructor_allows_empty_activeTypes_and_exceptions()
    {
        var result = new ContinuousProfilingCommandResult(new List<string>(), 0, 0, new Dictionary<string, string>());

        Assert.Multiple(() =>
        {
            Assert.That(result.ActiveTypes, Is.Empty);
            Assert.That(result.Exceptions, Is.Empty);
        });
    }
}
