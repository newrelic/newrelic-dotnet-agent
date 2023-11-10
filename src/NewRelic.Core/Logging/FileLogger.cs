// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace NewRelic.Core.Logging
{
    public class FileLogger : ILogger
    {
        private static ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
        private static string _path;

        public FileLogger(string path)
        {
            _path = path.Replace(".log", $".{Process.GetCurrentProcess().Id}.log");
        }

        public bool IsDebugEnabled => true;

        public bool IsErrorEnabled => true;

        public bool IsFinestEnabled => true;

        public bool IsInfoEnabled => true;

        public bool IsWarnEnabled => true;

        private static void LogWrite(string message)
        {
            _lock.EnterWriteLock();
            File.AppendAllText(_path, message);
            _lock.ExitWriteLock();
        }

        private static void FormatLog(string level, string template, params object[] args)
        {
            if (string.IsNullOrEmpty(template))
                return;
            string message;
            try
            {
                message = (args == null) || (args.Length == 0) ? template : string.Format(template, args);
            }
            catch // oops, we must be using Serilog-style logging. just dump the args
            {
                message = $"{template} /// {string.Join(",", args)}";
            }
            var pid = Process.GetCurrentProcess().Id;
            var tid = Thread.CurrentThread.ManagedThreadId;
            // 2023-11-09 20:04:50,248 NewRelic   INFO: [pid: 1, tid: 1] Application name from NEW_RELIC_APP_NAME Environment Variable.
            string full = $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")} NewRelic {level.PadLeft(5)}: [pid: {pid} tid: {tid}] {message}\r\n";
            LogWrite(full);
        }

        private static void LogException(Exception e)
        {
            if (e == null) return;
            LogWrite(e?.Message);
            LogWrite(e?.StackTrace);
            LogWrite("\r\n");
        }

        public void Debug(Exception exception, string message, params object[] args)
        {
            Debug(message, args);
            LogException(exception);
        }

        public void Debug(string message, params object[] args)
        {
            FormatLog("DEBUG", message, args);
        }

        public void Error(Exception exception, string message, params object[] args)
        {
            Error(message, args);
            LogException(exception);
        }

        public void Error(string message, params object[] args)
        {
            FormatLog("ERROR", message, args, args);
        }

        public void Finest(Exception exception, string message, params object[] args)
        {
            Finest(message, args);
            LogException(exception);
        }

        public void Finest(string message, params object[] args)
        {
            FormatLog("FINEST", message, args);
        }

        public void Info(Exception exception, string message, params object[] args)
        {
            Info(message, args);
            LogException(exception);
        }

        public void Info(string message, params object[] args)
        {
            FormatLog("INFO", message, args);
        }

        public void Warn(Exception exception, string message, params object[] args)
        {
            Warn(message, args);
            LogException(exception);
        }

        public void Warn(string message, params object[] args)
        {
            FormatLog("WARN", message, args);
        }
    }
}
