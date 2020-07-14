using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace NewRelic.Agent.Core.Configuration
{
	public class RunTimeConfiguration
	{
		[NotNull]
		public IEnumerable<String> ApplicationNames;

		public RunTimeConfiguration()
		{
			ApplicationNames = Enumerable.Empty<String>();
		}

		public RunTimeConfiguration([NotNull] IEnumerable<String> applicationNames)
		{
			ApplicationNames = applicationNames.ToList();
		}
	}
}
