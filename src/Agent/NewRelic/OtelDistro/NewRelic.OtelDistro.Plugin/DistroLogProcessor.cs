// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Threading;
using NewRelic.Agent.Core.OtelDistro;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace NewRelic.OtelDistro.Plugin;

/// <summary>
/// Copies each finished OTel <c>LogRecord</c> into a <see cref="DistroLogRecord"/> and hands it to Core.
/// Runs on the application's thread: it never throws, and it never keeps the (pooled) <c>LogRecord</c>.
/// </summary>
public sealed class DistroLogProcessor : BaseProcessor<LogRecord>
{
    private const string LogPrefix = "[NewRelic.OtelDistro.Plugin]";

    private static int _loggedError;

    private readonly DistroLogSink _sink;

    public DistroLogProcessor(DistroLogSink sink)
    {
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
    }

    public override void OnEnd(LogRecord data)
    {
        try
        {
            // Cheap exit first: nothing is allocated or copied when forwarding and log metrics are off or the agent is shut down.
            if (!_sink.Enabled || data == null)
            {
                return;
            }

            // LogRecord.Severity and SeverityText have no public getter in OpenTelemetry 1.20.1 (they live on the internal
            // LogRecordData), so the level comes from LogLevel, the same value the ILogger bridge derives both from.
#pragma warning disable CS0618 // LogRecord.LogLevel is obsolete, but it is the only public level
            var level = (int)data.LogLevel;
#pragma warning restore CS0618

            var record = new DistroLogRecord
            {
                Timestamp = data.Timestamp,
                LevelText = LevelText(level),
                SeverityNumber = SeverityNumber(level),
                FormattedMessage = data.FormattedMessage,
                Body = data.Body,
                Exception = data.Exception,
                Category = data.CategoryName,
                Attributes = CopyAttributes(data.Attributes),
                Scopes = CopyScopes(data)
            };

            if (data.TraceId != default)
            {
                record.TraceId = data.TraceId.ToHexString();
                record.SpanId = data.SpanId.ToHexString();
            }

            _sink.Record(record);
        }
        catch (Exception ex)
        {
            if (Interlocked.Exchange(ref _loggedError, 1) == 0)
            {
                Console.Error.WriteLine($"{LogPrefix} Copying a log record failed; later failures will not be logged: {ex}");
            }
        }
    }

    // Microsoft.Extensions.Logging.LogLevel names, as OpenTelemetry's ILogger bridge writes them to SeverityText.
    private static string LevelText(int level) => level switch
    {
        0 => "Trace",
        1 => "Debug",
        2 => "Information",
        3 => "Warning",
        4 => "Error",
        5 => "Critical",
        _ => null
    };

    // OpenTelemetry SeverityNumber for each Microsoft.Extensions.Logging.LogLevel, as the ILogger bridge maps them.
    private static int? SeverityNumber(int level) => level switch
    {
        0 => 1,
        1 => 5,
        2 => 9,
        3 => 13,
        4 => 17,
        5 => 21,
        _ => null
    };

    // The record's attribute list is pooled and reused after OnEnd, so it is copied.
    private static IReadOnlyList<KeyValuePair<string, object>> CopyAttributes(IReadOnlyList<KeyValuePair<string, object>> attributes)
    {
        if (attributes == null || attributes.Count == 0)
        {
            return null;
        }

        var copy = new List<KeyValuePair<string, object>>(attributes.Count);
        for (var i = 0; i < attributes.Count; i++)
        {
            copy.Add(attributes[i]);
        }

        return copy;
    }

    private static IReadOnlyList<KeyValuePair<string, object>> CopyScopes(LogRecord data)
    {
        var scopes = new List<KeyValuePair<string, object>>();
        data.ForEachScope(static (scope, list) =>
        {
            foreach (var item in scope)
            {
                list.Add(item);
            }
        }, scopes);

        return scopes.Count == 0 ? null : scopes;
    }
}
