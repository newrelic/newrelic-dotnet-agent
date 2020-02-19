namespace NewRelic.Api.Agent
{
	internal class NoOpTransaction : ITransaction
	{
		private static IDistributedTracePayload _noOpDistributedTracePayload = new NoOpDistributedTracePayload();

		public void AcceptDistributedTracePayload(string payload, TransportType transportType = TransportType.Unknown)
		{
		}

		public IDistributedTracePayload CreateDistributedTracePayload()
		{
			return _noOpDistributedTracePayload;
		}

		public ITransaction AddCustomAttribute(string key, object value)
		{
			return this;
		}
	}
}
