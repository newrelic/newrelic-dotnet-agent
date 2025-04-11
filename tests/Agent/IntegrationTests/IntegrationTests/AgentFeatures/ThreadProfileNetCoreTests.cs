// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Diagnostics;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using System.Threading;
using NewRelic.Testing.Assertions;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.AgentFeatures
{
    public abstract class ThreadProfileNetCoreTestsBase<TFixture> : NewRelicIntegrationTest<TFixture> where TFixture: AspNetCoreWebApiWithCollectorFixture
    {
        private readonly AspNetCoreWebApiWithCollectorFixture _fixture;
        private string _threadProfileString;

        protected ThreadProfileNetCoreTestsBase(TFixture fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.AddActions(
                setupConfiguration: () =>
                {
                    //Increasing the log level to attempt to diagnose the test runs where the profiling session does not terminate.
                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                    configModifier.SetLogLevel("finest");
                    configModifier.ConfigureFasterGetAgentCommandsCycle(10);
                },
                exerciseApplication: () =>
                {
                    var stopWatch = new Stopwatch();
                    stopWatch.Start();

                    _fixture.TestLogger?.WriteLine("[ThreadProfileNetCoreTests] Making first request to application at {0} ms.", stopWatch.ElapsedMilliseconds);
                    _fixture.Get();

                    _fixture.TestLogger?.WriteLine("[ThreadProfileNetCoreTests] Requesting a thread profile run at {0} ms.", stopWatch.ElapsedMilliseconds);
                    _fixture.TriggerThreadProfile();
                    _fixture.AgentLog.WaitForLogLine(AgentLogFile.ThreadProfileStartingLogLineRegex, TimeSpan.FromMinutes(1));
                    _fixture.TestLogger?.WriteLine("[ThreadProfileNetCoreTests] Thread profile run detected at {0} ms.", stopWatch.ElapsedMilliseconds);

                    var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    RequestUntilCancelled(cancellationTokenSource);
                    _fixture.TestLogger?.WriteLine("[ThreadProfileNetCoreTests] Stopped exercising the app at {0} ms.", stopWatch.ElapsedMilliseconds);

                    //We need to wait long enough for the thread profile run to finish
                    try
                    {
                        var threadProfileMatch = _fixture.AgentLog.WaitForLogLine(AgentLogFile.ThreadProfileDataLogLineRegex, TimeSpan.FromMinutes(2));
                        _threadProfileString = threadProfileMatch.Value;
                        _fixture.TestLogger?.WriteLine("[ThreadProfileNetCoreTests] Retrieved thread profile at {0} ms.", stopWatch.ElapsedMilliseconds);
                    }
                    catch (Exception e)
                    {
                        _fixture.TestLogger?.WriteLine("Thread profiler session did not end. {0}", e);
                        _fixture.TestLogger?.WriteLine("[ThreadProfileNetCoreTests] Begin Profiler log.");
                        _fixture.TestLogger?.WriteLine(_fixture.ProfilerLog.GetFullLogAsString());
                        _fixture.TestLogger?.WriteLine("[ThreadProfileNetCoreTests] End Profiler log.");
                        throw;
                    }

                    stopWatch.Stop();
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMainMethodSignature = "";

            if (_fixture.GetType() == typeof(AspNetCoreWebApiWithCollectorFixture))
            {
                expectedMainMethodSignature = @"[""AspNetCoreBasicWebApiApplication.Program"",""Main"",0]";
            }

            NrAssert.Multiple(
                () => Assert.Contains(@"""OTHER"":[[[""Native"",""Function Call"",0]", _threadProfileString),
                () => Assert.Contains(expectedMainMethodSignature, _threadProfileString),
                () => Assert.Contains(@"System.Threading", _threadProfileString),
                () => Assert.Contains(@"Microsoft.AspNetCore.Server.Kestrel", _threadProfileString)
            );
        }

        private void RequestUntilCancelled(CancellationTokenSource cancellationTokenSource)
        {
            var loggedException = false;
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    _fixture.Get();
                    //We don't want to hammer the system too much, otherwise the system can get too overwhelmed to take stack trace
                    //snapshots and complete the thread profiling session.
                    Thread.Sleep(25);
                }
                catch (Exception e)
                {
                    //We don't need every request to be successfull, we just need some requests to succeed so that
                    //the profiler can attempt to profile the request's execution. Give the test application some time
                    //to recover. The thread profiler will attempt to scan running threads roughly every 100 ms.

                    if (!loggedException)
                    {
                        _fixture.TestLogger?.WriteLine($"[ThreadProfileNetCoreTests] Encountered exception when executing the test app: {e}");
                        loggedException = true;
                    }

                    Thread.Sleep(TimeSpan.FromMilliseconds(150));
                }
            }
        }
    }

    public class ThreadProfileNetCoreLatestTests : ThreadProfileNetCoreTestsBase<AspNetCoreWebApiWithCollectorFixture>
    {
        public ThreadProfileNetCoreLatestTests(AspNetCoreWebApiWithCollectorFixture fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

}
