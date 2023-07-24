// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading;

namespace NewRelic.Agent.Core.Utilities
{
    public class SignalableAction : IDisposable
    {
        private readonly Thread _worker;
        private readonly object _lock = new object();
        private bool _signaled;

        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public SignalableAction(Action action, int delay)
        {
            void Action()
            {
                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    lock (_lock)
                    {
                        while (!_signaled) Monitor.Wait(_lock);
                    }

                    Thread.Sleep(delay);
                    lock (_lock) _signaled = false;
                    action();
                }
            }

            _worker = new Thread(Action);
            _worker.IsBackground = true;
        }

        public void Start()
        {
            _worker.Start();
        }

        public void Signal()
        {
            lock (_lock)
            {
                _signaled = true;
                Monitor.Pulse(_lock);
            }
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
        }
    }
}
