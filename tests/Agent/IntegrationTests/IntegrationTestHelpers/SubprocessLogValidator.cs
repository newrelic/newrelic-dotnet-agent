// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTestHelpers
{
    /// <summary>
    ///  A state-based log validator for subprocess console output (e.g. the output from HostedWebCore).
    ///  Suitable for validating 
    /// </summary>
    public static class SubprocessLogValidator
    {
        enum ValidatorState
        {
            HWC_FIRING_UP,
            HWC_STARTING_DIRECTORY,
            HWC_ENV,
            HWC_STARTING_SERVER,
            HWC_DONE,
            ALL_DONE
        }

        static string[] expected = new string[]
        {
                        "HostedWebCore: Firing up...",
                        "HostedWebCore: Starting directory:",
                        "HostedWebCore: Environment Variables:",
                        "HostedWebCore: Starting server...",
                        "HostedWebCore: Done.",
                        null
        };

        private static void Fail(string msg)
        {
            Assert.Fail("Hosted Web Core log failed validation: " + msg);
        }

        public static void ValidateHostedWebCoreConsoleOutput(string log, ITestOutputHelper testLogger)
        {
            StringReader reader = new StringReader(log);
            ValidatorState currentState = ValidatorState.HWC_FIRING_UP;
            string line;

            testLogger?.WriteLine("LogValidator: start");

            while ((line = reader.ReadLine()) != null)
            {
                testLogger?.WriteLine("LogValidator: in state '" + currentState + "' with line: '" + line + "'");

                int splitAt = line.IndexOf("] ");
                if (splitAt == -1 || line.Length <= splitAt + "] ".Length)
                {
                    Fail("badly formatted line: '" + line + "'");
                }

                if (!line.Substring(splitAt + "] ".Length).StartsWith(expected[(int)currentState]))
                {
                    Fail("unexpected line: " + line);
                }
                else
                {
                    currentState++;
                }
            }

            if (currentState != ValidatorState.ALL_DONE)
            {
                Fail("file ended early");
            }

            testLogger?.WriteLine("LogValidator: done.");
        }

    }
}
