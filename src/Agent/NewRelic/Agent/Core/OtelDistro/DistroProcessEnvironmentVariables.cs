// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary><see cref="IDistroEnvironmentVariables"/> backed by the current process environment.</summary>
public sealed class DistroProcessEnvironmentVariables : IDistroEnvironmentVariables
{
    public string Get(string name)
    {
        return System.Environment.GetEnvironmentVariable(name);
    }

    public void Set(string name, string value)
    {
        System.Environment.SetEnvironmentVariable(name, value);
    }
}
