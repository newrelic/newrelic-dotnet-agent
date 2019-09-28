namespace NewRelic.Api.Agent
{
	internal class NoOpTraceMetadata : ITraceMetadata
	{
		public string TraceId => string.Empty;

		public string SpanId => string.Empty;

		public bool IsSampled => false;
	}
}
