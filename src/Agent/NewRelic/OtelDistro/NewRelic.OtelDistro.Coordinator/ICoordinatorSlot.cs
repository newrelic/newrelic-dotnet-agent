// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.OtelDistro.Coordinator;

/// <summary>A single named storage cell that holds the process's coordinator. Seam so find-or-create is testable without AppDomains.</summary>
public interface ICoordinatorSlot
{
    /// <summary>Returns the stored object, or null when the slot is empty.</summary>
    object Get();

    /// <summary>Stores the object in the slot.</summary>
    void Set(object value);
}
