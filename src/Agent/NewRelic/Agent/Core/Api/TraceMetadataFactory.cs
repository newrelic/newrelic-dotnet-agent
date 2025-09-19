// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.DistributedTracing.Samplers;
using NewRelic.Agent.Core.Transactions;

namespace NewRelic.Agent.Core.Api
{
    public class TraceMetadata : ITraceMetadata
    {
        public static readonly ITraceMetadata EmptyModel = new TraceMetadata(string.Empty, string.Empty, false);

        public string TraceId { get; private set; }

        public string SpanId { get; private set; }

        public bool IsSampled { get; private set; }

        public TraceMetadata(string traceId, string spanId, bool isSampled)
        {
            TraceId = traceId;
            SpanId = spanId;
            IsSampled = isSampled;
        }
    }

    public interface ITraceMetadataFactory
    {
        ITraceMetadata CreateTraceMetadata(IInternalTransaction transaction);
    }

    public class TraceMetadataFactory : ITraceMetadataFactory
    {
        private readonly ISamplerService _samplerService;

        public TraceMetadataFactory(ISamplerService samplerService)
        {
            _samplerService = samplerService;
        }

        public ITraceMetadata CreateTraceMetadata(IInternalTransaction transaction)
        {
            var traceId = transaction.TraceId;
            var spanId = transaction.CurrentSegment.SpanId;
            var isSampled = SetIsSampled(transaction);

            return new TraceMetadata(traceId, spanId, isSampled);
        }

        private bool SetIsSampled(IInternalTransaction transaction)
        {
            // if Sampled has not been set, compute it now
            if (transaction.Sampled != null)
            {
                return (bool)transaction.Sampled;
            }
            else
            {
                transaction.SetSampled(_samplerService.GetSampler(SamplerLevel.Root));
                return (bool)transaction.Sampled;
            }
        }
    }
}
