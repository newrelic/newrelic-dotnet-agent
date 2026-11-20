// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Linq;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>A thread-safe <see cref="ILogger"/> that keeps every formatted message, tagged with its level.</summary>
public sealed class DistroRecordingLogger : ILogger
{
    private readonly ConcurrentQueue<(Level Level, string Message)> _entries = new ConcurrentQueue<(Level, string)>();

    public string[] Messages(Level level) => _entries.Where(e => e.Level == level).Select(e => e.Message).ToArray();

    public bool IsDebugEnabled => true;
    public bool IsErrorEnabled => true;
    public bool IsFinestEnabled => true;
    public bool IsInfoEnabled => true;
    public bool IsWarnEnabled => true;

    public void Debug(Exception exception, string message, params object[] args) => Record(Level.Debug, message, args);
    public void Debug(string message, params object[] args) => Record(Level.Debug, message, args);
    public void Error(Exception exception, string message, params object[] args) => Record(Level.Error, message, args);
    public void Error(string message, params object[] args) => Record(Level.Error, message, args);
    public void Finest(Exception exception, string message, params object[] args) => Record(Level.Finest, message, args);
    public void Finest(string message, params object[] args) => Record(Level.Finest, message, args);
    public void Info(Exception exception, string message, params object[] args) => Record(Level.Info, message, args);
    public void Info(string message, params object[] args) => Record(Level.Info, message, args);
    public void Warn(Exception exception, string message, params object[] args) => Record(Level.Warn, message, args);
    public void Warn(string message, params object[] args) => Record(Level.Warn, message, args);

    public bool IsEnabledFor(Level level) => true;
    public void Log(Level level, string message) => Record(level, message, null);
    public void Log(Level level, Exception ex, string message) => Record(level, message, null);

    private void Record(Level level, string message, object[] args)
    {
        _entries.Enqueue((level, args == null || args.Length == 0 ? message : string.Format(message, args)));
    }
}
