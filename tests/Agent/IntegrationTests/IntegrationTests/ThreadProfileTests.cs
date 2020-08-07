// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading;
using System.Threading.Tasks;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTests
{
    public class ThreadProfileTests : IClassFixture<MvcWithCollectorFixture>
    {
        private readonly MvcWithCollectorFixture _fixture;

        public ThreadProfileTests(MvcWithCollectorFixture fixture)
        {
            _fixture = fixture;
            _fixture.DelayKill = true;

            _fixture.AddActions(
                exerciseApplication: () =>
                {
                    _fixture.Get();
                    _fixture.TriggerThreadProfile();
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            _fixture.AgentLog.WaitForLogLine(AgentLogFile.ThreadProfileStartingLogLineRegex, TimeSpan.FromMinutes(3));

            var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(2));

            Task.Run(() => RequestUntilCancelled(cancellationTokenSource), cancellationTokenSource.Token).Wait();

            var threadProfileMatch = _fixture.AgentLog.WaitForLogLine(AgentLogFile.ThreadProfileDataLogLineRegex, TimeSpan.FromMinutes(1));
            var threadProfileString = threadProfileMatch.Value;

            Assert.Contains(@"""OTHER"":[[[""Native"",""Function Call"",0]", threadProfileString);
            Assert.Contains(@"[""HostedWebCore.Program"",""Main"",0]", threadProfileString);
            Assert.Contains(@"System.Threading", threadProfileString);
            Assert.Contains(@"System.Web", threadProfileString);
        }

        private void RequestUntilCancelled(CancellationTokenSource cancellationTokenSource)
        {
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                _fixture.Get();
            }
        }
    }
}
