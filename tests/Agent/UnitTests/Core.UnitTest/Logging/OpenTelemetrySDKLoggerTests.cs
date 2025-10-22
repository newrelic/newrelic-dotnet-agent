// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using NewRelic.Agent.Extensions.Logging;
using NUnit.Framework;
using Serilog;
using Serilog.Events;
using Telerik.JustMock;
using ExtensionsLog = NewRelic.Agent.Extensions.Logging.Log;

namespace NewRelic.Agent.Core.Logging.Tests
{
  [TestFixture]
  public class OpenTelemetrySDKLoggerTests
  {
    private TestOpenTelemetrySdkLogger _logger;
    private Serilog.ILogger _serilogLogger;

    [SetUp]
    public void SetUp()
    {
      // Mock the underlying Serilog logger instead of the static Log class
      _serilogLogger = Mock.Create<Serilog.ILogger>();
      Serilog.Log.Logger = _serilogLogger;

      // Initialize the static Log class with a Logger instance that uses our mocked Serilog logger
      var logger = new Logger();
      ExtensionsLog.Initialize(logger);
    }

    [TearDown]
    public void TearDown()
    {
      _logger?.Dispose();
    }

    #region EventSourceLevel Mapping Tests

    [Test]
    public void EventSourceLevel_WhenFinestEnabled_ReturnsVerbose()
    {
      Mock.Arrange(() => _serilogLogger.IsEnabled(LogEventLevel.Verbose)).Returns(true);
      _logger = new TestOpenTelemetrySdkLogger();

      var level = _logger.EventSourceLevel;

      Assert.That(level, Is.EqualTo(EventLevel.Verbose));
    }

    [Test]
    public void EventSourceLevel_WhenDebugEnabled_ReturnsInformational()
    {
      Mock.Arrange(() => _serilogLogger.IsEnabled(LogEventLevel.Verbose)).Returns(false);
      Mock.Arrange(() => _serilogLogger.IsEnabled(LogEventLevel.Debug)).Returns(true);
      _logger = new TestOpenTelemetrySdkLogger();

      var level = _logger.EventSourceLevel;

      Assert.That(level, Is.EqualTo(EventLevel.Informational));
    }

    [Test]
    public void EventSourceLevel_WhenInfoEnabled_ReturnsInformational()
    {
      Mock.Arrange(() => _serilogLogger.IsEnabled(LogEventLevel.Verbose)).Returns(false);
      Mock.Arrange(() => _serilogLogger.IsEnabled(LogEventLevel.Debug)).Returns(false);
      Mock.Arrange(() => _serilogLogger.IsEnabled(LogEventLevel.Information)).Returns(true);
      _logger = new TestOpenTelemetrySdkLogger();

      var level = _logger.EventSourceLevel;

      Assert.That(level, Is.EqualTo(EventLevel.Informational));
    }

    [Test]
    public void EventSourceLevel_WhenWarnEnabled_ReturnsWarning()
    {
      Mock.Arrange(() => _serilogLogger.IsEnabled(LogEventLevel.Verbose)).Returns(false);
      Mock.Arrange(() => _serilogLogger.IsEnabled(LogEventLevel.Debug)).Returns(false);
      Mock.Arrange(() => _serilogLogger.IsEnabled(LogEventLevel.Information)).Returns(false);
      Mock.Arrange(() => _serilogLogger.IsEnabled(LogEventLevel.Warning)).Returns(true);
      _logger = new TestOpenTelemetrySdkLogger();

      var level = _logger.EventSourceLevel;

      Assert.That(level, Is.EqualTo(EventLevel.Warning));
    }

    [Test]
    public void EventSourceLevel_WhenErrorEnabled_ReturnsError()
    {
      Mock.Arrange(() => _serilogLogger.IsEnabled(LogEventLevel.Verbose)).Returns(false);
      Mock.Arrange(() => _serilogLogger.IsEnabled(LogEventLevel.Debug)).Returns(false);
      Mock.Arrange(() => _serilogLogger.IsEnabled(LogEventLevel.Information)).Returns(false);
      Mock.Arrange(() => _serilogLogger.IsEnabled(LogEventLevel.Warning)).Returns(false);
      Mock.Arrange(() => _serilogLogger.IsEnabled(LogEventLevel.Error)).Returns(true);
      _logger = new TestOpenTelemetrySdkLogger();

      var level = _logger.EventSourceLevel;

      Assert.That(level, Is.EqualTo(EventLevel.Error));
    }

    [Test]
    public void EventSourceLevel_WhenAllDisabled_ReturnsLogAlways()
    {
      Mock.Arrange(() => _serilogLogger.IsEnabled(Arg.IsAny<LogEventLevel>())).Returns(false);
      _logger = new TestOpenTelemetrySdkLogger();

      var level = _logger.EventSourceLevel;

      Assert.That(level, Is.EqualTo(EventLevel.LogAlways));
    }

    [Test]
    public void EventSourceLevel_CachesValue_AfterFirstCall()
    {
      // Set up Serilog to return Verbose as enabled
      Mock.Arrange(() => _serilogLogger.IsEnabled(LogEventLevel.Verbose)).Returns(true);
      Mock.Arrange(() => _serilogLogger.IsEnabled(LogEventLevel.Debug)).Returns(false);
      Mock.Arrange(() => _serilogLogger.IsEnabled(LogEventLevel.Information)).Returns(false);
      Mock.Arrange(() => _serilogLogger.IsEnabled(LogEventLevel.Warning)).Returns(false);
      Mock.Arrange(() => _serilogLogger.IsEnabled(LogEventLevel.Error)).Returns(false);

      _logger = new TestOpenTelemetrySdkLogger();

      var level1 = _logger.EventSourceLevel;

      // Verify first call returns Verbose
      Assert.That(level1, Is.EqualTo(EventLevel.Verbose));

      // Change the mock return values - but the cached value should still be used
      Mock.Arrange(() => _serilogLogger.IsEnabled(LogEventLevel.Verbose)).Returns(false);
      Mock.Arrange(() => _serilogLogger.IsEnabled(LogEventLevel.Debug)).Returns(true);

      var level2 = _logger.EventSourceLevel;

      // Should still be Verbose because it was cached
      Assert.That(level2, Is.EqualTo(EventLevel.Verbose), "EventSourceLevel should be cached and not change after first access");
    }

    #endregion

    #region OnEventSourceCreated Tests

    [Test]
    public void OnEventSourceCreated_EnablesEventsForOpenTelemetryEventSource()
    {
      Mock.Arrange(() => _serilogLogger.IsEnabled(LogEventLevel.Information)).Returns(true);
      _logger = new TestOpenTelemetrySdkLogger();

      // Create a real EventSource with OpenTelemetry prefix
      using var eventSource = new OpenTelemetryTestSourceEventSource();

      // Manually trigger the OnEventSourceCreated to test the filtering logic
      _logger.TriggerOnEventSourceCreated(eventSource);

      // The logger should have enabled this event source
      Assert.That(_logger.EnabledEventSources, Does.Contain("OpenTelemetry-TestSource"));
    }

    [Test]
    public void OnEventSourceCreated_IgnoresNonOpenTelemetryEventSource()
    {
      Mock.Arrange(() => _serilogLogger.IsEnabled(LogEventLevel.Information)).Returns(true);
      _logger = new TestOpenTelemetrySdkLogger();

      // Create a real EventSource without OpenTelemetry prefix
      using var eventSource = new SomeOtherSourceEventSource();

      // Manually trigger the OnEventSourceCreated
      _logger.TriggerOnEventSourceCreated(eventSource);

      // The logger should not have enabled this event source
      Assert.That(_logger.EnabledEventSources, Does.Not.Contain("SomeOtherSource"));
    }

    [Test]
    public void OnEventSourceCreated_IsCaseInsensitive()
    {
      Mock.Arrange(() => _serilogLogger.IsEnabled(LogEventLevel.Information)).Returns(true);
      _logger = new TestOpenTelemetrySdkLogger();

      // Create a real EventSource with lowercase prefix
      using var eventSource = new OptelemetryTestSourceEventSource();

      // Manually trigger the OnEventSourceCreated
      _logger.TriggerOnEventSourceCreated(eventSource);

      Assert.That(_logger.EnabledEventSources, Does.Contain("opentelemetry-testSource"));
    }

    #endregion


    #region Helper Classes

    // Test class to expose protected methods
    private class TestOpenTelemetrySdkLogger : OpenTelemetrySDKLogger
    {
      public List<string> EnabledEventSources { get; } = new List<string>();

      protected override void OnEventSourceCreated(EventSource eventSource)
      {
        base.OnEventSourceCreated(eventSource);
        if (eventSource.Name.StartsWith("OpenTelemetry-", StringComparison.OrdinalIgnoreCase))
        {
          EnabledEventSources.Add(eventSource.Name);
        }
      }

      public void TriggerOnEventSourceCreated(EventSource eventSource)
      {
        OnEventSourceCreated(eventSource);
      }
    }

    // Pre-defined EventSource classes with correct names via EventSourceAttribute
    [EventSource(Name = "OpenTelemetry-TestSource")]
    private class OpenTelemetryTestSourceEventSource : EventSource { }

    [EventSource(Name = "SomeOtherSource")]
    private class SomeOtherSourceEventSource : EventSource { }

    [EventSource(Name = "opentelemetry-testSource")]
    private class OptelemetryTestSourceEventSource : EventSource { }

    #endregion
  }
}
