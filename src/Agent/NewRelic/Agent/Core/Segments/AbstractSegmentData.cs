// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Spans;
using NewRelic.Agent.Core.Transactions;

namespace NewRelic.Agent.Core.Segments;

public abstract class AbstractSegmentData : ISegmentData
{
    protected ISegmentDataState _segmentState;

    protected IAttributeDefinitions AttribDefs => _segmentState.AttribDefs;

    public virtual SpanCategory SpanCategory => SpanCategory.Generic;

    public void AttachSegmentDataState(ISegmentDataState segmentState)
    {
        _segmentState = segmentState;
    }

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

    public virtual void SetSpanTypeSpecificAttributes(SpanAttributeValueCollection attribVals)
    {
        AttribDefs.SpanCategory.TrySetValue(attribVals, SpanCategory.Generic);
    }

    internal virtual void AddTransactionTraceParameters(IConfigurationService configurationService, Segment segment, IDictionary<string, object> segmentParameters, ImmutableTransaction immutableTransaction)
    {
    }
}
