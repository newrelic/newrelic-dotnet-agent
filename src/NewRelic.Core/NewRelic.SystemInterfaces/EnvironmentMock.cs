using System;
using System.Collections.Generic;

namespace NewRelic.SystemInterfaces
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
    }
}
