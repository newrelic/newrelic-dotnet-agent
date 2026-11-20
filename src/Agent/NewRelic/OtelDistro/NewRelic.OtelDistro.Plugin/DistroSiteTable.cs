// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Core.OtelDistro;

namespace NewRelic.OtelDistro.Plugin;

/// <summary>
/// The generated definitions table, read once per AppDomain: the definitions <see cref="Plugin"/> registers with OTel,
/// the sites the generated integration classes find by their <c>SiteIndex</c> through <see cref="DistroCallTarget"/>,
/// and the manifest of shipped XML files the table was built from.
/// The type initializer does the read, so whichever comes first in an AppDomain (start-up, or an instrumented call in an
/// AppDomain where start-up never ran) reads the table, and every later use shares that result. A rejected table leaves
/// <see cref="Sites"/> and <see cref="Manifest"/> null, so every instrumented call runs without a tracer. TryRead never
/// throws (a failure to load Core itself would still surface as a TypeInitializationException, as the old per-class
/// static constructors would have).
/// </summary>
internal static class DistroSiteTable
{
    internal static readonly bool Loaded;
    internal static readonly IReadOnlyList<DistroCallTargetDefinition> Definitions;
    internal static readonly DistroInstrumentationSites Sites;
    internal static readonly DistroShippedXmlManifest Manifest;
    internal static readonly string FailureReason;

    // Explicit, so the type is not beforefieldinit: the read runs on first access (inside Plugin's timed window), never
    // earlier.
    static DistroSiteTable()
    {
        Loaded = InstrumentationDefinitionsTable.TryRead(Generated.GeneratedDistroDefinitions.Table, out Definitions, out Sites, out Manifest, out FailureReason);
    }

    /// <summary>
    /// The per-site gate checked first on every instrumented call: whether the site's tracer should run (on by default
    /// unless the shipped XML disables its point; the XML on disk can turn it off and back on at run time). False for a
    /// rejected table or an index out of range. One volatile read; never throws.
    /// </summary>
    internal static bool IsOn(int siteIndex) => Sites != null && Sites.IsOn(siteIndex);

    /// <summary>
    /// Whether the site is an NR log-forwarding wrapper, so the call target brackets it with the OTel/NR log dedupe
    /// counter. False for a rejected table or an index out of range. One array read; never throws.
    /// </summary>
    internal static bool IsLogForwarding(int siteIndex) => Sites != null && Sites.IsLogForwarding(siteIndex);
}
