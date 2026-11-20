// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework;

namespace NewRelic.OtelDistro.Generators.Tests;

[TestFixture]
public class LogForwardingClassificationTests
{
    // The exact set of NR log-recording tracer factories, pinned so a wrapper rename (which would silently stop the
    // dedupe or stop disabling the NLog bridge) fails here. Names are from the shipped wrapper Instrumentation.xml.
    [Test]
    public void LogForwardingTracerFactories_AreTheFiveLogRecordingWrappers()
    {
        Assert.That(LogForwardingClassification.LogForwardingTracerFactories, Is.EquivalentTo(new[]
        {
            "MicrosoftLogging", "log4net", "SitecoreLogging", "SerilogDispatchWrapper", "nlog",
        }));
    }

    [TestCase("MicrosoftLogging")]
    [TestCase("log4net")]
    [TestCase("SitecoreLogging")]
    [TestCase("SerilogDispatchWrapper")]
    [TestCase("nlog")]
    public void IsLogForwarding_LogRecordingFactory_True(string tracerFactoryName)
    {
        Assert.That(LogForwardingClassification.IsLogForwarding(tracerFactoryName), Is.True);
    }

    // The logger-wiring/creation factories and an ordinary (case-mismatched or unrelated) factory are not log-forwarding.
    [TestCase("AddProviderRegistrationWrapper")]
    [TestCase("SerilogCreateLoggerWrapper")]
    [TestCase("microsoftlogging")]
    [TestCase("NLOG")]
    [TestCase("Factory")]
    [TestCase("")]
    [TestCase(null)]
    public void IsLogForwarding_OtherFactory_False(string tracerFactoryName)
    {
        Assert.That(LogForwardingClassification.IsLogForwarding(tracerFactoryName), Is.False);
    }

    [Test]
    public void IsNLog_OnlyTheNLogFactory()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LogForwardingClassification.IsNLog("nlog"), Is.True);
            Assert.That(LogForwardingClassification.IsNLog(LogForwardingClassification.NLogTracerFactoryName), Is.True);
            Assert.That(LogForwardingClassification.IsNLog("NLOG"), Is.False);
            Assert.That(LogForwardingClassification.IsNLog("MicrosoftLogging"), Is.False);
            Assert.That(LogForwardingClassification.IsNLog(null), Is.False);
        });
    }
}
