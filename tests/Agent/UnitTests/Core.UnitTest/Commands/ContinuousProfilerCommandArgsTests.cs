// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Core.Commands;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace NewRelic.Agent.Core.UnitTest.Commands;

[TestFixture]
public class ContinuousProfilerCommandArgsTests
{
    [Test]
    public void Parses_a_JArray_include_list()
    {
        var arguments = new Dictionary<string, object> { { "include", new JArray("cpu", "heap") } };

        var args = new ContinuousProfilerCommandArgs(arguments);

        Assert.That(args.Include, Is.EqualTo(new[] { "cpu", "heap" }));
    }

    [Test]
    public void Missing_include_parses_as_empty()
    {
        var args = new ContinuousProfilerCommandArgs(new Dictionary<string, object>());

        Assert.That(args.Include, Is.Empty);
    }

    [Test]
    public void A_bare_string_include_parses_as_a_single_element_list()
    {
        var arguments = new Dictionary<string, object> { { "include", "all" } };

        var args = new ContinuousProfilerCommandArgs(arguments);

        Assert.That(args.Include, Is.EqualTo(new[] { "all" }));
    }

    [Test]
    public void Parses_sample_interval_and_cpu_report_interval_when_present()
    {
        var arguments = new Dictionary<string, object> { { "sample_interval", "1000" }, { "cpu_report_interval", 10000 } };

        var args = new ContinuousProfilerCommandArgs(arguments);

        Assert.Multiple(() =>
        {
            Assert.That(args.SampleIntervalMs, Is.EqualTo(1000));
            Assert.That(args.CpuReportIntervalMs, Is.EqualTo(10000));
        });
    }

    [Test]
    public void Missing_or_unparseable_intervals_are_null()
    {
        var arguments = new Dictionary<string, object> { { "sample_interval", "not-a-number" } };

        var args = new ContinuousProfilerCommandArgs(arguments);

        Assert.Multiple(() =>
        {
            Assert.That(args.SampleIntervalMs, Is.Null);
            Assert.That(args.CpuReportIntervalMs, Is.Null);
        });
    }

    [Test]
    public void A_zero_or_negative_interval_is_treated_as_not_provided()
    {
        var arguments = new Dictionary<string, object> { { "sample_interval", 0 }, { "cpu_report_interval", -5 } };

        var args = new ContinuousProfilerCommandArgs(arguments);

        Assert.Multiple(() =>
        {
            Assert.That(args.SampleIntervalMs, Is.Null);
            Assert.That(args.CpuReportIntervalMs, Is.Null);
        });
    }
}
