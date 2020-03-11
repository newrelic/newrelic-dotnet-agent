using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Core.Segments;
using Attribute = NewRelic.Agent.Core.Attributes.Attribute;

namespace NewRelic.Agent.Core.Transactions
{
	public class ImmutableTransaction
	{
		public readonly ITransactionName TransactionName;

		public readonly IEnumerable<Segment> Segments;

		public readonly IImmutableTransactionMetadata TransactionMetadata;

		public readonly DateTime StartTime;
		public readonly TimeSpan Duration;
		public readonly TimeSpan ResponseTimeOrDuration;

		public readonly string Guid;

		public readonly bool IgnoreAutoBrowserMonitoring;
		public readonly bool IgnoreAllBrowserMonitoring;
		public readonly bool IgnoreApdex;
		public readonly float Priority;
		public readonly bool Sampled;
		public readonly string TraceId;
		public readonly ITracingState TracingState;

		// The sqlObfuscator parameter should be the SQL obfuscator as defined by user configuration: obfuscate, off, or raw.
		public ImmutableTransaction(ITransactionName transactionName, IEnumerable<Segment> segments, IImmutableTransactionMetadata transactionMetadata, DateTime startTime, TimeSpan duration, TimeSpan? responseTime, string guid, bool ignoreAutoBrowserMonitoring, bool ignoreAllBrowserMonitoring, bool ignoreApdex, float priority, bool? sampled, string traceId, ITracingState tracingState)
		{
			TransactionName = transactionName;
			Segments = segments.Where(segment => segment != null).ToList();
			TransactionMetadata = transactionMetadata;
			StartTime = startTime;
			Duration = duration;
			ResponseTimeOrDuration = IsWebTransaction() ? responseTime ?? Duration : Duration;
			Guid = guid;
			IgnoreAutoBrowserMonitoring = ignoreAutoBrowserMonitoring;
			IgnoreAllBrowserMonitoring = ignoreAllBrowserMonitoring;
			IgnoreApdex = ignoreApdex;
			Priority = priority;
			Sampled = sampled.HasValue ? sampled.Value : false; // TODO: only tests call this constructor except for TransactionFinalizer and TxTransformer, the latter setting sampled before calling it. 
			TraceId = traceId;
			TracingState = tracingState;
		}

		private Attribute[] _commonSpanAttributes;
		public Attribute[] CommonSpanAttributes
		{
			get
			{
				if (_commonSpanAttributes == null)
				{
					_commonSpanAttributes = new Attribute[] {
						Attribute.BuildTypeAttribute(TypeAttributeValue.Span),
						Attribute.BuildDistributedTraceIdAttributes(TraceId ?? Guid),
						Attribute.BuildTransactionIdAttribute(Guid),
						Attribute.BuildSampledAttribute(Sampled),
						Attribute.BuildPriorityAttribute(Priority)
					};
				}

				return _commonSpanAttributes;
			}
		}

		public bool IsWebTransaction()
		{
			return TransactionName.IsWeb;
		}
	}
}
