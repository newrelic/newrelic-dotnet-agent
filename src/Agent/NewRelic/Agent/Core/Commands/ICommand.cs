// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace NewRelic.Agent.Core.Commands
{
    /// <summary>
    /// Commands are downloaded from the RPM service and executed. 
    /// </summary>
    public interface ICommand
    {
        /// <summary>
        /// The name of this command.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Executes this command.  This is called from the CommandService
        /// if it receives a command from the rpm service that matches this command.
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns></returns>
        object Process(IDictionary<string, object> arguments); // throws CommandException;
    }
}
