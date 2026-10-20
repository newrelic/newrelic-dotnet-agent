// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Time;
using Newtonsoft.Json;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Commands;

[TestFixture]
public class CommandServiceTests
{
    private IDataTransportService _dataTransportService;

    [SetUp]
    public void SetUp()
    {
        _dataTransportService = Mock.Create<IDataTransportService>();
    }

    [Test]
    public void TestProcessCommand()
    {
        var command = new PingCommand();
        var commandService = new CommandService(_dataTransportService, Mock.Create<IScheduler>(), Mock.Create<IConfigurationService>());
        commandService.AddCommands(command);
        var commands = JsonConvert.DeserializeObject<IEnumerable<CommandModel>>("[[1,{name:\"ping\",arguments:{}}],[2,{name:\"ping\",arguments:{}}]]");

        var results = commandService.ProcessCommands(commands);

        Assert.That(results, Has.Count.EqualTo(2));
    }

    [Test]
    public void TestRestartCommand()
    {
        var command = new RestartCommand();
        var commandService = new CommandService(_dataTransportService, Mock.Create<IScheduler>(), Mock.Create<IConfigurationService>());
        commandService.AddCommands(command);
        var serverCommand = JsonConvert.DeserializeObject<IEnumerable<CommandModel>>("[[666,{name:\"restart\",arguments:{}}]]");

        var processingResults = commandService.ProcessCommands(serverCommand);

        Assert.Multiple(() =>
        {
            Assert.That(processingResults.ContainsKey("666"), Is.True);
            Assert.That(processingResults["666"], Is.Null);
        });
    }

    [Test]
    public void verify_start_profiler_command_gets_processed()
    {
        var command = new MockCommand("start_profiler");
        var commandService = new CommandService(_dataTransportService, Mock.Create<IScheduler>(), Mock.Create<IConfigurationService>());
        commandService.AddCommands(command);
        var commands = JsonConvert.DeserializeObject<IEnumerable<CommandModel>>("[[666,{name:\"start_profiler\",arguments:{}}]]");

        Assert.That(command.Attempts, Is.EqualTo(0));
        commandService.ProcessCommands(commands);
        Assert.That(command.Attempts, Is.EqualTo(1));
    }

    [Test]
    public void verify_start_profiler_command_requires_profile_id_argument()
    {
        var command = new MockCommand("start_profiler");
        command.RequiredArguments.Add("profile_id");

        var commandService = new CommandService(_dataTransportService, Mock.Create<IScheduler>(), Mock.Create<IConfigurationService>());
        commandService.AddCommands(command);
        var commands = JsonConvert.DeserializeObject<IEnumerable<CommandModel>>("[[666,{name:\"start_profiler\",arguments:{}}]]");

        commandService.ProcessCommands(commands);
    }

    [Test]
    public void verify_stop_profiler_command_gets_processed()
    {
        var command = new MockCommand("stop_profiler");
        var commandService = new CommandService(_dataTransportService, Mock.Create<IScheduler>(), Mock.Create<IConfigurationService>());
        commandService.AddCommands(command);
        var commands = JsonConvert.DeserializeObject<IEnumerable<CommandModel>>("[[666,{name:\"stop_profiler\",arguments:{}}]]");

        Assert.That(command.Attempts, Is.EqualTo(0));
        commandService.ProcessCommands(commands);
        Assert.That(command.Attempts, Is.EqualTo(1));
    }

    [Test]
    public void a_command_that_throws_reports_an_error_instead_of_aborting_the_batch()
    {
        var throwingCommand = new ThrowingCommand("throws");
        var pingCommand = new PingCommand();
        var commandService = new CommandService(_dataTransportService, Mock.Create<IScheduler>(), Mock.Create<IConfigurationService>());
        commandService.AddCommands(throwingCommand, pingCommand);
        var commands = JsonConvert.DeserializeObject<IEnumerable<CommandModel>>(
            "[[1,{name:\"throws\",arguments:{}}],[2,{name:\"ping\",arguments:{}}]]");

        var results = commandService.ProcessCommands(commands);

        Assert.Multiple(() =>
        {
            Assert.That(results, Has.Count.EqualTo(2));
            Assert.That(((IDictionary<string, object>)results["1"]).ContainsKey("error"), Is.True);
            Assert.That(((IDictionary<string, object>)results["1"]).ContainsKey("errors"), Is.True);
            Assert.That(pingCommand.Count, Is.EqualTo(1));
        });
    }
}

public class ThrowingCommand : AbstractCommand
{
    public ThrowingCommand(string commandName)
    {
        Name = commandName;
    }

    public override object Process(IDictionary<string, object> arguments)
    {
        throw new System.InvalidOperationException("boom");
    }
}

public class MockCommand : AbstractCommand
{
    public int Attempts = 0;

    public List<string> RequiredArguments = new List<string>();

    public MockCommand(string commandName)
    {
        Name = commandName;
    }

    public override object Process(IDictionary<string, object> arguments)
    {
        Attempts++;
        return null;
    }
}

public class PingCommand : AbstractCommand
{
    public int Count { get; private set; }

    public PingCommand()
    {
        Count = 0;
        Name = "ping";
    }

    public override object Process(IDictionary<string, object> arguments)
    {
        Count++;
        return null;
    }
}