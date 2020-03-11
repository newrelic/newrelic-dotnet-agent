using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Core.DistributedTracing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NewRelic.Agent.Core.DistributedTracing
{
	public interface ITracingState
	{
		DistributedTracingParentType Type { get; }

		string AppId { get; }

		string AccountId { get; }

		TransportType TransportType { get; }

		string Guid { get; }

		DateTime Timestamp { get; }

		string TraceId { get; }

		string TransactionId { get; }

		bool? Sampled { get; }

		float? Priority { get; }

		List<IngestErrorType> IngestErrors { get; }

		List<string> VendorStateEntries { get; }
	}

	public class TracingState : ITracingState
	{
		public DistributedTracingParentType Type
		{
			get
			{
				if (_newRelicPayload?.Type != null)
				{
					return (DistributedTracingParentType)Enum.Parse(typeof(DistributedTracingParentType), _newRelicPayload?.Type);
				}

				return DistributedTracingParentType.None;
			}
		}

		public string AppId => _newRelicPayload?.AppId;

		public string AccountId => _newRelicPayload?.AccountId;

		public TransportType TransportType { get; private set; }

		public string Guid => _newRelicPayload?.Guid;

		public DateTime Timestamp => _newRelicPayload != null ? (_newRelicPayload.Timestamp != null ? (DateTime)_newRelicPayload.Timestamp : (DateTime)default) : (DateTime)default;

		public string TraceId => _newRelicPayload?.TraceId;

		public string TransactionId => _newRelicPayload?.TransactionId;

		public bool? Sampled => (_newRelicPayload?.Sampled != null && _newRelicPayload.Sampled.HasValue) ? _newRelicPayload.Sampled : null;

		public float? Priority => (_newRelicPayload != null && _newRelicPayload.Priority.HasValue) ? _newRelicPayload.Priority : null;

		public List<IngestErrorType> IngestErrors { get; private set; }

		public List<string> VendorStateEntries => (_traceContext.VendorStateEntries); 

		private DistributedTracePayload _newRelicPayload;
		private W3CTraceContext _traceContext;

		public static ITracingState AcceptDistributedTracePayload(string encodedPayload, TransportType transportType, string agentTrustKey)
		{
			var tracingState = new TracingState();
			var errors = new List<IngestErrorType>();
			tracingState._newRelicPayload = DistributedTracePayload.TryDecodeAndDeserializeDistributedTracePayload(encodedPayload, agentTrustKey, errors);

			if (errors.Any())
			{
				tracingState.IngestErrors = errors;
			}

			tracingState.TransportType = transportType;

			return tracingState;
		}

		public static ITracingState AcceptDistributedTraceHeaders(Func<string, IList<string>> getHeaders, TransportType transportType, string agentTrustKey)
		{
			var tracingState = new TracingState();
			var errors = new List<IngestErrorType>();

			// newrelic 
			var newRelicHeaderValue = getHeaders(Constants.DistributedTracePayloadKey).FirstOrDefault();

			if (!string.IsNullOrWhiteSpace(newRelicHeaderValue))
			{
				tracingState._newRelicPayload = DistributedTracePayload.TryDecodeAndDeserializeDistributedTracePayload(newRelicHeaderValue, agentTrustKey, errors);
			}

			if (errors.Any())
			{
				tracingState.IngestErrors = errors;
			}

			// w3c
			tracingState._traceContext = W3CTraceContext.TryGetTraceContextFromHeaders(getHeaders, transportType, agentTrustKey);


			tracingState.TransportType = transportType;

			return tracingState;
		}
	}
}
