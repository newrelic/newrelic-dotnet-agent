// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.CSharp.RuntimeBinder;

namespace NewRelic.Api.Agent;

internal class Agent : IAgent
{
    private static IAgent _noOpAgent = new NoOpAgent();
    private dynamic _wrappedAgent = _noOpAgent;

    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    internal void SetWrappedAgent(object agentBridge)
    {
        _wrappedAgent = agentBridge;
    }

    private static bool _isCurrentTransactionAvailable = true;
    public ITransaction CurrentTransaction
    {
        get
        {
            if (!_isCurrentTransactionAvailable) return _noOpAgent.CurrentTransaction;

            try
            {
                var wrappedTransaction = _wrappedAgent.CurrentTransaction;
                if (wrappedTransaction != null)
                {
                    return new Transaction(wrappedTransaction);
                }
            }
            catch (RuntimeBinderException)
            {
                _isCurrentTransactionAvailable = false;
            }

            return _noOpAgent.CurrentTransaction;
        }
    }


    private static bool _isCurrentSpanAvailable = true;
    public ISpan CurrentSpan
    {
        get
        {
            if (!_isCurrentSpanAvailable) return _noOpAgent.CurrentTransaction.CurrentSpan;

            try
            {
                var wrappedSpan = _wrappedAgent.CurrentTransaction.CurrentSpan;
                if (wrappedSpan != null)
                {
                    return new Span(wrappedSpan);
                }
            }
            catch (RuntimeBinderException)
            {
                _isCurrentSpanAvailable = false;
            }

            return _noOpAgent.CurrentTransaction.CurrentSpan;
        }
    }

    private static bool _isTraceMetadataAvailable = true;
    public ITraceMetadata TraceMetadata
    {
        get
        {
            if (!_isTraceMetadataAvailable) return _noOpAgent.TraceMetadata;

            try
            {
                var wrappedTraceMetadata = _wrappedAgent.TraceMetadata;
                if (wrappedTraceMetadata != null)
                {
                    return new TraceMetadata(wrappedTraceMetadata);
                }
            }
            catch (RuntimeBinderException)
            {
                _isTraceMetadataAvailable = false;
            }

            return _noOpAgent.TraceMetadata;
        }
    }

    private static bool _isGetLinkingMetadataAvailable = true;
    public Dictionary<string, string> GetLinkingMetadata()
    {
        if (!_isGetLinkingMetadataAvailable) return _noOpAgent.GetLinkingMetadata();

        try
        {
            var wrappedLinkingMetadata = _wrappedAgent.GetLinkingMetadata();
            if (wrappedLinkingMetadata != null)
            {
                return wrappedLinkingMetadata;
            }
        }
        catch (RuntimeBinderException)
        {
            _isGetLinkingMetadataAvailable = false;
        }

        return new Dictionary<string, string>();
    }
}
