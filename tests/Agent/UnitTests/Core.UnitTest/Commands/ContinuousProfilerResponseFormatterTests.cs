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
    public void AckOnly_returns_an_empty_dictionary_when_there_are_no_exceptions()
    {
        var result = new ContinuousProfilingCommandResult(new[] { "cpu" }, 10000, 10000, new Dictionary<string, string>());

        var response = new AckOnlyContinuousProfilerResponseFormatter().Format(result);

        Assert.That(response, Is.Empty);
    }

    [Test]
    public void AckOnly_surfaces_exceptions_under_the_errors_key()
    {
        var result = new ContinuousProfilingCommandResult(new[] { "cpu" }, 10000, 10000, new Dictionary<string, string> { { "heap", "not supported" } });

        var response = new AckOnlyContinuousProfilerResponseFormatter().Format(result);

        Assert.That(response["errors"], Is.EqualTo("heap: not supported"));
    }

    [Test]
    public void AckOnly_joins_multiple_exceptions_under_the_errors_key()
    {
        var exceptions = new Dictionary<string, string> { { "heap", "not supported" }, { "cpu", "failed to start: boom" } };
        var result = new ContinuousProfilingCommandResult(new[] { "cpu" }, 10000, 10000, exceptions);

        var response = new AckOnlyContinuousProfilerResponseFormatter().Format(result);

        Assert.That(response["errors"], Is.EqualTo("heap: not supported; cpu: failed to start: boom"));
    }
}
