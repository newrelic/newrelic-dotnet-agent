// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.Config;

/// <summary>
/// Manages a readonly only value of type T, which is tagged with its provenance.
/// Typically the provenance will be the name of a configuration file wherein the value was defined.
/// </summary>
/// <typeparam name="T">The type of the value being monitored.</typeparam>
public class ValueWithProvenance<T>
{
    public T Value { get; private set; }
    public string Provenance { get; private set; }

    public ValueWithProvenance(T Value, string Provenance)
    {
        this.Value = Value;
        this.Provenance = Provenance;
    }
};
