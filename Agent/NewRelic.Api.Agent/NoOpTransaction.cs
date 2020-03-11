namespace NewRelic.Api.Agent
{
	internal class NoOpTransaction : ITransaction
	{
		private static IDistributedTracePayload _noOpDistributedTracePayload = new NoOpDistributedTracePayload();
		private static ISpan _noOpSpan = new NoOpSpan();

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

		public ISpan CurrentSpan => _noOpSpan;
	}
}
