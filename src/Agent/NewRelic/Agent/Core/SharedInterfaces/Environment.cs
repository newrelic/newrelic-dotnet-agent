// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace NewRelic.Agent.Core.SharedInterfaces;

/// <summary>
/// Provides methods for retrieving command-line arguments and environment variables for the current process.
/// Serves as a wrapper around System.Environment to facilitate testing and abstraction.
/// </summary>
/// <remarks>This class offers convenient access to environment-related information, such as command-line
/// arguments and environment variables, with optional caching for improved performance. It is intended for use in
/// scenarios where environment data needs to be accessed or filtered within an application. All members are instance
/// methods; thread safety is ensured for environment variable retrieval.</remarks>
public class Environment : IEnvironment
{
    private readonly ConcurrentDictionary<string, string> _environmentVariableCache = new();

    public string[] GetCommandLineArgs()
    {
        return System.Environment.GetCommandLineArgs();
    }

    public string GetEnvironmentVariable(string variable)
    {
        return _environmentVariableCache.GetOrAdd(variable, _ => System.Environment.GetEnvironmentVariable(variable));
    }

    public string GetEnvironmentVariableFromList(params string[] variables)
    {
        var envValue = (variables ?? Enumerable.Empty<string>())
            .Select(GetEnvironmentVariable)
            .FirstOrDefault(v => v != null);

        return envValue == string.Empty ? null : envValue;
    }


    public Dictionary<string, string> GetEnvironmentVariablesWithPrefix(string prefix)
    {
        if (prefix == null)
            throw new ArgumentNullException(nameof(prefix));

        // Enumerate once; Environment variables rarely change intra-process.
        var envVars = System.Environment.GetEnvironmentVariables();
        Dictionary<string, string> result = new();

        foreach (DictionaryEntry entry in envVars)
        {
            // Keys for environment variables are strings; cast avoids virtual ToString() call.
            if (entry.Key is not string name || !name.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            var value = entry.Value as string;
            result[name] = value;
            _environmentVariableCache.TryAdd(name, value);
        }

        return result;
    }
}
