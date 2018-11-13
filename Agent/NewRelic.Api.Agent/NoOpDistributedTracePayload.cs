namespace NewRelic.Api.Agent
{
	internal class NoOpDistributedTracePayload : IDistributedTracePayload
	{
		public string HttpSafe()
		{
			return string.Empty;
		}

		public string Text()
		{
			return string.Empty;
		}

		public bool IsEmpty()
		{
			return true;
		}
	}
}
