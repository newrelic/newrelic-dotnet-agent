// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Utilities;

namespace NewRelic.Agent.Core.Spans;

public interface ISpanEventMaker
{
    IEnumerable<ISpanEventWireModel> GetSpanEvents(ImmutableTransaction immutableTransaction, string transactionName, IAttributeValueCollection transactionAttribValues);
}

public class SpanEventMaker : ISpanEventMaker
{
    private readonly IAttributeDefinitionService _attribDefSvc;
    private IAttributeDefinitions _attribDefs => _attribDefSvc?.AttributeDefs;
    private readonly IConfigurationService _configurationService;
    private readonly IAgentHealthReporter _agentHealthReporter;

    public SpanEventMaker(IAttributeDefinitionService attribDefSvc, IConfigurationService configurationService, IAgentHealthReporter agentHealthReporter)
    {
        _attribDefSvc = attribDefSvc;
        _configurationService = configurationService;
        _agentHealthReporter = agentHealthReporter;
    }

    public IEnumerable<ISpanEventWireModel> GetSpanEvents(ImmutableTransaction immutableTransaction, string transactionName, IAttributeValueCollection transactionAttribValues)
    {
        var rootSpanId = GuidGenerator.GenerateNewRelicGuid();

        yield return GenerateRootSpan(rootSpanId, immutableTransaction, transactionName, transactionAttribValues);

        foreach (var segment in immutableTransaction.Segments)
        {
            var segmentAttribValues = GetAttributeValues(segment, immutableTransaction, rootSpanId);

            segmentAttribValues.MakeImmutable();

            yield return segmentAttribValues;
        }
    }

    /// <summary>
    /// Creates a single root span, much like we do for Transaction Traces, since DT requires that there be only one parent-less span per txn (or at least the UI/Backend is expecting that). 
    /// </summary>
    private SpanAttributeValueCollection GenerateRootSpan(string rootSpanId, ImmutableTransaction immutableTransaction, string transactionName, IAttributeValueCollection transactionAttribValues)
    {
        var spanAttributes = new SpanAttributeValueCollection();

        spanAttributes.AddRange(transactionAttribValues.GetAttributeValues(AttributeClassification.AgentAttributes));
        
        _attribDefs.TransactionNameForSpan.TrySetValue(spanAttributes, transactionName);


        spanAttributes.Priority = immutableTransaction.Priority;

        spanAttributes.AddRange(immutableTransaction.CommonSpanAttributes);

        if (immutableTransaction.TracingState != null)
        {
            _attribDefs.ParentId.TrySetValue(spanAttributes, immutableTransaction.TracingState.ParentId ?? immutableTransaction.TracingState.Guid);
            _attribDefs.TrustedParentId.TrySetValue(spanAttributes, immutableTransaction.TracingState.Guid);
            _attribDefs.TracingVendors.TrySetValue(spanAttributes, immutableTransaction.TracingState.VendorStateEntries ?? null);
        }

        if (_configurationService.Configuration.ErrorCollectorEnabled && immutableTransaction.TransactionMetadata.ReadOnlyTransactionErrorState.HasError)
        {
            _attribDefs.SpanErrorClass.TrySetValue(spanAttributes, immutableTransaction.TransactionMetadata.ReadOnlyTransactionErrorState.ErrorData.ErrorTypeName);
            _attribDefs.SpanErrorMessage.TrySetValue(spanAttributes, immutableTransaction.TransactionMetadata.ReadOnlyTransactionErrorState.ErrorData.ErrorMessage);
            if (immutableTransaction.TransactionMetadata.ReadOnlyTransactionErrorState.ErrorData.IsExpected)
            {
                _attribDefs.SpanIsErrorExpected.TrySetValue(spanAttributes, true);
            }
        }

        _attribDefs.Guid.TrySetValue(spanAttributes, rootSpanId);
        _attribDefs.Timestamp.TrySetValue(spanAttributes, immutableTransaction.StartTime);
        _attribDefs.Duration.TrySetValue(spanAttributes, immutableTransaction.Duration);

        _attribDefs.NameForSpan.TrySetValue(spanAttributes, transactionName);

        _attribDefs.SpanCategory.TrySetValue(spanAttributes, SpanCategory.Generic);
        _attribDefs.NrEntryPoint.TrySetValue(spanAttributes, true);

        spanAttributes.AddRange(transactionAttribValues.GetAttributeValues(AttributeClassification.UserAttributes));

        spanAttributes.MakeImmutable();

        return spanAttributes;
    }

    private SpanAttributeValueCollection GetAttributeValues(Segment segment, ImmutableTransaction immutableTransaction, string rootSpanId)
    {
        var attribValues = segment.GetAttributeValues();

        attribValues.AddRange(immutableTransaction.CommonSpanAttributes);

        _attribDefs.Guid.TrySetValue(attribValues, segment.SpanId);
        _attribDefs.Timestamp.TrySetValue(attribValues, immutableTransaction.StartTime.Add(segment.RelativeStartTime));

        attribValues.Priority = immutableTransaction.Priority;

        _attribDefs.ParentId.TrySetValue(attribValues, GetParentSpanId(segment, immutableTransaction, rootSpanId));

        foreach (var link in segment.Links)
        {
            var attributes = link.Attributes;

            _attribDefs.GetTypeAttribute(TypeAttributeValue.SpanLink).TrySetDefault(attributes);
            attributes.TrySetValue(_attribDefs.TraceIdForSpanData, immutableTransaction.TraceId);
            attributes.TrySetValue(_attribDefs.SpanIdForSpanLink, segment.SpanId);
            attributes.TrySetValue(_attribDefs.LinkedTraceId, link.LinkedTraceId);
            attributes.TrySetValue(_attribDefs.LinkedSpanId, link.LinkedSpanId);
            _attribDefs.Timestamp.TrySetValue(attributes, immutableTransaction.StartTime.Add(segment.RelativeStartTime));

            var linkWireModel = new SpanLinkWireModel(attributes);
            attribValues.Span.Links.Add(linkWireModel);
        }

        foreach (var evt in segment.Events)
        {
            var attributes = evt.Attributes;

            _attribDefs.GetTypeAttribute(TypeAttributeValue.SpanEvent).TrySetDefault(attributes);
            _attribDefs.Timestamp.TrySetValue(attributes, evt.Timestamp);
            attributes.TrySetValue(_attribDefs.NameForSpan, evt.Name);
            attributes.TrySetValue(_attribDefs.TraceIdForSpanData, immutableTransaction.TraceId);
            attributes.TrySetValue(_attribDefs.SpanIdForSpanEvent, segment.SpanId);

            var eventWireModel = new SpanEventEventWireModel(attributes);
            attribValues.Span.Events.Add(eventWireModel);
        }

        return attribValues;
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
