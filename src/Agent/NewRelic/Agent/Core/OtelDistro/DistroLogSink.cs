// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Threading;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>
/// Forwards neutral <see cref="DistroLogRecord"/>s to Core's existing log path
/// (<see cref="IAgentExperimental.RecordLogMessage"/>), so they get the same deny list, log metrics, forwarding,
/// context-data filtering and transaction batching as agent mode. Runs on the application's thread: never throws.
/// </summary>
public class DistroLogSink
{
    public const string FrameworkName = "OpenTelemetry";

    private const string OriginalFormatKey = "{OriginalFormat}";

    private static readonly Func<object, DateTime> GetTimestamp = record => ((DistroLogRecord)record).Timestamp;
    private static readonly Func<object, object> GetLevel = record => MapLevel((DistroLogRecord)record) ?? string.Empty;
    private static readonly Func<object, string> GetMessage = record => ((DistroLogRecord)record).FormattedMessage ?? ((DistroLogRecord)record).Body;
    private static readonly Func<object, Exception> GetException = record => ((DistroLogRecord)record).Exception;

    private readonly IAgent _agent;
    private readonly Func<object, Dictionary<string, object>> _getContextData;

    // Set once the agent shuts down. The caller's logger outlives the agent, so Record checks this first.
    private volatile bool _disabled;

    private int _loggedRecordError;

    /// <summary>
    /// Whether records are worth mapping at all: not disabled, and log forwarding or log metrics is on.
    /// </summary>
    public bool Enabled
    {
        get
        {
            if (_disabled)
            {
                return false;
            }

            var configuration = _agent.Configuration;
            return configuration.LogEventCollectorEnabled || configuration.LogMetricsCollectorEnabled;
        }
    }

    /// <summary>
    /// Whether <see cref="Disable"/> has been called.
    /// </summary>
    public bool IsDisabled => _disabled;

    public DistroLogSink(IAgent agent)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _getContextData = GetContextData;
    }

    /// <summary>
    /// Stops feeding Core. Called when the agent shuts down. Irreversible and idempotent.
    /// </summary>
    public void Disable()
    {
        _disabled = true;
    }

    public void Record(DistroLogRecord record)
    {
        if (record == null)
        {
            return;
        }

        try
        {
            if (!Enabled)
            {
                return;
            }

            var traceId = record.TraceId;
            var spanId = record.SpanId;
            if (!AreValidIds(traceId, spanId))
            {
                traceId = null;
                spanId = null;
            }

            _agent.GetExperimentalApi().RecordLogMessage(FrameworkName, record, GetTimestamp, GetLevel, GetMessage, GetException, _getContextData, spanId, traceId);
        }
        catch (Exception exception)
        {
            if (Interlocked.Exchange(ref _loggedRecordError, 1) == 0)
            {
                Log.Warn(exception, "OTel distro log sink failed to record a log record. Later failures will not be logged.");
            }
        }
    }

    // Only built when Core asks for it, and Core asks only when forwarding is on; the config check keeps the
    // "context data off means no context" rule local to the sink.
    private Dictionary<string, object> GetContextData(object logEvent)
    {
        if (!_agent.Configuration.ContextDataEnabled)
        {
            return null;
        }

        var record = (DistroLogRecord)logEvent;
        var context = new Dictionary<string, object>();
        AddNonNull(context, record.Attributes, skipOriginalFormat: true);
        AddNonNull(context, record.Scopes, skipOriginalFormat: false);
        return context;
    }

    private static void AddNonNull(Dictionary<string, object> context, IReadOnlyList<KeyValuePair<string, object>> source, bool skipOriginalFormat)
    {
        if (source == null)
        {
            return;
        }

        for (var i = 0; i < source.Count; i++)
        {
            var pair = source[i];
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value == null || (skipOriginalFormat && pair.Key == OriginalFormatKey))
            {
                continue;
            }

            context[pair.Key] = pair.Value;
        }
    }

    private static string MapLevel(DistroLogRecord record)
    {
        if (!string.IsNullOrWhiteSpace(record.LevelText))
        {
            return record.LevelText.ToUpperInvariant();
        }

        return record.SeverityNumber switch
        {
            >= 1 and <= 4 => "TRACE",
            >= 5 and <= 8 => "DEBUG",
            >= 9 and <= 12 => "INFO",
            >= 13 and <= 16 => "WARN",
            >= 17 and <= 20 => "ERROR",
            >= 21 and <= 24 => "FATAL",
            _ => null
        };
    }

    private static bool AreValidIds(string traceId, string spanId) => IsHexId(traceId, 32) && IsHexId(spanId, 16);

    // Exactly `length` hex chars, not all zeros (the W3C invalid id).
    private static bool IsHexId(string id, int length)
    {
        if (id == null || id.Length != length)
        {
            return false;
        }

        var nonZero = false;
        foreach (var c in id)
        {
            if (!Uri.IsHexDigit(c))
            {
                return false;
            }

            nonZero |= c != '0';
        }

        return nonZero;
    }
}
