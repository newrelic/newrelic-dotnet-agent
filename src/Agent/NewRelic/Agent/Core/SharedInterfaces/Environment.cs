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

    public string[] GetCommandLineArgs() => System.Environment.GetCommandLineArgs();

    /// <summary>
    /// Retrieves the value of the specified environment variable from the current process, caching the result for future calls.
    /// </summary>
    /// <param name="variable">The name of the environment variable to retrieve. Cannot be null.</param>
    /// <returns>The value of the environment variable specified by <paramref name="variable"/>; or null if the environment
    /// variable is not found.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="variable"/> is null.</exception>
    public string GetEnvironmentVariable(string variable)
    {
        if (variable == null)
            throw new ArgumentNullException(nameof(variable));
        return _environmentVariableCache.GetOrAdd(variable, _ => System.Environment.GetEnvironmentVariable(variable));
    }

    /// <summary>
    /// Retrieves the value of the first environment variable from the specified list that is set and not empty.
    /// </summary>
    /// <remarks>The method checks each environment variable name in the order provided and returns the value
    /// of the first one that exists and is not an empty string. If all specified variables are unset or empty, the
    /// method returns null.</remarks>
    /// <param name="variables">An array of environment variable names to search, in order of preference. Cannot be null or empty.</param>
    /// <returns>The value of the first environment variable in the list that is set and not empty; otherwise, null if none are
    /// found.</returns>
    /// <exception cref="ArgumentNullException">Thrown if variables is null or empty.</exception>
    public string GetEnvironmentVariableFromList(params string[] variables)
    {
        if (variables == null || variables.Length == 0)
            throw new ArgumentNullException(nameof(variables));

        var envValue = (variables ?? Enumerable.Empty<string>())
            .Select(GetEnvironmentVariable)
            .FirstOrDefault(v => v != null);

        return envValue == string.Empty ? null : envValue;
    }

    /// <summary>
    /// Retrieves all environment variables whose names begin with the specified prefix.
    /// </summary>
    /// <remarks>The returned dictionary includes only environment variables whose names start with the given
    /// prefix, using an ordinal, case-sensitive comparison. The method reflects the current process environment at the
    /// time of the call; changes to environment variables after the call are not reflected in the returned
    /// dictionary.</remarks>
    /// <param name="prefix">The string prefix to match at the start of environment variable names. Comparison is case-sensitive and uses
    /// ordinal comparison.</param>
    /// <returns>A dictionary containing the names and values of all environment variables that start with the specified prefix.
    /// If no variables match, the dictionary is empty.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="prefix"/> is <see langword="null"/>.</exception>
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
