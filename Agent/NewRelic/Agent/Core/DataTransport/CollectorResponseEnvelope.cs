using JetBrains.Annotations;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.DataTransport
{
	public class CollectorResponseEnvelope<T>
	{
		[CanBeNull]
		[JsonProperty("exception")]
		public readonly CollectorExceptionEnvelope CollectorExceptionEnvelope;

		[CanBeNull]
		[JsonProperty("return_value")]
		public readonly T ReturnValue;

		public CollectorResponseEnvelope([CanBeNull] CollectorExceptionEnvelope collectorExceptionEnvelope, [CanBeNull] T returnValue)
		{
			CollectorExceptionEnvelope = collectorExceptionEnvelope;
			ReturnValue = returnValue;
		}
	}
}