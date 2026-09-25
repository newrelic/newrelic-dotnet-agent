// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.ContinuousProfiling;
using NUnit.Framework;

namespace NewRelic.Agent.Core.UnitTest.ContinuousProfiling;

[TestFixture]
public class ContinuousProfilingCommandTypesTests
{
    [TestCase("all", true, true)]
    [TestCase("cpu", true, false)]
    [TestCase("heap", false, true)]
    [TestCase("bogus", false, false)]
    [TestCase(null, false, false)]
    [TestCase("", false, false)]
    [TestCase("ALL", false, false)] // matching is case-sensitive; an unrecognized case variant is reported unsupported, not normalized
    [TestCase("Cpu", false, false)]
    [TestCase(" ", false, false)]
    [TestCase(" cpu ", false, false)] // no trimming; surrounding whitespace makes the token unrecognized
    public void Classify_returns_expected_flags(string token, bool expectedStartsCpuBundle, bool expectedRequestsHeap)
    {
        ContinuousProfilingCommandTypes.Classify(token, out var startsCpuBundle, out var requestsHeap);

        Assert.Multiple(() =>
        {
            Assert.That(startsCpuBundle, Is.EqualTo(expectedStartsCpuBundle));
            Assert.That(requestsHeap, Is.EqualTo(expectedRequestsHeap));
        });
    }
}
