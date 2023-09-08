// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;

namespace NewRelic.Agent.Core.Time
{
    class SimpleTimer : ISimpleTimer
    {
        private readonly Stopwatch _timer = Stopwatch.StartNew();

        public void Stop()
        {
            _timer.Stop();
        }

        public TimeSpan Duration => _timer.Elapsed;

        public bool IsRunning => _timer.IsRunning;

        void IDisposable.Dispose()
        {
            Stop();
        }
    }
}
