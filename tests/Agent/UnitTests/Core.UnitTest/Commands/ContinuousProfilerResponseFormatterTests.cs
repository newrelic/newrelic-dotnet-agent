// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Core.Commands;
using NewRelic.Agent.Core.ContinuousProfiling;
using NUnit.Framework;

namespace NewRelic.Agent.Core.UnitTest.Commands;

[TestFixture]
public class ContinuousProfilerResponseFormatterTests
{
    [Test]
    public void AckOnly_always_returns_an_empty_dictionary()
    {
        // "bogus" rather than "heap": every recognized token ("all"/"cpu"/"heap") is acted on now, so an
        // unrecognized token is the only thing that produces an exception entry.
        var result = new ContinuousProfilingCommandResult(new[] { "cpu" }, 10000, 10000, new Dictionary<string, string> { { "bogus", "not supported" } });

        var response = new AckOnlyContinuousProfilerResponseFormatter().Format(result);

        Assert.That(response, Is.Empty);
    }

    [Test]
    public void Detailed_includes_intervals_and_active_types_without_exceptions_key_when_none()
    {
        var result = new ContinuousProfilingCommandResult(new[] { "cpu" }, 10000, 10000, new Dictionary<string, string>());

        var response = new DetailedContinuousProfilerResponseFormatter().Format(result);

        Assert.Multiple(() =>
        {
            Assert.That(response["include"], Is.EqualTo(new[] { "cpu" }));
            Assert.That(response["sample_interval"], Is.EqualTo(10000));
            Assert.That(response["cpu_report_interval"], Is.EqualTo(10000));
            Assert.That(response.ContainsKey("exceptions"), Is.False);
        });
    }

    [Test]
    public void Detailed_includes_exceptions_key_when_present()
    {
        var exceptions = new Dictionary<string, string> { { "bogus", "not supported" } };
        var result = new ContinuousProfilingCommandResult(new[] { "cpu" }, 10000, 10000, exceptions);

        var response = new DetailedContinuousProfilerResponseFormatter().Format(result);

        Assert.That(response["exceptions"], Is.EqualTo(exceptions));
    }
}
