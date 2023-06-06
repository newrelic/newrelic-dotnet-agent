// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Core.Logging;
using NewRelic.SystemExtensions.Collections.Generic;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace NewRelic.Agent.Core.Commands
{
    public class CommandService : DisposableService
    {
        private readonly IDictionary<string, ICommand> _knownCommands = new Dictionary<string, ICommand>();

        private readonly IDataTransportService _dataTransportService;

        private readonly IScheduler _scheduler;

        private readonly IConfigurationService _configurationService;

        public CommandService(IDataTransportService dataTransportService, IScheduler scheduler, IConfigurationService configurationService)
        {
            _dataTransportService = dataTransportService;
            _configurationService = configurationService;
            _scheduler = scheduler;

            _scheduler.ExecuteEvery(GetAndExecuteAgentCommandsAction, _configurationService.Configuration.GetAgentCommandsCycle);
        }

        private void GetAndExecuteAgentCommandsAction() => Task.Run(GetAndExecuteAgentCommandsAsync).GetAwaiter().GetResult();

        public override void Dispose()
        {
            _scheduler.StopExecuting(GetAndExecuteAgentCommandsAction);
        }

        public void AddCommands(params ICommand[] commands)
        {
            foreach (var command in commands)
            {
                if (command != null)
                {
                    _knownCommands.Add(command.Name, command);
                }
            }
        }

        private async Task GetAndExecuteAgentCommandsAsync()
        {
            var commands = await _dataTransportService.GetAgentCommandsAsync();
            var commandResults = ProcessCommands(commands);
            if (commandResults.Count < 1)
                return;

            await _dataTransportService.SendCommandResultsAsync(commandResults);
        }

        public IDictionary<string, object> ProcessCommands(IEnumerable<CommandModel> commandModels)
        {
            var results = new Dictionary<string, object>();

            if (commandModels == null)
                return results;

            foreach (var commandModel in commandModels)
            {
                if (commandModel == null)
                    continue;
                if (commandModel.Details == null)
                    continue;

                var id = commandModel.CommandId;
                var name = commandModel.Details.Name;
                var arguments = commandModel.Details.Arguments;

                if (name == null)
                    continue;
                if (arguments == null)
                    continue;

                object returnValue;
                var command = _knownCommands.GetValueOrDefault(name);
                if (command == null)
                {
                    var msg = $"Ignoring command named '{name}' that the agent doesn't know how to execute";
                    Log.Debug(msg);
                    returnValue = new Dictionary<string, object>
                    {
                        { "errors", msg },
                        { "error", msg }
                    };
                }
                else
                {
                    returnValue = command.Process(arguments);
                }

                results.Add(id.ToString(CultureInfo.InvariantCulture), returnValue);
            }
            return results;
        }
    }
}
