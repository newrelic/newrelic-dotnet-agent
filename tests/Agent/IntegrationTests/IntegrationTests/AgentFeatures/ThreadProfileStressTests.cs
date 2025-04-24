// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Diagnostics;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.AgentFeatures
{
    public class ThreadProfileStressTests : NewRelicIntegrationTest<ThreadProfileStressTestWithCollectorFixture>
    {
        private string _threadProfileString;

        public ThreadProfileStressTests(ThreadProfileStressTestWithCollectorFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            fixture.TestLogger = output;

            fixture.AddActions(
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);
                    configModifier.SetLogLevel("debug");
                    configModifier.ConfigureFasterGetAgentCommandsCycle(10);
                },
                exerciseApplication: () =>
                {
                    var stopWatch = new Stopwatch();
                    stopWatch.Start();

                    fixture.TestLogger?.WriteLine("[ThreadProfileStressTests] Requesting a thread profile run at {0} ms.", stopWatch.ElapsedMilliseconds);
                    fixture.TriggerThreadProfile();
                    fixture.AgentLog.WaitForLogLine(AgentLogBase.ThreadProfileStartingLogLineRegex, TimeSpan.FromMinutes(1));
                    fixture.TestLogger?.WriteLine("[ThreadProfileStressTests] Thread profile run detected at {0} ms.", stopWatch.ElapsedMilliseconds);

                    //Wait for the thread profile run to begin before triggering the scenario because if we don't the agent has to compete with process
                    //for thread pool resources and thread scheduling so the timing of things don't always work out.
                    fixture.StartThreadStressScenario();

                    //We need to wait long enough for the thread profile run to finish
                    try
                    {
                        var threadProfileMatch = fixture.AgentLog.WaitForLogLine(AgentLogBase.ThreadProfileDataLogLineRegex, TimeSpan.FromMinutes(2));
                        _threadProfileString = threadProfileMatch.Value;
                        fixture.TestLogger?.WriteLine("[ThreadProfileStressTests] Retrieved thread profile at {0} ms.", stopWatch.ElapsedMilliseconds);
                    }
                    catch (Exception e)
                    {
                        fixture.TestLogger?.WriteLine("Thread profiler session did not end. {0}", e);
                        fixture.TestLogger?.WriteLine("[ThreadProfileStressTests] Begin Profiler log.");
                        fixture.TestLogger?.WriteLine(fixture.ProfilerLog.GetFullLogAsString());
                        fixture.TestLogger?.WriteLine("[ThreadProfileStressTests] End Profiler log.");
                        throw;
                    }

                    stopWatch.Stop();
                }
            );
            fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            NrAssert.Multiple(
                () => Assert.Contains(@"""OTHER"":[[[""Native"",""Function Call"",0]", _threadProfileString),
                () => Assert.Contains(@"[""ThreadProfileStressTest.Program"",""Main"",0]", _threadProfileString),
                () => Assert.Contains(@"[""ThreadProfileStressTest.Program"",""DoTheThing"",0]", _threadProfileString)
            );

            // This assertion has been commented out since the purpose of this test is to verify that the profiler doesn't CRASH
            // when threads are frequently created/destroyed. In this stress scenario we are unable to capture stack traces for finalized
            // threads. Ideally we would get atleast one sample with the 'DoWork' method, but the purpose of the test is to verify we don't crash

            // Assert.Contains(@"[""ThreadProfileStressTest.Program"",""DoWork"",0]", _threadProfileString);
        }
    }
}
