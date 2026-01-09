// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.OpenTelemetryBridge
{
    /// <summary>
    /// Coordinates the bridging of OpenTelemetry meters and instruments to the New Relic agent.
    /// </summary>
    public interface IMeterBridgingService : IDisposable
    {
        /// <summary>
        /// Starts listening to the specified meter.
        /// </summary>
        void StartListening(object meter);

        /// <summary>
        /// Stops listening to all meters and disposes the internal listener.
        /// </summary>
        void StopListening();

        /// <summary>
        /// Called when an instrument is published to determine if it should be bridged.
        /// </summary>
        void OnInstrumentPublished(object instrument, IMeterListenerWrapper listener);
    }
}
