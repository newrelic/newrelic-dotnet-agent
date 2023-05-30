// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Fixtures;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.WireModels;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Utilities
{
    [TestFixture]
    public class UpdatedLoadedModulesServiceTests
    {
        private IDataTransportService _dataTransportService;
        private UpdatedLoadedModulesService _updatedLoadedModulesService;

        private Action _getLoadedModulesAction;
        private ConfigurationAutoResponder _configurationAutoResponder;
        private TimeSpan? _harvestCycle;

        [SetUp]
        public void SetUp()
        {
            var configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.CollectorSendDataOnExit).Returns(true);
            Mock.Arrange(() => configuration.CollectorSendDataOnExitThreshold).Returns(0);
            _configurationAutoResponder = new ConfigurationAutoResponder(configuration);

            _dataTransportService = Mock.Create<IDataTransportService>();

            var scheduler = Mock.Create<IScheduler>();

            Mock.Arrange(() => scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>()))
                .DoInstead<Action, TimeSpan, TimeSpan?>((action, harvestCycle, __) => { _getLoadedModulesAction = action; _harvestCycle = harvestCycle; });

            _updatedLoadedModulesService = new UpdatedLoadedModulesService(scheduler, _dataTransportService);

            EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent());
        }

        [TearDown]
        public void TearDown()
        {
            _updatedLoadedModulesService.Dispose();
            _configurationAutoResponder.Dispose();
        }

        [Test]
        public void GetLoadedModules_SendsModules()
        {
            LoadedModuleWireModelCollection loadedModulesCollection = (LoadedModuleWireModelCollection)null;
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<LoadedModuleWireModelCollection>()))
                .DoInstead<LoadedModuleWireModelCollection>(modules => loadedModulesCollection = modules);

            _getLoadedModulesAction();

            var loadedModules = loadedModulesCollection.LoadedModules;

            Assert.NotNull(loadedModulesCollection);
            Assert.IsTrue(loadedModules.Count > 0);
        }

        [Test]
        public void GetLoadedModules_DoesNot_SendDuplicateModules()
        {
            LoadedModuleWireModelCollection loadedModulesCollection = (LoadedModuleWireModelCollection)null;
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<LoadedModuleWireModelCollection>()))
                .DoInstead<LoadedModuleWireModelCollection>(modules => loadedModulesCollection = modules);

            _getLoadedModulesAction();

            var initialModules = loadedModulesCollection.LoadedModules;

            _getLoadedModulesAction();

            var secondloadedModules = loadedModulesCollection.LoadedModules;

            Assert.Greater(initialModules.Count, secondloadedModules.Count);
        }
    }
}
