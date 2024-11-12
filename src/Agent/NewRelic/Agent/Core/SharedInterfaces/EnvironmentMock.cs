// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.SharedInterfaces
{
    public class EnvironmentMock : IEnvironment
    {
        private static string[] _emptyCommandLineArgs = new string[0];
        private static Dictionary<string, string> _emptyEnvVarDictionary = new Dictionary<string, string>();

        public string[] GetCommandLineArgs()
        {
            return _emptyCommandLineArgs;
        }

        public string GetEnvironmentVariable(string variable)
        {
            return null;
        }

        public string GetEnvironmentVariable(string variable, EnvironmentVariableTarget environmentVariableTarget)
        {
            return null;
        }

        public Dictionary<string, string> GetEnvironmentVariablesWithPrefix(string prefix)
        {
            return _emptyEnvVarDictionary;
        }

        public string GetEnvironmentVariableFromList(params string[] variables)
        {
            return null;
        }
    }
}
