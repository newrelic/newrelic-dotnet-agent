// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

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

        public override object Process(IDictionary<string, object> arguments)
        {
            EventBus<RestartAgentEvent>.Publish(new RestartAgentEvent());
            return null;
        }
    }
}
