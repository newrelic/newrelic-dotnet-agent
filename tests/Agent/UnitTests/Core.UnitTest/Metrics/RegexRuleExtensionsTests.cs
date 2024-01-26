// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Metrics
{
    /// <summary>
    /// This class only verifies that regex rules are applied correctly with just a few simple test cases. 
    /// 
    /// For more complicated test cases, especially multi-rule test cases, see MetricNameServiceTests -- particularly the cross-agent tests.
    /// </summary>
    [TestFixture]
    public class RegexRuleExtensionsTests
    {
        [Test]
        public void ApplyTo_ReturnsCorrectResult_ForSimpleRule()
        {
            var rule = new RegexRule("/test/.*", "/test", false, 0, false, false, false);
            Assert.That(rule.ApplyTo("WebTransaction/NormalizedUri/test/ohdude").Replacement, Is.EqualTo("WebTransaction/NormalizedUri/test"));
        }

        [Test]
        public void ApplyTo_ReturnsCorrectResult_ForBackreferencedRule()
        {
            var rule = new RegexRule("/test/(.*)/(.*)", "/blah/$2/$1", false, 0, false, false, false);
            Assert.That(rule.ApplyTo("WebTransaction/NormalizedUri/test/ohdude/ohman").Replacement, Is.EqualTo("WebTransaction/NormalizedUri/blah/ohman/ohdude"));
        }

        [Test]
        public void ApplyTo_ReturnsCorrectResult_ForJadeRule()
        {
            var rule = new RegexRule("/Fixture/[^/]*/id/\\*/tabid/\\*/Default\\.aspx$", "/Fixture/*/id/*/tabid/*/Default.aspx", false, 0, false, false, false);
            Assert.That(rule.ApplyTo("WebTransaction/NormalizedUri/Fixture/BluesVsStormers/id/*/tabid/*/Default.aspx").Replacement, Is.EqualTo("WebTransaction/NormalizedUri/Fixture/*/id/*/tabid/*/Default.aspx"));
        }

        [Test]
        [TestCase("WebTransaction/NormalizedUri/dude/social/rest/test/blah/blah", "WebTransaction/NormalizedUri/dude/social/rest/test/*")]
        [TestCase("WebTransaction/NormalizedUri/meet/social/rest/stop/blah/blah", "WebTransaction/NormalizedUri/meet/social/rest/stop/*")]
        public void ApplyTo_ReturnsCorrectResult_ForComplicatedRule(string input, string expectedOutput)
        {
            var rule = new RegexRule("(.*)/social/rest/([^/]*)/.*", "$1/social/rest/$2/*", false, 0, false, false, false);
            var output = rule.ApplyTo(input).Replacement;
            Assert.That(output, Is.EqualTo(expectedOutput));
        }

        [Test]
        public void ApplyTo_ReturnsCorrectResult_ForIgnoreRule()
        {
            var rule = new RegexRule("/test/.*", null, true, 0, false, false, false);

            var result = rule.ApplyTo("WebTransaction/NormalizedUri/test/ignore");

            Assert.Multiple(() =>
            {
                Assert.That(result.IsMatch, Is.True);
                Assert.That(result.Replacement, Is.Null);
            });
        }

        [Test]
        public void ApplyTo_ReturnsNonMatch_IfRuleDoesNotMatch()
        {
            var rule = new RegexRule("apple", "banana", false, 1, true, true, true);
            var result = rule.ApplyTo("cthulu");

            Assert.That(result.IsMatch, Is.False);
        }
    }
}

