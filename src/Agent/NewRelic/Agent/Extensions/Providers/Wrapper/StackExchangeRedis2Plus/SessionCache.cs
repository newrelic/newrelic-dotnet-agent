// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Threading;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Helpers;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using StackExchange.Redis.Profiling;

namespace NewRelic.Providers.Wrapper.StackExchangeRedis2Plus
{
    public class SessionCache : IStackExchangeRedisCache
    {
        private readonly EventWaitHandle _stopHandle = new EventWaitHandle(false, EventResetMode.ManualReset);

        private readonly ConcurrentDictionary<string, ProfilingSession> _sessionCache = new ConcurrentDictionary<string, ProfilingSession>();

        private readonly IAgent _agent;

        private readonly ConnectionInfo _connectionInfo;

        private readonly int _invocationTargetHashCode;

        public SessionCache(IAgent agent, ConnectionInfo connectionInfo, int invocationTargetHashCode)
        {
            _agent = agent;
            _connectionInfo = connectionInfo;

            // Since the methodcall will not change, it is passed in from the instrumentation for reuse later.
            _invocationTargetHashCode = invocationTargetHashCode;
        }

        /// <summary>
        /// Finishes a profiling session for the segment indicated by the span id and creates a child DataStoreSegment for each command in the session.
        /// </summary>
        /// <param name="spanId">Span ID of the segment being finalized.</param>
        /// <param name="transaction">The currently active transaction for the given context.</param>
        public void Harvest(string spanId, Agent.Api.ITransaction transaction)
        {
            // If we can't remove the session, it doesn't exist, so do nothing and return.
            if (!_sessionCache.TryRemove(spanId, out var session))
            {
                return;
            }

            var commands = session.FinishProfiling();

            var xTransaction = (ITransactionExperimental)transaction;
            var startTime = xTransaction.StartTime;
            foreach (var command in commands)
            {
                // We need to build the relative start and stop time based on the transaction start time.
                var relativeStartTime = command.CommandCreated - startTime;
                var relativeEndTime = relativeStartTime + command.ElapsedTime;
                var operation = command.Command;

                // This new segment maker accepts relative start and stop times since we will be starting and ending(RemoveSegmentFromCallStack) the segment immediately.
                // This also sets the segment as a Leaf.
                var segment = xTransaction.StartStackExchangeRedisSegment(_invocationTargetHashCode, ParsedSqlStatement.FromOperation(DatastoreVendor.Redis, operation),
                    _connectionInfo, relativeStartTime, relativeEndTime);

                // This version of End does not set the end time or check for redis Harvests
                // This calls Finish and removes the segment from the callstack.
                segment.EndStackExchangeRedis();
            }
        }

        /// <summary>
        /// On-demand ambient session provider based on the calling context.  Context is a segment in a transaction.
        /// </summary>
        /// <returns></returns>
        public Func<ProfilingSession> GetProfilingSession()
        {
            return () =>
            {
                if (_stopHandle.WaitOne(0))
                {
                    return null;
                }

                var transaction = _agent.CurrentTransaction;

                // Don't want to save data to a session outside of a transaction - no way to clean it up easily or reliably.
                if (!transaction.IsValid)
                {
                    return null;
                }

                // Use the spanid of the segment as the key for the cache.
                var segment = transaction.CurrentSegment;
                var spanId = segment.SpanId;
                if (!_sessionCache.TryGetValue(spanId, out var session))
                {
                    session = new ProfilingSession(segment);
                    _sessionCache.TryAdd(spanId, session);
                }

                return session;
            };
        }

        public void Dispose()
        {
            this._stopHandle.Set();
            this._stopHandle.Dispose();
        }
    }
}
