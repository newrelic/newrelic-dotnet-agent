using System;
using System.Collections.Generic;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Utilities;

namespace NewRelic.Agent.Core.Commands
{
    /// <summary>
    /// A command that restarts the agent. 
    /// </summary>
    public class RestartCommand : AbstractCommand
    {
        public RestartCommand()
        {
            Name = "restart";
        }

        public override object Process(IDictionary<String, object> arguments)
        {
            EventBus<RestartAgentEvent>.Publish(new RestartAgentEvent());
            return null;
        }
    }
}
