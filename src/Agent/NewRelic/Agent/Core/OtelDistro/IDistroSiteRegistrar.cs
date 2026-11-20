// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>
/// Registers NR instrumentation sites (indices into the distro's compiled site table) with OTel's native call-target
/// registration on behalf of every AppDomain in the process, so each site is sent once per process. Core talks to the
/// process-wide coordinator only through this seam; the OTel plugin adapts the coordinator to it.
/// </summary>
public interface IDistroSiteRegistrar
{
    /// <summary>Registers <paramref name="sites"/>, sending only those not already registered in this process. Reports what happened; should not throw.</summary>
    DistroSiteRegistration Register(IReadOnlyList<int> sites);
}
