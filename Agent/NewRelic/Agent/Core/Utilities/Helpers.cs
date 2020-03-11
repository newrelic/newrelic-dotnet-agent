using System;

namespace NewRelic.Agent.Core.Utilities
{
	public static class Helpers
	{
		public static TimeSpan ComputeDuration(DateTime transactionStart, DateTime payloadStart)
		{
			var duration = transactionStart - payloadStart;
			return duration > TimeSpan.Zero ? duration : TimeSpan.Zero;
		}
	}
}
