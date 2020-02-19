using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Segments;
using Attribute = NewRelic.Agent.Core.Attributes.Attribute;

namespace NewRelic.Agent.Core.Transactions
{
	public class ImmutableTransaction
	{
		public readonly ITransactionName TransactionName;

		public readonly IEnumerable<Segment> Segments;

		public readonly ImmutableTransactionMetadata TransactionMetadata;

		public readonly DateTime StartTime;
		public readonly TimeSpan Duration;
		public readonly TimeSpan ResponseTimeOrDuration;

		public readonly string Guid;

		public readonly bool IgnoreAutoBrowserMonitoring;
		public readonly bool IgnoreAllBrowserMonitoring;
		public readonly bool IgnoreApdex;

		// The sqlObfuscator parameter should be the SQL obfuscator as defined by user configuration: obfuscate, off, or raw.
		public ImmutableTransaction(ITransactionName transactionName, IEnumerable<Segment> segments, ImmutableTransactionMetadata transactionMetadata, DateTime startTime, TimeSpan duration, TimeSpan? responseTime, string guid, bool ignoreAutoBrowserMonitoring, bool ignoreAllBrowserMonitoring, bool ignoreApdex)
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
						Attribute.BuildDistributedTraceIdAttributes(TransactionMetadata.DistributedTraceTraceId ?? Guid),
						Attribute.BuildTransactionIdAttribute(Guid),
						Attribute.BuildSampledAttribute(TransactionMetadata.DistributedTraceSampled),
						Attribute.BuildPriorityAttribute(TransactionMetadata.Priority)
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
