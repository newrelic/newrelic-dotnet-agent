/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System.Collections.Generic;

namespace NewRelic.Agent.Core.Commands
{
    public abstract class AbstractCommand : ICommand
    {
        public string Name { get; protected set; }

        public abstract object Process(IDictionary<string, object> arguments);
    }
}
