// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>What <see cref="DistroOtelEnvironmentDefaults.Apply"/> did to the OTel environment.</summary>
public sealed class DistroOtelDefaultsResult
{
    private static readonly string[] NoStrings = Array.Empty<string>();
    private static readonly KeyValuePair<string, string>[] NoPairs = Array.Empty<KeyValuePair<string, string>>();

    public DistroOtelDefaultsResult(
        IReadOnlyList<string> appliedDisables,
        IReadOnlyList<KeyValuePair<string, string>> customerValuesKept,
        IReadOnlyList<KeyValuePair<string, string>> exportersOverridden,
        string nonDefaultPropagators,
        string failureMessage,
        bool fileBasedConfigActive)
    {
        // Null-coalesced so FormatSummary (documented "never throws") cannot NRE on a result built with null lists.
        AppliedDisables = appliedDisables ?? NoStrings;
        CustomerValuesKept = customerValuesKept ?? NoPairs;
        ExportersOverridden = exportersOverridden ?? NoPairs;
        NonDefaultPropagators = nonDefaultPropagators;
        FailureMessage = failureMessage;
        FileBasedConfigActive = fileBasedConfigActive;
    }

    /// <summary>OTel trace instrumentation IDs New Relic turned off (the customer had not set them).</summary>
    public IReadOnlyList<string> AppliedDisables { get; }

    /// <summary>OTel trace instrumentation IDs the customer had set (ID, customer value); left as set.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> CustomerValuesKept { get; }

    /// <summary>Signals whose exporter was not <c>none</c> before New Relic set it to <c>none</c> (signal, prior value).</summary>
    public IReadOnlyList<KeyValuePair<string, string>> ExportersOverridden { get; }

    /// <summary>The customer's <c>OTEL_PROPAGATORS</c> when it is not the default; null when unset or default.</summary>
    public string NonDefaultPropagators { get; }

    /// <summary>The first failure's message (the remaining steps were still attempted); null when every step succeeded.</summary>
    public string FailureMessage { get; }

    /// <summary>
    /// True when OTel's file-based (YAML) configuration is active, so OTel ignores the environment defaults that were written.
    /// </summary>
    public bool FileBasedConfigActive { get; }
}
