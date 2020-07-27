using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MoreLinq;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Utils;
using NewRelic.SystemExtensions.Collections.Generic;

namespace NewRelic.Agent.Core.Commands
{
    public class CommandService : DisposableService
    {
        private readonly IDictionary<String, ICommand> _knownCommands = new Dictionary<String, ICommand>();
        private readonly IDataTransportService _dataTransportService;
        private readonly IScheduler _scheduler;

        public CommandService(IDataTransportService dataTransportService, IScheduler scheduler)
        {
            _dataTransportService = dataTransportService;
            _scheduler = scheduler;

            _scheduler.ExecuteEvery(GetAndExecuteAgentCommands, TimeSpan.FromMinutes(1));
        }

        public override void Dispose()
        {
            _scheduler.StopExecuting(GetAndExecuteAgentCommands);
        }

        public void AddCommands(params ICommand[] commands)
        {
            commands.Where(command => command != null).ForEach(command => _knownCommands.Add(command.Name, command));
        }

        private void GetAndExecuteAgentCommands()
        {
            var commands = _dataTransportService.GetAgentCommands();
            var commandResults = ProcessCommands(commands);
            if (commandResults.Count < 1)
                return;

            _dataTransportService.SendCommandResults(commandResults);
        }
        public IDictionary<String, Object> ProcessCommands(IEnumerable<CommandModel> commandModels)
        {
            var results = new Dictionary<String, Object>();

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
