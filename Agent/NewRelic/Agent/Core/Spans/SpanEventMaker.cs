using System.Collections.Generic;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Core;
using Attribute = NewRelic.Agent.Core.Attributes.Attribute;

namespace NewRelic.Agent.Core.Spans
{
	public interface ISpanEventMaker
	{
		IEnumerable<SpanEventWireModel> GetSpanEvents(ImmutableTransaction immutableTransaction, string transactionName);
	}

	public class SpanEventMaker : ISpanEventMaker
	{
		private readonly IAttributeService _attributeService;

		public SpanEventMaker(IAttributeService attributeService)
		{
			_attributeService = attributeService;
		}

		public IEnumerable<SpanEventWireModel> GetSpanEvents(ImmutableTransaction immutableTransaction, string transactionName)
		{
			var spanEvents = new List<SpanEventWireModel>();
			var rootSpanId = GuidGenerator.GenerateNewRelicGuid();
			spanEvents.Add(GenerateRootSpan(rootSpanId, immutableTransaction, transactionName));

			foreach (var segment in immutableTransaction.Segments)
			{
				var attributes = GetAttributes(segment, immutableTransaction, rootSpanId);
				spanEvents.Add(GetSpanEvent(immutableTransaction, attributes));
			}

			return spanEvents;
		}

		private SpanEventWireModel GetSpanEvent(ImmutableTransaction immutableTransaction, AttributeCollection attributes)
		{
			var filteredAttributes = _attributeService.FilterAttributes(attributes, AttributeDestinations.SpanEvent);
			var agentAttributes = filteredAttributes.GetAgentAttributesDictionary();
			var intrinsicAttributes = filteredAttributes.GetIntrinsicsDictionary();
			var userAttributes = filteredAttributes.GetUserAttributesDictionary();

			var transactionMetadata = immutableTransaction.TransactionMetadata;
			var priority = immutableTransaction.Priority;

			return new SpanEventWireModel(priority, intrinsicAttributes, userAttributes, agentAttributes);
		}

		/// <summary>
		/// Creates a single root span, much like we do for Transaction Traces, since DT requires that there be only one parent-less span per txn (or at least the UI/Backend is expecting that). 
		/// </summary>
		private SpanEventWireModel GenerateRootSpan(string rootSpanId, ImmutableTransaction immutableTransaction, string transactionName)
		{
			var spanAttributes = new AttributeCollection();

			spanAttributes.Add(immutableTransaction.CommonSpanAttributes);

			// parentId should be null if very first span in trace, but should use DistributedTraceGuid otherwise.
			if (immutableTransaction.TracingState != null && immutableTransaction.TracingState.Guid != null)
			{
				spanAttributes.Add(Attribute.BuildParentIdAttribute(immutableTransaction.TracingState.Guid));
			}

			spanAttributes.Add(Attribute.BuildGuidAttribute(rootSpanId));
			spanAttributes.Add(Attribute.BuildTimestampAttribute(immutableTransaction.StartTime));
			spanAttributes.Add(Attribute.BuildDurationAttribute(immutableTransaction.Duration));
			spanAttributes.Add(Attribute.BuildNameAttributeForSpanEvent(transactionName));

			spanAttributes.Add(Attribute.BuildSpanCategoryAttribute(SpanCategory.Generic));
			spanAttributes.Add(Attribute.BuildNrEntryPointAttribute(true));

			AddTransactionCustomAttributesToRootSpan(spanAttributes, immutableTransaction);

			return GetSpanEvent(immutableTransaction, spanAttributes);
		}

		private static AttributeCollection GetAttributes(Segment segment, ImmutableTransaction immutableTransaction, string rootSpanId)
		{
			var spanAttributes = new AttributeCollection();

			spanAttributes.Add(immutableTransaction.CommonSpanAttributes);

			// parentId, Should be either the spanId of the parent segment OR the spanId of the faux root span in the case the current segment has a null ParentUniqueId.
			spanAttributes.Add(Attribute.BuildParentIdAttribute(GetParentSpanId(segment, immutableTransaction, rootSpanId)));
			spanAttributes.Add(Attribute.BuildGuidAttribute(segment.SpanId));
			spanAttributes.Add(Attribute.BuildTimestampAttribute(immutableTransaction.StartTime.Add(segment.RelativeStartTime)));
			spanAttributes.Add(Attribute.BuildDurationAttribute(segment.DurationOrZero));
			spanAttributes.Add(Attribute.BuildNameAttributeForSpanEvent(segment.GetTransactionTraceName()));

			segment.Data.AddSpanTypeSpecificAttributes(spanAttributes, segment);

			AddSegmentCustomAttributesToSpan(spanAttributes, segment);

			return spanAttributes;
		}

		///  TODO:  This should pobably add the intrinsics, agents too.
		private static void AddTransactionCustomAttributesToRootSpan(AttributeCollection attributes, ImmutableTransaction transaction)
		{
			attributes.TryAddAll(Attribute.BuildCustomAttributeForSpan, transaction.TransactionMetadata.UserAttributes);
		}

		private static void AddSegmentCustomAttributesToSpan(AttributeCollection attributes, Segment segment)
		{
			attributes.TryAddAll(Attribute.BuildCustomAttributeForSpan, segment.CustomAttributes);
		}

		/// <summary>
		/// Determines the parentId (SpanId) for the span being created.
		/// </summary>
		/// <param name="segment">Current segment being processed into a Span Event.</param>
		/// <param name="immutableTransaction">Current transaction whose segments are being processed into Span Events.</param>
		/// <param name="rootSpanId">SpanId of the faux root segment.</param>
		/// <returns>SpanId of the parent segment.</returns>
		private static string GetParentSpanId(Segment segment, ImmutableTransaction immutableTransaction, string rootSpanId)
		{
			if (segment.ParentUniqueId == null)
			{
				return rootSpanId;
			}

			foreach (var otherSegment in immutableTransaction.Segments)
			{
				if (otherSegment.UniqueId != segment.ParentUniqueId)
				{
					continue;
				}

				return otherSegment.SpanId;
			}

			return rootSpanId;
		}
	}
}
