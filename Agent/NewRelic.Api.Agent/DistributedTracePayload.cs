namespace NewRelic.Api.Agent
{
	internal class DistributedTracePayload : IDistributedTracePayload
	{
		private dynamic _distributedTracePayload;
		private static IDistributedTracePayload _noOpDistributedTracePayload = new NoOpDistributedTracePayload();

		internal DistributedTracePayload(dynamic distributedTracePayload)
		{
			_distributedTracePayload = distributedTracePayload;
		}

		public string HttpSafe => string.Empty;

		public string Text => string.Empty;
	}
}
