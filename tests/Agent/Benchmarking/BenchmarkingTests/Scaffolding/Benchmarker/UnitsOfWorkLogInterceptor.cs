// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using BenchmarkDotNet.Loggers;
using System.Collections.Generic;
using System.Text;

namespace BenchmarkingTests.Scaffolding.Benchmarker
{

    /// <summary>
    /// Custom Logger that intercepts the output of our exerciser process
    /// so that it may be interpreted by the Diagnoser later on.
    /// Specifically, the exercier process outputs the number of
    /// times the exercised function was executed during the iteration
    /// </summary>
    public class UnitsOfWorkLogInterceptor : ILogger
    {
        private bool _shouldIntercept = false;

        public List<string> CapturedInfo { get; private set; } = new List<string>();

        public void StartReading()
        {
            _shouldIntercept = true;
        }

        public void StopReading()
        {
            _shouldIntercept = false;
        }

        private StringBuilder _sbInProcessString = new StringBuilder();

        public void Write(LogKind logKind, string text)
        {
            if (_shouldIntercept)
            {
                _sbInProcessString.Append(text);
            }
        }

        public void WriteLine()
        {
            if (_shouldIntercept && _sbInProcessString.Length > 0)
            {
                CapturedInfo.Add(_sbInProcessString.ToString());
                _sbInProcessString.Clear();
            }
        }

        public void WriteLine(LogKind logKind, string text)
        {
            if (_shouldIntercept)
            {
                _sbInProcessString.Append(text);
                WriteLine();
            }
        }

        public void Flush()
        {
            WriteLine();
        }
    }
}
