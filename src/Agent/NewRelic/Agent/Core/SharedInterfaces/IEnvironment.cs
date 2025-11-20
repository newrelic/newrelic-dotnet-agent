// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Security;

namespace NewRelic.Agent.Core.SharedInterfaces
{
    public interface IEnvironment
    {
        /// <summary>
        /// Returns a string array containing the command-line arguments for the current process.
        /// </summary>
        /// <returns>An array of string where each element contains a command-line argument. The first element is the executable file name, and the following zero or more elements contain the remaining command-line arguments.</returns>
        string[] GetCommandLineArgs();

        /// <summary>
        /// Retrieves the value of an environment variable from the current process.
        /// </summary>
        /// <param name="variable">The name of the environment variable.</param>
        /// <returns>The value of the environment variable specified by variable, or null if the environment variable is not found.</returns>
        /// <exception cref="ArgumentNullException">variable is null.</exception>
        /// <exception cref="SecurityException">The caller does not have the required permission to perform this operation.</exception>
        string GetEnvironmentVariable(string variable);

        /// <summary>
        /// Retrieves the value of an environment variable with specified prefix from the current process located in the specified target.
        /// </summary>
        /// <param name="prefix">prefix.</param>
        /// <returns>Return a dictionary of environment variables that have the prefix matches with the specified prefix, or null if the environment variable is not found.</returns>
        /// <exception cref="ArgumentNullException">prefix is null.</exception>
        /// <exception cref="SecurityException">The caller does not have the required permission to perform this operation.</exception>
        Dictionary<string, string> GetEnvironmentVariablesWithPrefix(string prefix);

        /// <summary>
        /// Retrieves the value of an environment variable, searching the provided list of variables in order. The first variable found is returned.
        /// </summary>
        /// <param name="variables">A list of one or more environment variable names to search, in order of preference</param>
        /// <returns>The value of the first matching environment variable, if found, otherwise <c>null</c></returns>
        string GetEnvironmentVariableFromList(params string[] variables);
    }
}
