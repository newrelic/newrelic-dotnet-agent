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
        private const string SessionCacheCleanupSupportabilityMetricName = "Supportability/Dotnet/RedisSessionCacheCleanup/Count";

        private readonly EventWaitHandle _stopHandle = new EventWaitHandle(false, EventResetMode.ManualReset);

        private readonly ConcurrentDictionary<ISegment, (WeakReference<ITransaction> transaction, ProfilingSession session)> _sessionCache = new ConcurrentDictionary<ISegment, (WeakReference<ITransaction> transaction, ProfilingSession session)>();

        private readonly IAgent _agent;

        private readonly int _invocationTargetHashCode;

        public SessionCache(IAgent agent, int invocationTargetHashCode)
        {
            _agent = agent;

            // Since the methodcall will not change, it is passed in from the instrumentation for reuse later.
            _invocationTargetHashCode = invocationTargetHashCode;

            _agent.SimpleSchedulingService.StartExecuteEvery(CleanUp, _agent.Configuration.StackExchangeRedisCleanupCycle, _agent.Configuration.StackExchangeRedisCleanupCycle);
        }

        /// <summary>
        /// Finishes a profiling session for the segment and creates a child DataStoreSegment for each command in the session.
        /// </summary>
        /// <param name="hostSegment">Segment being finalized.</param>
        public void Harvest(ISegment hostSegment)
        {
            (WeakReference<ITransaction> transaction, ProfilingSession session) sessionData;
            lock (hostSegment)
            {
                // If we can't remove the session, it doesn't exist, so do nothing and return.
                if (!_sessionCache.TryRemove(hostSegment, out sessionData))
                {
                    return;
                }
            }
            
            // Get the transaction from the data.
            if (!(sessionData.transaction?.TryGetTarget(out var transaction) ?? false))
            {
                return;
            }

            var xTransaction = (ITransactionExperimental)transaction;
            var commands = sessionData.session.FinishProfiling();
            foreach (var command in commands)
            {
                // We need to build the relative start and stop time based on the transaction start time.
                var relativeStartTime = command.CommandCreated - xTransaction.StartTime;
                var relativeEndTime = relativeStartTime + command.ElapsedTime;

                // This new segment maker accepts relative start and stop times since we will be starting and ending(RemoveSegmentFromCallStack) the segment immediately.
                // This also sets the segment as a Leaf.
                var segment = xTransaction.StartStackExchangeRedisSegment(_invocationTargetHashCode,
                    ParsedSqlStatement.FromOperation(DatastoreVendor.Redis, command.Command),
                    GetConnectionInfo(command.EndPoint), relativeStartTime, relativeEndTime);

                // This version of End does not set the end time or check for redis Harvests
                // This calls Finish and removes the segment from the callstack.
                segment.EndStackExchangeRedis();
            }
        }

        private void CleanUp()
        {
            var cleanedSessions = 0;

            try
            {
                foreach (var pair in _sessionCache)
                {
                    // This can happen outside the lock since the object transaction was garbage collected.
                    if (!(pair.Value.transaction?.TryGetTarget(out _) ?? false))
                    {
                        if (_sessionCache.TryRemove(pair.Key, out _))
                        {
                            cleanedSessions++;
                        }
                    }

                    lock (pair.Key)
                    {
                        if (((ISegmentExperimental)pair.Key).IsDone)
                        {
                            if (_sessionCache.TryRemove(pair.Key, out _))
                            {
                                cleanedSessions++;
                            }
                        }
                    }
                }
            }
            catch { } // Don't want to log here, just want to prevent collection problems from breaking things.

            _agent.RecordSupportabilityMetric(SessionCacheCleanupSupportabilityMetricName, cleanedSessions);
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
                if (transaction.IsFinished || !transaction.IsValid)
                {
                    return null;
                }

                // Don't want to save data to a session to a NoOp - no way to clean it up easily or reliably.
                // Don't want to save to a Datastore segment - could be another Redis segment or something else.
                var segment = transaction.CurrentSegment;

                // These don't change over time so they don't need to be in the lock.
                if (!segment.IsValid || ((ISegmentExperimental)segment).GetCategory() == "Datastore")
                {
                    return null;
                }

                ProfilingSession session = null;
                lock (segment)
                {
                    if (!((ISegmentExperimental)segment).IsDone)
                    {
                        var sessiontoken = _sessionCache.GetOrAdd(segment, (s) => (new WeakReference<ITransaction>(transaction), new ProfilingSession()));
                        session = sessiontoken.session;
                    }
                }

                return session;
            };
        }

        // Clean up the handles, sessions, and wipe the dictionary.
        public void Dispose()
        {
            _stopHandle.Set();
            _agent.SimpleSchedulingService.StopExecuting(CleanUp);
            _sessionCache.Clear();
            _stopHandle.Dispose();
        }
    }
}
