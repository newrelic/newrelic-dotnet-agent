namespace NewRelic.Api.Agent
{
	internal class NoOpTransaction : ITransaction
	{
		private static IDistributedTracePayload _noOpDistributedTracePayload = new NoOpDistributedTracePayload();

		public void AcceptDistributedTracePayload(string payload)
		{
		}

		public void AcceptDistributedTracePayload(IDistributedTracePayload payload)
		{
		}

		public IDistributedTracePayload CreateDistributedTracePayload()
		{
			return _noOpDistributedTracePayload;
		}
	}
}
