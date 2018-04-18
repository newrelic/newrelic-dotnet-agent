using System;
using JetBrains.Annotations;
using NewRelic.Agent.Core.DataTransport;

namespace NewRelic.Agent.Core.Configuration
{
    public class SecurityPolicy
    {
		[NotNull]
		public string Name { get; private set; }
		[NotNull]
		public bool Enabled { get; private set; }

		public SecurityPolicy(string name, bool enabled)
		{
			Name = name;
			Enabled = enabled;
		}
	}
}
