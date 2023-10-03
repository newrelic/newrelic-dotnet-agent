// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.Time
{
    /// <summary>
    /// A timer.  Used for timing stuff.  A timer is acquired from the TimingService.
    /// </summary>
    /// <remarks>
    /// ITimer is an IDisposable, not because it has any unmanaged resources to dispose of,
    /// but because it is much easier to use derived class objects in a using block than it 
    /// is to create an exception-handling block around it and assure the timer is stopped.
    /// </remarks>
    public interface ISimpleTimer : IDisposable
    {
        /// <summary>
        /// Stop the timer.
        /// </summary>
        void Stop();

        /// <summary>
        /// Returns the duration of the timer.  If the timer is still running, the duration up to now is returned.
        /// </summary>
        TimeSpan Duration { get; }

        /// <summary>
        /// Returns true if the timer is running - that is, the Stop() method has not been called.
        /// </summary>
        bool IsRunning { get; }
    }
}
