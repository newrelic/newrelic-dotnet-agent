// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework;

namespace NewRelic.Agent.Core.Logging;

[TestFixture]
public class OpenTelemetryLogLevelMapperTests
{
    [TestCase("none", "OFF")]
    [TestCase("error", "ERROR")]
    [TestCase("warn", "WARN")]
    [TestCase("info", "INFO")]
    [TestCase("debug", "DEBUG")]
    public void TryMapToAgentLogLevel_MapsEverySupportedValue(string otelLogLevel, string expected)
    {
        Assert.That(OpenTelemetryLogLevelMapper.TryMapToAgentLogLevel(otelLogLevel, out var agentLogLevel), Is.True);
        Assert.That(agentLogLevel, Is.EqualTo(expected));
    }

    [TestCase("NONE", "OFF")]
    [TestCase("Debug", "DEBUG")]
    [TestCase("wArN", "WARN")]
    public void TryMapToAgentLogLevel_IsCaseInsensitive(string otelLogLevel, string expected)
    {
        Assert.That(OpenTelemetryLogLevelMapper.TryMapToAgentLogLevel(otelLogLevel, out var agentLogLevel), Is.True);
        Assert.That(agentLogLevel, Is.EqualTo(expected));
    }

    [TestCase("  debug  ", "DEBUG")]
    public void TryMapToAgentLogLevel_TrimsSurroundingWhitespace(string otelLogLevel, string expected)
    {
        Assert.That(OpenTelemetryLogLevelMapper.TryMapToAgentLogLevel(otelLogLevel, out var agentLogLevel), Is.True);
        Assert.That(agentLogLevel, Is.EqualTo(expected));
    }

    // "finest", "all", and "off" are New Relic level names, not OpenTelemetry ones. "warning" and
    // "informational" are EventLevel names, which belong to OTEL_DIAGNOSTICS.json, not here.
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("notalevel")]
    [TestCase("finest")]
    [TestCase("all")]
    [TestCase("off")]
    [TestCase("warning")]
    [TestCase("informational")]
    [TestCase("verbose")]
    [TestCase("critical")]
    [TestCase("4")]
    public void TryMapToAgentLogLevel_ReturnsFalseForUnsupportedValues(string otelLogLevel)
    {
        Assert.That(OpenTelemetryLogLevelMapper.TryMapToAgentLogLevel(otelLogLevel, out var agentLogLevel), Is.False);
        Assert.That(agentLogLevel, Is.Null);
    }
}
