using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Api;
using NewRelic.Agent.Core.Transactions;

namespace NewRelic.Agent.Core.Api
{
	public class TraceMetadata : ITraceMetadata
	{
		public static readonly ITraceMetadata EmptyModel = new TraceMetadata(string.Empty, string.Empty, false);

		public string TraceId { get; private set; }

		public string SpanId { get; private set; }

		public bool IsSampled { get; private set; }

		public TraceMetadata(string traceId, string spanId, bool isSampled)
		{
			TraceId = traceId;
			SpanId = spanId;
			IsSampled = isSampled;
		}
	}
	
	public interface ITraceMetadataFactory
	{
		ITraceMetadata CreateTraceMetadata(IInternalTransaction transaction);
	}

	public class TraceMetadataFactory : ITraceMetadataFactory
	{
		private readonly IAdaptiveSampler _adaptiveSampler;

		public TraceMetadataFactory(IAdaptiveSampler adaptiveSampler)
		{
			_adaptiveSampler = adaptiveSampler;
		}

		public ITraceMetadata CreateTraceMetadata(IInternalTransaction transaction)
		{
			var traceId = transaction.TraceId;
			var spanId = transaction.CurrentSegment.SpanId;
			var isSampled = setIsSampled(transaction);

			return new TraceMetadata(traceId, spanId, isSampled);
		}

		private bool setIsSampled(IInternalTransaction transaction)
		{
			// if Sampled has not been set, compute it now
			if (transaction.Sampled != null)
			{
				return (bool)transaction.Sampled;
			}
			else
			{
				transaction.SetSampled(_adaptiveSampler);
				return (bool)transaction.Sampled;
			}
		}
	}
}