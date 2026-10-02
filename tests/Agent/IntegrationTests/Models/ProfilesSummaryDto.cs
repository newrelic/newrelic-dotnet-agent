// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace NewRelic.IntegrationTests.Models;

/// <summary>
/// Summary of a single OTLP <c>ExportProfilesServiceRequest</c> the mock collector received and parsed,
/// used by continuous-profiling integration tests to assert against an independently-decoded payload
/// rather than only the agent's own debug-log dump.
///
/// Beyond the structural counts, this captures decoded <b>content</b>: the deepest sample's frame count,
/// whether any sample carries a real (non-placeholder) resolved frame name, and the resource attributes
/// that identify the entity. Counts alone let a wrong payload pass green -- N samples with empty stacks
/// satisfy <c>SampleCount &gt; 0</c>, a string table full of placeholders satisfies <c>StringTableSize &gt; 1</c>,
/// and a profile attached to the wrong entity (or with resource attributes dropped) satisfies every count.
/// </summary>
public class ProfilesSummaryDto
{
    public DateTime ReceivedAtUtc { get; set; }
    public int ResourceProfileCount { get; set; }
    public int ScopeProfileCount { get; set; }
    public int ProfileCount { get; set; }
    public int SampleCount { get; set; }
    public int StringTableSize { get; set; }

    // Content: the maximum number of frames (stack location indices) any single sample carries. Guards the
    // "N samples with empty stacks" case that SampleCount > 0 alone cannot catch.
    public int MaxFramesInAnySample { get; set; }

    // Content: number of samples whose stack has at least one frame.
    public int SamplesWithFramesCount { get; set; }

    // Content: true when at least one sample references a resolved, real frame name -- a "Type.Method"-shaped
    // string that is neither empty, the synthetic native-entry marker, nor an "UnknownClass.UnknownMethod(...)"
    // placeholder. Guards a string table populated only with placeholders/reserved entries.
    public bool HasRealFrameName { get; set; }

    // Content: an example real (non-placeholder) frame name, for diagnostics on assertion failure.
    public string ExampleRealFrameName { get; set; }

    // Resource identity: the attribute keys present on the first resource, plus the specific
    // identity-bearing attributes broken out. Guards wrong-entity attachment / an ingest-side resource drop.
    public List<string> ResourceAttributeKeys { get; set; } = new List<string>();
    public bool HasServiceName { get; set; }
    public string ServiceName { get; set; }
    public bool HasEntityGuid { get; set; }
    public string EntityGuid { get; set; }
    public bool HasHost { get; set; }

    // True when the request parsed and carries the minimum structure a real profile has:
    // at least one ResourceProfiles/ScopeProfiles/Profile and a string table with more than
    // the reserved index-0 empty entry.
    public bool StructurallyValid { get; set; }

    // Stronger than StructurallyValid: the profile also carries real decoded content -- at least one sample
    // with a non-empty stack, at least one resolved (non-placeholder) frame name, and a service.name resource
    // attribute identifying the entity. This is what pins "captured, encoded, shipped, and non-empty/correct".
    public bool ContentValid { get; set; }
}
