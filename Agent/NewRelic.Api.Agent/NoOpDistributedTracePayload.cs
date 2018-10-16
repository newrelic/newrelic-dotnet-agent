namespace NewRelic.Api.Agent
{
	internal class NoOpDistributedTracePayload : IDistributedTracePayload
	{
		public string HttpSafe => string.Empty;

		public string Text => string.Empty;
	}
}
