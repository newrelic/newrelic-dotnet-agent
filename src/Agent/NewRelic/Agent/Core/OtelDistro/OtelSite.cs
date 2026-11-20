// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>
/// One OTel CallTarget site (an <c>InstrumentMethodAttribute</c> of an enabled OTel instrumentation), as read by the
/// plugin's reflection adapter and judged by <see cref="DistroOtelPrecedenceDiagnostics"/>.
/// </summary>
public sealed class OtelSite
{
    /// <summary>The OTel instrumentation ID, upper case as in its env var (for example ADONET).</summary>
    public string Id { get; set; }

    /// <summary>The attribute's integration name (for example AdoNet).</summary>
    public string Integration { get; set; }

    /// <summary>Simple name of the target assembly.</summary>
    public string Assembly { get; set; }

    /// <summary>Full name of the target type (for a Derived/Interface site: the base type or interface).</summary>
    public string Type { get; set; }

    public string Method { get; set; }

    /// <summary>Parameter type names, without the return type. Null is treated as parameterless.</summary>
    public IReadOnlyList<string> Signature { get; set; }

    /// <summary>True for OTel's Derived and Interface kinds: OTel rewrites the method in every derived or implementing type.</summary>
    public bool MatchesDerivedOrInterface { get; set; }
}
