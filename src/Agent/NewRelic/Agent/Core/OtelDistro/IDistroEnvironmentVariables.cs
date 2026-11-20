// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>
/// The process environment variables distro mode reads and writes before OTel reads its settings. A seam so the
/// defaults logic can be unit tested without touching the real process environment.
/// </summary>
public interface IDistroEnvironmentVariables
{
    /// <summary>The variable's value, or null when it is not set.</summary>
    string Get(string name);

    /// <summary>Sets the variable for the current process.</summary>
    void Set(string name, string value);
}
