// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.SharedInterfaces
{
    public class Environment : IEnvironment
    {
        public string[] GetCommandLineArgs()
        {
            return System.Environment.GetCommandLineArgs();
        }

        public string GetEnvironmentVariable(string variable)
        {
            return System.Environment.GetEnvironmentVariable(variable);
        }

        public string GetEnvironmentVariableFromList(params string[] variables)
        {
            var envValue = (variables ?? Enumerable.Empty<string>())
                .Select(v => GetEnvironmentVariableInternal(v))
                .FirstOrDefault(value => value != null);

            return envValue == string.Empty ? null : envValue;
        }

        public string GetEnvironmentVariable(string variable, EnvironmentVariableTarget environmentVariableTarget)
        {
            return System.Environment.GetEnvironmentVariable(variable, environmentVariableTarget);
        }

        private string GetEnvironmentVariableInternal(string variable, EnvironmentVariableTarget environmentVariableTarget = EnvironmentVariableTarget.Process)
        {
            var value = System.Environment.GetEnvironmentVariable(variable, environmentVariableTarget);

            // TODO: remove in v11
            if (value != null && variable.StartsWith("NEWRELIC_", StringComparison.OrdinalIgnoreCase))
            {
                var preferredVariableName = variable.ToUpper().Replace("NEWRELIC_", "NEW_RELIC_");
                Log.Warn($"The environment variable {variable} is deprecated and will be removed in version 11. Please use {preferredVariableName} instead.");
            }

            return value;
        }

        public Dictionary<string, string> GetEnvironmentVariablesWithPrefix(string prefix)
        {
            var environmentVariables = System.Environment.GetEnvironmentVariables();

            Dictionary<string, string> result = null;

            foreach (DictionaryEntry entry in environmentVariables)
            {
                var key = entry.Key.ToString();
                if (key.StartsWith(prefix))
                {
                    if (result == null)
                    {
                        result = new Dictionary<string, string>();
                    }

                    result.Add(key.ToString(), entry.Value.ToString());
                }
            }
            return result;
        }
    }
}
