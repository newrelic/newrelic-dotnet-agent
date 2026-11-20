// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>One NR definitions-table site, as judged by <see cref="DistroOtelPrecedenceDiagnostics"/>.</summary>
public sealed class NrSite
{
    public string TracerFactory { get; set; }

    /// <summary>Simple name of the target assembly.</summary>
    public string Assembly { get; set; }

    /// <summary>Full name of the target type.</summary>
    public string Type { get; set; }

    public string Method { get; set; }

    /// <summary>Parameter type names; empty for a parameterless method, null when the site matches every overload.</summary>
    public IReadOnlyList<string> Signature { get; set; }

    /// <summary>The NR site of a read definitions table, its XML parameter string split into parameter type names.</summary>
    public static NrSite From(DistroInstrumentationSite site)
    {
        return new NrSite
        {
            TracerFactory = site.TracerFactoryName,
            Assembly = site.AssemblyName,
            Type = site.TypeName,
            Method = site.MethodName,
            Signature = DistroOtelPrecedenceDiagnostics.ParseSignature(site.ArgumentSignature),
        };
    }
}
