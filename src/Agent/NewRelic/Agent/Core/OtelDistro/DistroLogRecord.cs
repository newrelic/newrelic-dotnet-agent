// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>
/// A source-neutral log record handed to <see cref="DistroLogSink"/> by the distro plugin. Plain data, no logic:
/// the sink owns the mapping onto Core's log path.
/// </summary>
public sealed class DistroLogRecord
{
    /// <summary>When the record was created, in UTC.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>The source's level name (e.g. "Information"), if it has one.</summary>
    public string LevelText { get; set; }

    /// <summary>The source's numeric severity (OpenTelemetry ranges 1-24), if it has one.</summary>
    public int? SeverityNumber { get; set; }

    /// <summary>The message with its template placeholders filled in.</summary>
    public string FormattedMessage { get; set; }

    /// <summary>The raw message body (e.g. the template), used when there is no formatted message.</summary>
    public string Body { get; set; }

    public Exception Exception { get; set; }

    /// <summary>Lower-case hex trace id of the Activity the record was logged in, if any.</summary>
    public string TraceId { get; set; }

    /// <summary>Lower-case hex span id of the Activity the record was logged in, if any.</summary>
    public string SpanId { get; set; }

    public string Category { get; set; }

    public IReadOnlyList<KeyValuePair<string, object>> Attributes { get; set; }

    /// <summary>Scope values, already flattened, outermost first.</summary>
    public IReadOnlyList<KeyValuePair<string, object>> Scopes { get; set; }
}
