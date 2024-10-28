// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NET
using System;
using System.Collections.Generic;
using NewRelic.Agent.Core.Samplers;
using NewRelic.Agent.Core.Transformers;
#endif

using Autofac.Core.Registration;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Commands;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Fixtures;
using NewRelic.Agent.Core.Utilities;
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
                AgentServices.RegisterServices(container, false, false);
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
                AgentServices.RegisterServices(container, false, false);

                container.ReplaceInstanceRegistration(configurationService);
#if NET
                container.ReplaceRegistrations(); // creates a new scope, registering the replacement instances from all .ReplaceRegistration() calls above
#endif

                Assert.DoesNotThrow(() => container.Resolve<IWrapperService>());
                Assert.DoesNotThrow(() => AgentServices.StartServices(container, false, false));
            }
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void CorrectServicesAreRegistered_BasedOnServerlessMode(bool serverlessModeEnabled)
        {
            // Arrange
            var configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.AutoStartAgent).Returns(false);
            Mock.Arrange(() => configuration.NewRelicConfigFilePath).Returns("c:\\");
            var configurationService = Mock.Create<IConfigurationService>();
            Mock.Arrange(() => configurationService.Configuration).Returns(configuration);

            // Act
            using (new ConfigurationAutoResponder(configuration))
            using (var container = AgentServices.GetContainer())
            {
                AgentServices.RegisterServices(container, serverlessModeEnabled, false);

                container.ReplaceInstanceRegistration(configurationService);
#if NET
                container.ReplaceRegistrations(); // creates a new scope, registering the replacement instances from all .ReplaceRegistration() calls above
#endif
                // Assert
                Assert.DoesNotThrow(() => container.Resolve<IWrapperService>());
                Assert.DoesNotThrow(() => AgentServices.StartServices(container, true, false));

                // ensure dependent services are registered
                if (serverlessModeEnabled)
                {
                    Assert.DoesNotThrow(() => container.Resolve<IServerlessModePayloadManager>());
                    var serverlessModePayloadManager = container.Resolve<IServerlessModePayloadManager>();
                    Assert.That(serverlessModePayloadManager.GetType() == typeof(ServerlessModePayloadManager));

                    Assert.DoesNotThrow(() => container.Resolve<IFileWrapper>());
                    var fileWrapper = container.Resolve<IFileWrapper>();
                    Assert.That(fileWrapper.GetType() == typeof(FileWrapper));
                }

                var dataTransportService = container.Resolve<IDataTransportService>();
                var expectedDataTransportServiceType = serverlessModeEnabled ? typeof(ServerlessModeDataTransportService) : typeof(DataTransportService);
                Assert.That(dataTransportService.GetType() == expectedDataTransportServiceType);

                if (serverlessModeEnabled)
                {
                    Assert.Throws<ComponentNotRegisteredException>(() => container.Resolve<IConnectionHandler>());
                    Assert.Throws<ComponentNotRegisteredException>(() => container.Resolve<IConnectionManager>());
                    Assert.Throws<ComponentNotRegisteredException>(() => container.Resolve<CommandService>());
                }
                else
                {
                    Assert.DoesNotThrow(() => container.Resolve<IConnectionHandler>());
                    Assert.DoesNotThrow(() => container.Resolve<IConnectionManager>());
                    Assert.DoesNotThrow(() => container.Resolve<CommandService>());
                }
            }
        }

#if NET
        [TestCase(true)]
        [TestCase(false)]
        public void CorrectServicesAreRegistered_BasedOnGCSamplerV2EnabledMode(bool gcSamplerV2Enabled)
        {
            // Arrange
            var configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.AutoStartAgent).Returns(false);
            Mock.Arrange(() => configuration.NewRelicConfigFilePath).Returns("c:\\");
            var configurationService = Mock.Create<IConfigurationService>();
            Mock.Arrange(() => configurationService.Configuration).Returns(configuration);

            // Act
            using (new ConfigurationAutoResponder(configuration))
            using (var container = AgentServices.GetContainer())
            {
                AgentServices.RegisterServices(container, false, gcSamplerV2Enabled);

                container.ReplaceInstanceRegistration(configurationService);
                container.ReplaceRegistrations(); // creates a new scope, registering the replacement instances from all .ReplaceRegistration() calls above
                // Assert
                Assert.DoesNotThrow(() => container.Resolve<IWrapperService>());
                Assert.DoesNotThrow(() => AgentServices.StartServices(container, false, gcSamplerV2Enabled));

                // ensure dependent services are registered
                if (gcSamplerV2Enabled)
                {
                    Assert.DoesNotThrow(() => container.Resolve<IGCSampleTransformerV2>());
                    Assert.DoesNotThrow(() => container.Resolve<GCSamplerV2>());

                    Assert.Throws<ComponentNotRegisteredException>(() => container.Resolve<Func<ISampledEventListener<Dictionary<GCSampleType, float>>>>());
                    Assert.Throws<ComponentNotRegisteredException>(() => container.Resolve<Func<GCSamplerNetCore.SamplerIsApplicableToFrameworkResult>>());
                    Assert.Throws<ComponentNotRegisteredException>(() => container.Resolve<GCSamplerNetCore>());

                }
                else
                {
                    Assert.DoesNotThrow(() => container.Resolve<Func<ISampledEventListener<Dictionary<GCSampleType, float>>>>());
                    Assert.DoesNotThrow(() => container.Resolve<Func<GCSamplerNetCore.SamplerIsApplicableToFrameworkResult>>());
                    Assert.DoesNotThrow(() => container.Resolve<GCSamplerNetCore>());

                    Assert.Throws<ComponentNotRegisteredException>(() => container.Resolve<IGCSampleTransformerV2>());
                    Assert.Throws<ComponentNotRegisteredException>(() => container.Resolve<GCSamplerV2>());
                }
            }
        }
#endif
    }
}
