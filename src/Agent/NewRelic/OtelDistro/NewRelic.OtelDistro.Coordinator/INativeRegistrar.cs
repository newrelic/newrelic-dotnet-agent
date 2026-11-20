// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.OtelDistro.Coordinator;

/// <summary>Hands definitions to OTel native. Seam so the coordinator logic is testable without the native library.</summary>
public interface INativeRegistrar
{
    /// <summary>Registers the definitions under <paramref name="id"/> and returns how many native took.</summary>
    int Register(string id, CoordinatorDefinition[] definitions, uint enabledCategories);
}
