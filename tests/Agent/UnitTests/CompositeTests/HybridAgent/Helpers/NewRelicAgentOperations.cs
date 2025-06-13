// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

//using NewRelic.Api.Agent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NUnit.Framework;

namespace CompositeTests.HybridAgent.Helpers;

public class NewRelicAgentOperations
{
    private IAgent _agent;

    public NewRelicAgentOperations(IAgent agent)
    {
        _agent = agent;
    }

    public void DoWorkInTransaction(string transactionName, Action work)
    {
        var transaction = _agent.CreateTransaction(isWeb:false, "Custom", transactionName, doNotTrackAsUnitOfWork: true);

        try
        {
            work();
        }
        finally
        {
            transaction.End();
        }
    }

    public void DoWorkInSegment(string segmentName, Action work)
    {
        var segment = _agent.StartCustomSegmentOrThrow(segmentName);
        segment.SetName(segmentName);

        try
        {
            work();
        }
        finally
        {
            segment.End();
        }
    }

    public void AssertNotValidTransaction()
    {
        Assert.That( _agent.CurrentTransaction.IsValid, Is.False, "Transaction found when none was expected.");
    }

    public object GetCurrentTraceId()
    {
        return _agent.TraceMetadata.TraceId;
    }

    public object GetCurrentSpanId()
    {
        return _agent.TraceMetadata.SpanId;
    }

    public bool GetCurrentIsSampledFlag()
    {
        return _agent.TraceMetadata.IsSampled;
    }

    public void InjectHeaders(Action work)
    {
        var externalCall = SimulatedOperations.GetCurrentExternalCall()!;

        var transactionApi = _agent.CurrentTransaction;

        transactionApi.InsertDistributedTraceHeaders(externalCall, (call, headerName, headerValue) => call.Headers[headerName] = headerValue);

        work();
    }
}
