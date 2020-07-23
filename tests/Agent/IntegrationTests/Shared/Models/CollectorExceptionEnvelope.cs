using System;
using JetBrains.Annotations;

namespace NewRelic.Agent.IntegrationTests.Shared.Models
{
	public class CollectorExceptionEnvelope
	{
		[NotNull]
		public readonly String Exception;

		public CollectorExceptionEnvelope([NotNull] Exception exception)
		{
			Exception = null;
		}
	}
}
