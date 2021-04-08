// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace NewRelic.Agent.ConsoleScanner
{
    public class ScannerConfiguration
    {
        public List<InstrumentationSet> InstrumentationSets { get; set; }

        public static ScannerConfiguration GetScannerConfiguration(string filePath)
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(HyphenatedNamingConvention.Instance)
                .Build();

            return  deserializer.Deserialize<ScannerConfiguration>(File.ReadAllText(filePath)).SubstituteEnvironmentVariables();
        }

        public ScannerConfiguration SubstituteEnvironmentVariables()
        {
            foreach (var instrumentationSet in InstrumentationSets)
            {
                ProcessInstrumentationSet(instrumentationSet);
            }

            return this;
        }

        public void ProcessInstrumentationSet(InstrumentationSet instrumentationSet)
        {
            instrumentationSet.Name = GetSubstitutedValue(instrumentationSet.Name);
            instrumentationSet.XmlFile = GetSubstitutedValue(instrumentationSet.XmlFile);

            if (instrumentationSet.NugetPackages != null)
            {
                foreach (var nugetSet in instrumentationSet.NugetPackages)
                {
                    nugetSet.PackageName = GetSubstitutedValue(nugetSet.PackageName);
                    // not processing versions at this time since its not likely to be replace with an env var
                }
            }

            if (instrumentationSet.LocalAssemblies != null)
            {
                for (var i = 0; i < instrumentationSet.LocalAssemblies.Count; i++)
                {
                    instrumentationSet.LocalAssemblies[i] = GetSubstitutedValue(instrumentationSet.LocalAssemblies[i]);
                }
            }
        }

        public string GetSubstitutedValue(string initialValue)
        {
            // indexes start at first char in token
            if (string.IsNullOrWhiteSpace(initialValue))
            {
                throw new InvalidDataException("Value is empty.");
            }

            var tokenStart = initialValue.IndexOf("${{");
            if (tokenStart == -1)
            {
                return initialValue;
            }

            var initialEnd = initialValue.IndexOf("}}");
            if (initialEnd == -1)
            {
                throw new FormatException("Variable in string missing closing token.");
            }

            if (initialEnd < tokenStart)
            {
                throw new FormatException("Variable in string has ending token before starting token.");
            }

            var tokenEnd = initialEnd + 2;
            var token = initialValue.Substring(tokenStart, tokenEnd - tokenStart);
            var envVarName = token.Trim('$', '{', '}').Trim(); // removes the wrapper chars and then the whitespace, it any
            var envVarValue = Environment.GetEnvironmentVariable(envVarName);
            if (string.IsNullOrWhiteSpace(envVarValue))
            {
                throw new InvalidDataException($"Environment variable '{envVarName}' does not exist or has no value.");
            }

            return GetSubstitutedValue(initialValue.Replace(token, envVarValue));
        }
    }
}
