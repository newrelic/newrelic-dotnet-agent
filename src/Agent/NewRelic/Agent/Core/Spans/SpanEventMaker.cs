using System.Collections.Generic;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Core;

namespace NewRelic.Agent.Core.Spans
{
    public interface ISpanEventMaker
    {
        IEnumerable<ISpanEventWireModel> GetSpanEvents(ImmutableTransaction immutableTransaction, string transactionName);
    }

    public class SpanEventMaker : ISpanEventMaker
    {
        private readonly IAttributeDefinitionService _attribDefSvc;
        private IAttributeDefinitions _attribDefs => _attribDefSvc?.AttributeDefs;

        public SpanEventMaker(IAttributeDefinitionService attribDefSvc)
        {
            _attribDefSvc = attribDefSvc;
        }

        public IEnumerable<ISpanEventWireModel> GetSpanEvents(ImmutableTransaction immutableTransaction, string transactionName)
        {
            var spanEvents = new List<SpanAttributeValueCollection>();

            var rootSpanId = GuidGenerator.GenerateNewRelicGuid();

            spanEvents.Add(GenerateRootSpan(rootSpanId, immutableTransaction, transactionName));

            foreach (var segment in immutableTransaction.Segments)
            {
                SetAttributes(segment, immutableTransaction, rootSpanId);

                segment.AttribValues.MakeImmutable();

                spanEvents.Add(segment.AttribValues);
            }

            return spanEvents;
        }

        /// <summary>
        /// Creates a single root span, much like we do for Transaction Traces, since DT requires that there be only one parent-less span per txn (or at least the UI/Backend is expecting that). 
        /// </summary>
        private SpanAttributeValueCollection GenerateRootSpan(string rootSpanId, ImmutableTransaction immutableTransaction, string transactionName)
        {
            var spanAttributes = new SpanAttributeValueCollection();
            spanAttributes.Priority = immutableTransaction.Priority;

            spanAttributes.AddRange(immutableTransaction.CommonSpanAttributes);

            if (immutableTransaction.TracingState != null)
            {
                _attribDefs.ParentId.TrySetValue(spanAttributes, immutableTransaction.TracingState.ParentId ?? immutableTransaction.TracingState.Guid);
                _attribDefs.TrustedParentId.TrySetValue(spanAttributes, immutableTransaction.TracingState.Guid);
                _attribDefs.TracingVendors.TrySetValue(spanAttributes, immutableTransaction.TracingState.VendorStateEntries ?? null);
            }

            if (immutableTransaction.TransactionMetadata.ReadOnlyTransactionErrorState.HasError)
            {
                _attribDefs.SpanErrorClass.TrySetValue(spanAttributes, immutableTransaction.TransactionMetadata.ReadOnlyTransactionErrorState.ErrorData.ErrorTypeName);
                _attribDefs.SpanErrorMessage.TrySetValue(spanAttributes, immutableTransaction.TransactionMetadata.ReadOnlyTransactionErrorState.ErrorData.ErrorMessage);
            }

            _attribDefs.Guid.TrySetValue(spanAttributes, rootSpanId);
            _attribDefs.Timestamp.TrySetValue(spanAttributes, immutableTransaction.StartTime);
            _attribDefs.Duration.TrySetValue(spanAttributes, immutableTransaction.Duration);
            _attribDefs.NameForSpan.TrySetValue(spanAttributes, transactionName);

            _attribDefs.SpanCategory.TrySetValue(spanAttributes, SpanCategory.Generic);
            _attribDefs.NrEntryPoint.TrySetValue(spanAttributes, true);

            //Add Transaction Cutom Attributes to Root Span
            foreach (var customAttrib in immutableTransaction.TransactionMetadata.UserAttributes)
            {
                _attribDefs.GetCustomAttributeForSpan(customAttrib.Key).TrySetValue(spanAttributes, customAttrib.Value);
            }

            spanAttributes.MakeImmutable();

            return spanAttributes;
        }

        private void SetAttributes(Segment segment, ImmutableTransaction immutableTransaction, string rootSpanId)
        {
            segment.AttribValues.AddRange(immutableTransaction.CommonSpanAttributes);

            _attribDefs.Guid.TrySetValue(segment.AttribValues, segment.SpanId);
            _attribDefs.Timestamp.TrySetValue(segment.AttribValues, immutableTransaction.StartTime.Add(segment.RelativeStartTime));

            segment.AttribValues.Priority = immutableTransaction.Priority;

            segment.SetAttributeValues();

            _attribDefs.ParentId.TrySetValue(segment.AttribValues, () => GetParentSpanId(segment, immutableTransaction, rootSpanId));
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
