// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Api;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Extensions.Logging;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.OtelDistro;

[TestFixture]
public class DistroLogSinkTests
{
    private const string ValidTraceId = "0af7651916cd43dd8448eb211c80319c";
    private const string ValidSpanId = "b7ad6b7169203331";

    private IAgent _mockAgent;
    private IConfiguration _mockConfig;
    private DistroLogSink _sink;

    private int _callCount;
    private string _framework;
    private object _logEvent;
    private Func<object, DateTime> _getTimestamp;
    private Func<object, object> _getLevel;
    private Func<object, string> _getMessage;
    private Func<object, Exception> _getException;
    private Func<object, Dictionary<string, object>> _getContext;
    private string _spanId;
    private string _traceId;
    private bool _logInitialized;

    [TearDown]
    public void TearDown()
    {
        if (_logInitialized)
        {
            Log.Initialize(new NoOpLogger());
        }
    }

    [SetUp]
    public void SetUp()
    {
        _mockAgent = Mock.Create<IAgent>();
        _mockConfig = Mock.Create<IConfiguration>();
        Mock.Arrange(() => _mockAgent.Configuration).Returns(_mockConfig);
        Mock.Arrange(() => _mockConfig.LogEventCollectorEnabled).Returns(true);
        Mock.Arrange(() => _mockConfig.LogMetricsCollectorEnabled).Returns(false);
        Mock.Arrange(() => _mockConfig.ContextDataEnabled).Returns(false);

        _callCount = 0;
        Mock.Arrange(() => _mockAgent.RecordLogMessage(Arg.IsAny<string>(), Arg.IsAny<object>(), Arg.IsAny<Func<object, DateTime>>(),
                Arg.IsAny<Func<object, object>>(), Arg.IsAny<Func<object, string>>(), Arg.IsAny<Func<object, Exception>>(),
                Arg.IsAny<Func<object, Dictionary<string, object>>>(), Arg.IsAny<string>(), Arg.IsAny<string>()))
            .DoInstead((string framework, object logEvent, Func<object, DateTime> ts, Func<object, object> level, Func<object, string> message,
                Func<object, Exception> exception, Func<object, Dictionary<string, object>> context, string spanId, string traceId) =>
            {
                _callCount++;
                _framework = framework;
                _logEvent = logEvent;
                _getTimestamp = ts;
                _getLevel = level;
                _getMessage = message;
                _getException = exception;
                _getContext = context;
                _spanId = spanId;
                _traceId = traceId;
            });

        _sink = new DistroLogSink(_mockAgent);
    }

    private static DistroLogRecord NewRecord() => new()
    {
        Timestamp = new DateTime(2026, 11, 6, 1, 2, 3, DateTimeKind.Utc),
        LevelText = "Information",
        FormattedMessage = "hello 42",
        Body = "hello {n}",
        TraceId = ValidTraceId,
        SpanId = ValidSpanId
    };

    private static KeyValuePair<string, object> Pair(string key, object value) => new(key, value);

    [Test]
    public void Ctor_NullAgent_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new DistroLogSink(null));
    }

    [Test]
    public void Record_MapsTimestampLevelMessageAndIds()
    {
        var record = NewRecord();

        _sink.Record(record);

        Assert.Multiple(() =>
        {
            Assert.That(_callCount, Is.EqualTo(1));
            Assert.That(_framework, Is.EqualTo("OpenTelemetry"));
            Assert.That(DistroLogSink.FrameworkName, Is.EqualTo("OpenTelemetry"));
            Assert.That(_logEvent, Is.SameAs(record));
            Assert.That(_getTimestamp(_logEvent), Is.EqualTo(record.Timestamp));
            Assert.That(_getLevel(_logEvent), Is.EqualTo("INFORMATION"));
            Assert.That(_getMessage(_logEvent), Is.EqualTo("hello 42"));
            Assert.That(_traceId, Is.EqualTo(ValidTraceId));
            Assert.That(_spanId, Is.EqualTo(ValidSpanId));
        });
    }

    // Core calls getLevel(...).ToString(), so "no level" must be an empty string (-> UNKNOWN), never null.
    [TestCase(1, "TRACE")]
    [TestCase(5, "DEBUG")]
    [TestCase(9, "INFO")]
    [TestCase(12, "INFO")]
    [TestCase(13, "WARN")]
    [TestCase(17, "ERROR")]
    [TestCase(21, "FATAL")]
    [TestCase(null, "")]
    [TestCase(0, "")]
    [TestCase(25, "")]
    public void Record_NoLevelText_UsesSeverityRanges(int? severity, string expected)
    {
        var record = NewRecord();
        record.LevelText = " ";
        record.SeverityNumber = severity;

        _sink.Record(record);

        Assert.That(_getLevel(_logEvent), Is.EqualTo(expected));
    }

    [Test]
    public void Record_NoFormattedMessage_UsesBody()
    {
        var record = NewRecord();
        record.FormattedMessage = null;

        _sink.Record(record);

        Assert.That(_getMessage(_logEvent), Is.EqualTo("hello {n}"));
    }

    [Test]
    public void Record_ExceptionWithoutMessage_ForwardsException()
    {
        var record = NewRecord();
        record.FormattedMessage = null;
        record.Body = null;
        record.Exception = new InvalidOperationException("boom");

        _sink.Record(record);

        Assert.Multiple(() =>
        {
            Assert.That(_getMessage(_logEvent), Is.Null);
            Assert.That(_getException(_logEvent), Is.SameAs(record.Exception));
        });
    }

    [TestCase("00000000000000000000000000000000", ValidSpanId)]
    [TestCase(ValidTraceId, "0000000000000000")]
    [TestCase("0af7651916cd43dd", ValidSpanId)]
    [TestCase(ValidTraceId, "b7ad6b71692033311")]
    [TestCase("zaf7651916cd43dd8448eb211c80319c", ValidSpanId)]
    [TestCase(ValidTraceId, "g7ad6b7169203331")]
    [TestCase(null, ValidSpanId)]
    [TestCase(ValidTraceId, null)]
    public void Record_InvalidIds_PassesNullIds(string traceId, string spanId)
    {
        var record = NewRecord();
        record.TraceId = traceId;
        record.SpanId = spanId;

        _sink.Record(record);

        Assert.Multiple(() =>
        {
            Assert.That(_callCount, Is.EqualTo(1));
            Assert.That(_traceId, Is.Null);
            Assert.That(_spanId, Is.Null);
        });
    }

    [Test]
    public void Record_ContextDataDisabled_ContextGetterReturnsNull()
    {
        var record = NewRecord();
        record.Attributes = new[] { Pair("a", 1) };

        _sink.Record(record);

        Assert.That(_getContext(_logEvent), Is.Null);
    }

    [Test]
    public void Record_ContextDataEnabled_DropsOriginalFormatMergesScopesLastWinsSkipsNullsAndBlankKeys()
    {
        Mock.Arrange(() => _mockConfig.ContextDataEnabled).Returns(true);
        var record = NewRecord();
        record.Attributes = new[]
        {
            Pair("{OriginalFormat}", "hello {n}"),
            Pair("n", 42),
            Pair("shared", "attribute"),
            Pair("nullAttribute", null),
            Pair(null, "nullKey"),
            Pair("", "emptyKey"),
            Pair("  ", "whitespaceKey")
        };
        record.Scopes = new[]
        {
            Pair("{OriginalFormat}", "scope format"),
            Pair("shared", "scope"),
            Pair("scopeOnly", "s"),
            Pair("nullScope", null),
            Pair(" ", "whitespaceScopeKey")
        };

        _sink.Record(record);
        var context = _getContext(_logEvent);

        Assert.Multiple(() =>
        {
            Assert.That(context, Has.Count.EqualTo(4));
            Assert.That(context["n"], Is.EqualTo(42));
            Assert.That(context["shared"], Is.EqualTo("scope"));
            Assert.That(context["scopeOnly"], Is.EqualTo("s"));
            Assert.That(context["{OriginalFormat}"], Is.EqualTo("scope format"));
        });
    }

    [Test]
    public void Record_ContextDataEnabled_NullAttributesAndScopes_ReturnsEmptyContext()
    {
        Mock.Arrange(() => _mockConfig.ContextDataEnabled).Returns(true);

        _sink.Record(NewRecord());

        Assert.That(_getContext(_logEvent), Is.Empty);
    }

    [Test]
    public void Record_SinkNotEnabled_DoesNotCallAgent()
    {
        Mock.Arrange(() => _mockConfig.LogEventCollectorEnabled).Returns(false);
        Mock.Arrange(() => _mockConfig.LogMetricsCollectorEnabled).Returns(false);

        _sink.Record(NewRecord());

        Assert.That(_callCount, Is.EqualTo(0));
    }

    [Test]
    public void Record_AfterDisable_DoesNothing()
    {
        _sink.Disable();
        _sink.Disable();

        _sink.Record(NewRecord());

        Assert.Multiple(() =>
        {
            Assert.That(_sink.IsDisabled, Is.True);
            Assert.That(_callCount, Is.EqualTo(0));
        });
    }

    [TestCase(true, false, false, true)]
    [TestCase(false, true, false, true)]
    [TestCase(true, true, false, true)]
    [TestCase(false, false, false, false)]
    [TestCase(true, true, true, false)]
    public void Enabled_ReflectsForwardingOrMetricsAndDisable(bool forwarding, bool metrics, bool disabled, bool expected)
    {
        Mock.Arrange(() => _mockConfig.LogEventCollectorEnabled).Returns(forwarding);
        Mock.Arrange(() => _mockConfig.LogMetricsCollectorEnabled).Returns(metrics);
        if (disabled)
        {
            _sink.Disable();
        }

        Assert.That(_sink.Enabled, Is.EqualTo(expected));
    }

    [Test]
    public void Record_WhenMappingThrows_DoesNotThrowAndLogsOnce()
    {
        var warnings = new List<string>();
        var logger = Mock.Create<ILogger>();
        Mock.Arrange(() => logger.Warn(Arg.IsAny<Exception>(), Arg.IsAny<string>(), Arg.IsAny<object[]>()))
            .DoInstead((Exception exception, string message, object[] args) => warnings.Add(message));
        Log.Initialize(logger);
        _logInitialized = true;
        Mock.Arrange(() => _mockAgent.RecordLogMessage(Arg.IsAny<string>(), Arg.IsAny<object>(), Arg.IsAny<Func<object, DateTime>>(),
                Arg.IsAny<Func<object, object>>(), Arg.IsAny<Func<object, string>>(), Arg.IsAny<Func<object, Exception>>(),
                Arg.IsAny<Func<object, Dictionary<string, object>>>(), Arg.IsAny<string>(), Arg.IsAny<string>()))
            .Throws(new InvalidOperationException("boom"));

        Assert.DoesNotThrow(() => _sink.Record(NewRecord()));
        Assert.DoesNotThrow(() => _sink.Record(NewRecord()));

        Assert.Multiple(() =>
        {
            Assert.That(warnings, Has.Count.EqualTo(1));
            Assert.That(_sink.IsDisabled, Is.False);
        });
    }

    [Test]
    public void Record_WhenConfigurationThrows_DoesNotThrow()
    {
        Mock.Arrange(() => _mockConfig.LogEventCollectorEnabled).Throws(new InvalidOperationException("config boom"));

        Assert.DoesNotThrow(() => _sink.Record(NewRecord()));
        Assert.That(_callCount, Is.EqualTo(0));
    }

    [Test]
    public void Record_NullRecord_DoesNothing()
    {
        _sink.Record(null);

        Assert.That(_callCount, Is.EqualTo(0));
    }
}
