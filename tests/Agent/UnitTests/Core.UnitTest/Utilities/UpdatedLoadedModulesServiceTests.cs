// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Fixtures;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.WireModels;
using NUnit.Framework;
using Telerik.JustMock;
using Telerik.JustMock.Helpers;

namespace NewRelic.Agent.Core.Utilities;

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
        Mock.Arrange(() => configuration.UpdateLoadedModulesCycle).Returns(TimeSpan.FromMinutes(1));
        _configurationAutoResponder = new ConfigurationAutoResponder(configuration);

        var configurationService = Mock.Create<IConfigurationService>();
        Mock.Arrange(() => configurationService.Configuration).Returns(configuration);

        _dataTransportService = Mock.Create<IDataTransportService>();

        var scheduler = Mock.Create<IScheduler>();

        Mock.Arrange(() => scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>()))
            .DoInstead<Action, TimeSpan, TimeSpan?>((action, harvestCycle, __) => { _getLoadedModulesAction = action; _harvestCycle = harvestCycle; });

        _updatedLoadedModulesService = new UpdatedLoadedModulesService(scheduler, _dataTransportService, configurationService);

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
        Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<LoadedModuleWireModelCollection>(), Arg.IsAny<string>()))
            .DoInstead<LoadedModuleWireModelCollection>(modules => loadedModulesCollection = modules)
            .Returns<DataTransportResponseStatus>(DataTransportResponseStatus.RequestSuccessful);

        _getLoadedModulesAction();

        var loadedModules = loadedModulesCollection.LoadedModules;

        Assert.Multiple(() =>
        {
            Assert.That(loadedModulesCollection, Is.Not.Null);
            Assert.That(loadedModules, Is.Not.Empty);
        });
    }

    [Test]
    public void GetLoadedModules_NoNewModules()
    {
        LoadedModuleWireModelCollection loadedModulesCollection = (LoadedModuleWireModelCollection)null;
        Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<LoadedModuleWireModelCollection>(), Arg.IsAny<string>()))
            .DoInstead<LoadedModuleWireModelCollection>(modules => loadedModulesCollection = modules)
            .Returns<DataTransportResponseStatus>(DataTransportResponseStatus.RequestSuccessful);

        _getLoadedModulesAction();

        var initialModules = loadedModulesCollection.LoadedModules;

        // double sure that no new modules are loaded.
        _getLoadedModulesAction();

        _ = loadedModulesCollection.LoadedModules;

        _getLoadedModulesAction();

        var loadedModules = loadedModulesCollection.LoadedModules;

        Assert.That(loadedModules, Has.Count.EqualTo(initialModules.Count));
    }

    [Test]
    public void GetLoadedModules_AfterReconnect_ResendsAllModules()
    {
        // Arrange - First send succeeds
        LoadedModuleWireModelCollection loadedModulesCollection = null;
        Mock.Arrange(() => _dataTransportService.Send(
                Arg.IsAny<LoadedModuleWireModelCollection>(), Arg.IsAny<string>()))
            .DoInstead<LoadedModuleWireModelCollection>(modules => loadedModulesCollection = modules)
            .Returns(DataTransportResponseStatus.RequestSuccessful);

        _getLoadedModulesAction();
        var initialModules = loadedModulesCollection.LoadedModules;
        var initialCount = initialModules.Count;
        Assert.That(initialCount, Is.GreaterThan(0), "Initial module count should be greater than 0");

        // Verify deduplication is working (second call sends nothing new)
        _getLoadedModulesAction();
        Assert.That(loadedModulesCollection.LoadedModules.Count, Is.EqualTo(0), "No new modules should be sent on second call");

        // Act - Simulate reconnect
        EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent());

        // Harvest again after reconnect
        _getLoadedModulesAction();
        var afterReconnectModules = loadedModulesCollection.LoadedModules;
        var afterReconnectCount = afterReconnectModules.Count;

        // Assert - All modules should be resent
        // We use GreaterThanOrEqualTo here because it's possible that new modules were loaded between the
        // initial send and the reconnect, but we want to ensure that at least all of the initial modules were resent.
        Assert.That(afterReconnectCount, Is.GreaterThanOrEqualTo(initialCount), "All modules should be resent after reconnect");
    }

    [Test]
    public void GetLoadedModules_SendError_DuplicatesNotSaved()
    {
        LoadedModuleWireModelCollection loadedModulesCollection = (LoadedModuleWireModelCollection)null;
        var result = Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<LoadedModuleWireModelCollection>(), Arg.IsAny<string>()))
            .DoInstead<LoadedModuleWireModelCollection>(modules => loadedModulesCollection = modules)
            .Returns<DataTransportResponseStatus>(DataTransportResponseStatus.Discard);

        _getLoadedModulesAction();

        var initialModules = loadedModulesCollection.LoadedModules;

        _getLoadedModulesAction();

        var loadedModules = loadedModulesCollection.LoadedModules;

        Assert.Multiple(() =>
        {
            Assert.That(initialModules.Count, Is.GreaterThan(0));
            Assert.That(loadedModules.Count, Is.GreaterThan(0));
        });
        Assert.That(loadedModules, Has.Count.EqualTo(initialModules.Count));
    }
}
