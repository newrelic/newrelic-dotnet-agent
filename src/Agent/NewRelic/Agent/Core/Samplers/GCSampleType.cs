// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.Samplers;

public enum GCSampleType
{
    /// <summary>
    /// Gen 0 heap size as of the current sample
    /// </summary>
    Gen0Size,
    Gen0Promoted,
    /// <summary>
    /// Gen 1 heap size as of the current sample
    /// </summary>
    Gen1Size,
    Gen1Promoted,
    /// <summary>
    /// Gen 2 heap size as of the current sample
    /// </summary>
    Gen2Size,
    Gen2Survived,
    /// <summary>
    /// Large object heap size as of the current sample
    /// </summary>
    LOHSize,
    LOHSurvived,
    HandlesCount,
    InducedCount,
    PercentTimeInGc,
    /// <summary>
    /// Gen 0 heap collection count since the last sample
    /// </summary>
    Gen0CollectionCount,
    /// <summary>
    /// Gen 1 heap collection count since the last sample
    /// </summary>
    Gen1CollectionCount,
    /// <summary>
    /// Gen 2 heap collection count since the last sample
    /// </summary>
    Gen2CollectionCount,

    // the following are supported by GCSamplerV2 only
    /// <summary>
    /// Pinned object heap size
    /// </summary>
    POHSize,
    /// <summary>
    /// Large object heap collection count since the last sample
    /// </summary>
    LOHCollectionCount,
    /// <summary>
    /// Pinned object heap collection count since the last sample
    /// </summary>
    POHCollectionCount,
    /// <summary>
    /// Total heap memory in use as of the current sample
    /// </summary>
    TotalHeapMemory,
    /// <summary>
    /// Total committed memory in use as of the current sample
    /// </summary>
    TotalCommittedMemory,
    /// <summary>
    /// Total heap memory allocated since the last sample
    /// </summary>
    TotalAllocatedMemory,
    /// <summary>
    /// Fragmentation of the Gen 0 heap as of the current sample
    /// </summary>
    Gen0FragmentationSize,
    /// <summary>
    /// Fragmentation of the Gen 1 heap as of the current sample
    /// </summary>
    Gen1FragmentationSize,
    /// <summary>
    /// Fragmentation of the Gen 2 heap as of the current sample
    /// </summary>
    Gen2FragmentationSize,
    /// <summary>
    /// Fragmentation of the Large Object heap as of the current sample
    /// </summary>
    LOHFragmentationSize,
    /// <summary>
    /// Fragmentation of the Pinned Object heap as of the current sample
    /// </summary>
    POHFragmentationSize,
}