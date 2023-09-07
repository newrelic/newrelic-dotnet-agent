// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.DistributedTracing.W3CInstrumentationTests
{
    /// <summary>
    /// This test runs the python-based W3C validation tests found at https://github.com/w3c/trace-context/tree/master/test
    /// 
    /// This test currently only runs for Framework. There is nothing preventing this from running in Core, however.
    /// 
    /// This test requires python 3.6.0+ AND the aiohttp package to be installed AND python to be in the global PATH.
    /// 
    /// This test is unique in afew ways:
    /// * it pulls down the latest version of the test repo for each test run.  It does this using a new set of git commands in the ConsoleMF app.
    /// * It runs the tests in python.  This is done via a new ProcessRunner option in the ConsoleMF app.
    /// * Requires a very specific web service that the python app can interact with.  This is part of the helpers in the ConsoleMF app
    /// * The python tests can be run directly using consoleMF if external validation is required.
    /// 
    /// The test process is as follows:
    /// * Starts the web service that the tests target.  This is a custom Owin app we created (as required by the tests)
    /// * Pulls down the test repo from github
    /// * Builds the python process, setting the require env vars, and working directory, and command
    /// * Starts the python process and runs the validation tests
    /// * Waits for the process to exit OR 30 seconds to elapse
    /// * Writes the output from StdOut and StdError to console
    /// * Writes the exit code to the console.
    /// 
    /// At this point our testing takes over.
    /// * Parse the exit code from the log.
    /// * If the code is 0, we consider this a pass. (tested to confirm python follows the proper exit code rules)
    /// * If the code is not 0, we fail the test.
    /// 
    /// IMPORTANT
    /// The result of the test is an exit code from the python process, which is a bit unusual for our tests.
    /// This was done due to minor differences in the output format of the tests, likely due to variations in python version, aiohttp version, etc.
    /// Instead of attempting to cover all those possible variations, this test instead relies on the exit code.
    /// Testing was done and python does appear to follow the proper exit code procedures.
    /// This makes troubleshooting a bit more difficult since you have to dig through the output for the test and find the failures.
    /// </summary>
    [NetFrameworkTest]
    public class W3CValidation : NewRelicIntegrationTest<ConsoleDynamicMethodFixtureFWLatest>
    {
        protected readonly ConsoleDynamicMethodFixtureFWLatest _fixture;

        public W3CValidation(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    _fixture.RemoteApplication.NewRelicConfig.SetLogLevel("finest");
                    _fixture.RemoteApplication.NewRelicConfig.SetRequestTimeout(TimeSpan.FromSeconds(10));
                    _fixture.RemoteApplication.NewRelicConfig.ForceTransactionTraces();

                    _fixture.AddCommand($"W3CTestService StartService {_fixture.RemoteApplication.Port}");
                    _fixture.AddCommand($@"GitCommand Clone https://github.com/w3c/trace-context.git {Path.Combine(_fixture.RemoteApplication.DestinationApplicationDirectoryPath, "trace-context")}");
                    _fixture.AddCommand($@"GitCommand Checkout { Path.Combine(_fixture.RemoteApplication.DestinationApplicationDirectoryPath, "trace-context")} 98f210efd89c63593dce90e2bae0a1bdcb986f51");
                    _fixture.AddCommand("ProcessRunner ProcessName python.exe");
                    _fixture.AddCommand("ProcessRunner AddArgument -m unittest");
                    _fixture.AddCommand("ProcessRunner AddSwitch -v");
                    _fixture.AddCommand($@"ProcessRunner WorkingDirectory {Path.Combine(_fixture.RemoteApplication.DestinationApplicationDirectoryPath, "trace-context", "test")}"); // python W3C tests are in test dir
                    _fixture.AddCommand($"ProcessRunner AddEnvironmentVariable SERVICE_ENDPOINT http://localhost:{_fixture.RemoteApplication.Port}/test");
                    _fixture.AddCommand($"ProcessRunner AddEnvironmentVariable STRICT_LEVEL 1");
                    _fixture.AddCommand("ProcessRunner Start");
                    _fixture.AddCommand("ProcessRunner WaitforExit 30000");
                    _fixture.AddCommand("ProcessRunner RawStandardOutput");
                    _fixture.AddCommand("ProcessRunner RawStandardError");
                    _fixture.AddCommand("ProcessRunner ExitCode");

                    _fixture.SetTimeout(TimeSpan.FromMinutes(5));
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            // get the output that was sent to the console
            var processOutput = _fixture.ReturnProcessOutput();

            Assert.True(processOutput.Contains("LASTEXITCODE"), "Could not find LASTEXITCODE, something went wrong!");

            var startLastExitCode = processOutput.IndexOf("LASTEXITCODE") + 15;
            var endLastExitCode = processOutput.IndexOf("\r\n", startLastExitCode);
            var exitCodeString = processOutput.Substring(startLastExitCode, endLastExitCode - startLastExitCode);
            var exitCodeInt = Convert.ToInt32(exitCodeString);

            NrAssert.Multiple(
                () => Assert.False(exitCodeInt > 0, $"Python exited with {exitCodeInt}! Check the output for the failure(s)!")
            );
        }
    }
}
