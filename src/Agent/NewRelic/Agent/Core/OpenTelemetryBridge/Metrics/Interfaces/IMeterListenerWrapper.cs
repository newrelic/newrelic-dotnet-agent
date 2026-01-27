// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.OpenTelemetryBridge.Metrics.Interfaces;

/// <summary>
/// Delegate for measurement callbacks.
/// </summary>
public delegate void MeasurementCallbackDelegate<T>(object instrument, T measurement, ReadOnlySpan<KeyValuePair<string, object>> tags, object state) where T : struct;

/// <summary>
/// Provides a version-agnostic wrapper for the System.Diagnostics.Metrics.MeterListener.
/// </summary>
public interface IMeterListenerWrapper : IDisposable
{
    Action<object, IMeterListenerWrapper> InstrumentPublished { get; set; }

    /// <summary>
    /// Gets or sets the callback called when measurements for an instrument are completed.
    /// Parameters are: (object instrument, object state, IMeterListenerWrapper listener)
    /// </summary>
    Action<object, object, IMeterListenerWrapper> MeasurementsCompleted { get; set; }

    /// <summary>
    /// Starts the listener.
    /// </summary>
    void Start();

    /// <summary>
    /// Enables measurement events for the specified instrument.
    /// </summary>
    void EnableMeasurementEvents(object instrument, object state);

    /// <summary>
    /// Records measurements for all observable instruments.
    /// </summary>
    void RecordObservableInstruments();

    /// <summary>
    /// Sets the measurement callback for the specified numeric type.
    /// </summary>
    void SetMeasurementCallback<T>(MeasurementCallbackDelegate<T> callback) where T : struct;
}