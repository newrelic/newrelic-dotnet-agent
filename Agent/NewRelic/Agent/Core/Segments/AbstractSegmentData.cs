using System;
using System.Collections.Generic;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Spans;
using Attribute = NewRelic.Agent.Core.Attributes.Attribute;

namespace NewRelic.Agent.Core.Segments
{
	public abstract class AbstractSegmentData : ISegmentData
	{
		public virtual SpanCategory SpanCategory => SpanCategory.Generic;

		/// <summary>
		/// Called when the owning segment finishes.  Returns an enumerable 
		/// of the segment parameters or null if none are applicable.
		/// </summary>
		/// <returns></returns>
		internal virtual IEnumerable<KeyValuePair<string, object>> Finish()
		{
			return null;
		}

		public abstract bool IsCombinableWith(AbstractSegmentData otherData);
		public abstract string GetTransactionTraceName();
		public abstract void AddMetricStats(Segment segment, TimeSpan durationOfChildren, TransactionMetricStatsCollection txStats, IConfigurationService configService);

		public virtual void AddSpanTypeSpecificAttributes(AttributeCollection attributes, Segment segment)
		{
			attributes.Add(Attribute.BuildSpanCategoryAttribute(SpanCategory.Generic));
		}

		internal virtual void AddTransactionTraceParameters(IConfigurationService configurationService, Segment segment, IDictionary<string, object> segmentParameters, ImmutableTransaction immutableTransaction)
		{
		}
	}
}
