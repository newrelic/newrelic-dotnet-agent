// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Helpers;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Parsing.ConnectionString;
using StackExchange.Redis.Profiling;

namespace NewRelic.Providers.Wrapper.StackExchangeRedis2Plus
{
    public class SessionCache : IStackExchangeRedisCache
    {
        private readonly EventWaitHandle _stopHandle = new EventWaitHandle(false, EventResetMode.ManualReset);

        private readonly ConcurrentDictionary<string, ProfilingSession> _sessionCache = new ConcurrentDictionary<string, ProfilingSession>();

        private readonly IAgent _agent;

        private readonly int _invocationTargetHashCode;

        public SessionCache(IAgent agent, int invocationTargetHashCode)
        {
            _agent = agent;

            // Since the methodcall will not change, it is passed in from the instrumentation for reuse later.
            _invocationTargetHashCode = invocationTargetHashCode;
        }

        /// <summary>
        /// Finishes a profiling session for the segment indicated by the span id and creates a child DataStoreSegment for each command in the session.
        /// </summary>
        /// <param name="spanId">Span ID of the segment being finalized.</param>
        public void Harvest(string spanId)
        {
            // If we can't remove the session, it doesn't exist, so do nothing and return.
            if (!_sessionCache.TryRemove(spanId, out var sessionData))
            {
                return;
            }

            // Get the transaction from the session
            var transaction = sessionData.UserToken as ITransaction;

            // We want to make sure to finish the session even if the transaction is done so that it is not orphaned.
            var commands = sessionData.FinishProfiling();
            if (transaction.IsFinished)
            {
                return;
            }

            var xTransaction = (ITransactionExperimental)transaction;
            foreach (var command in commands)
            {
                // We need to build the relative start and stop time based on the transaction start time.
                var relativeStartTime = command.CommandCreated - xTransaction.StartTime;
                var relativeEndTime = relativeStartTime + command.ElapsedTime;

                // This new segment maker accepts relative start and stop times since we will be starting and ending(RemoveSegmentFromCallStack) the segment immediately.
                // This also sets the segment as a Leaf.
                var segment = xTransaction.StartStackExchangeRedisSegment(_invocationTargetHashCode, ParsedSqlStatement.FromOperation(DatastoreVendor.Redis, command.Command),
                    GetConnectionInfo(command.EndPoint), relativeStartTime, relativeEndTime);

                // This version of End does not set the end time or check for redis Harvests
                // This calls Finish and removes the segment from the callstack.
                segment.EndStackExchangeRedis();
            }
        }

        private ConnectionInfo GetConnectionInfo(EndPoint endpoint)
        {
            if (endpoint is DnsEndPoint dnsEndpoint)
            {
                var port = dnsEndpoint.Port.ToString();
                var host = ConnectionStringParserHelper.NormalizeHostname(dnsEndpoint.Host, _agent.Configuration.UtilizationHostName);
                return new ConnectionInfo(host, port, null);
            }

            if (endpoint is IPEndPoint ipEndpoint)
            {
                var port = ipEndpoint.Port.ToString();
                var host = ConnectionStringParserHelper.NormalizeHostname(ipEndpoint.Address.ToString(), _agent.Configuration.UtilizationHostName);
                return new ConnectionInfo(host, port, null);
            }

            return null;
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

                // Don't want to save data to a session outside of a transaction or to a NoOp - no way to clean it up easily or reliably.
                var transaction = _agent.CurrentTransaction;
                if (!transaction.IsValid)
                {
                    return null;
                }

                // Don't want to save data to a session to a NoOp - no way to clean it up easily or reliably.
                var segment = transaction.CurrentSegment;
                if (!segment.IsValid)
                {
                    return null;
                }

                // Use the spanid of the segment as the key for the cache.
                var spanId = segment.SpanId;
                if (string.IsNullOrWhiteSpace(spanId))
                {
                    return null;
                }

                // During async operations, the transaction can get lost and report as NoOp so we store a reference to it in the session.
                if (!_sessionCache.TryGetValue(spanId, out var sessionData))
                {
                    sessionData = new ProfilingSession(transaction);
                    _sessionCache.TryAdd(spanId, sessionData);
                }

                return sessionData;
            };
        }

        public int Count
        {
            get
            {
                return _sessionCache.Count;
            }
        }

        // Clean up the handles, sessions, and wipe the dictionary.
        public void Dispose()
        {
            _stopHandle.Set();
            _stopHandle.Dispose();
            foreach (var cachedSession in _sessionCache.Values)
            {
                _ = cachedSession.FinishProfiling();
            }

            _sessionCache.Clear();
        }
    }
}
