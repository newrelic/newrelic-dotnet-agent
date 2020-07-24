using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.Commands
{
    public abstract class AbstractCommand : ICommand
    {
        public string Name { get; protected set; }

        public abstract object Process(IDictionary<String, object> arguments);
    }
}
