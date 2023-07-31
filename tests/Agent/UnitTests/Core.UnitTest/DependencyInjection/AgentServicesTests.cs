// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Fixtures;
using NewRelic.Agent.Core.Wrapper;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.DependencyInjection
{
    [TestFixture]
    public class AgentServicesTests
    {
        [Test]
        public void ConfigurationServiceCanFullyResolve()
        {
            // Prevent the ConnectionManager from trying to connect to anything
            var configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.AutoStartAgent).Returns(false);

            using (new ConfigurationAutoResponder(configuration))
            using (var container = AgentServices.GetContainer())
            {
                AgentServices.RegisterServices(container);
                Assert.DoesNotThrow(() => container.Resolve<IConfigurationService>());
            }
        }

        [Test]
        public void AllServicesCanFullyResolve()
        {
            // Prevent the ConnectionManager from trying to connect to anything
            var configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.AutoStartAgent).Returns(false);
            Mock.Arrange(() => configuration.NewRelicConfigFilePath).Returns("c:\\");
            var configurationService = Mock.Create<IConfigurationService>();
            Mock.Arrange(() => configurationService.Configuration).Returns(configuration);

            using (new ConfigurationAutoResponder(configuration))
            using (var container = AgentServices.GetContainer())
            {
                AgentServices.RegisterServices(container);
                container.ReplaceInstanceRegistration(configurationService);
#if NET
                container.ReplaceRegistrations(); // creates a new scope, registering the replacement instances from all .ReplaceRegistration() calls above
#endif

                Assert.DoesNotThrow(() => container.Resolve<IWrapperService>());
                Assert.DoesNotThrow(() => AgentServices.StartServices(container));
            }
        }

    }
}
