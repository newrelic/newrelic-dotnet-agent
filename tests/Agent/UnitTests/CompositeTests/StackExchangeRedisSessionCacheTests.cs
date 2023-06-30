// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Providers.Wrapper.StackExchangeRedis2Plus;
using NUnit.Framework;
using StackExchange.Redis.Profiling;

namespace CompositeTests
{
    [TestFixture]
    public class StackExchangeRedisSessionCacheTests
    {
        private static CompositeTestAgent _compositeTestAgent;

        private IConfigurationService _configSvc;

        private static readonly string _accountId = "acctid";
        private static readonly string _appId = "appid";
        private static readonly string _trustKey = "trustedkey";

        [SetUp]
        public void SetUp()
        {
            _compositeTestAgent = new CompositeTestAgent();
            _compositeTestAgent.ServerConfiguration.AccountId = _accountId;
            _compositeTestAgent.ServerConfiguration.TrustedAccountKey = _trustKey;
            _compositeTestAgent.ServerConfiguration.PrimaryApplicationId = _appId;
            var cleanupOverride = new NewRelic.Agent.Core.Config.configurationAdd
            {
                key = "OverrideStackExchangeRedisCleanupCycle",
                value = "2"
            };
            _compositeTestAgent.LocalConfiguration.appSettings.Add(cleanupOverride);
            _configSvc = _compositeTestAgent.Container.Resolve<IConfigurationService>();
        }

        [TearDown]
        public static void TearDown()
        {
            _compositeTestAgent.Dispose();
        }

        #region Harvest Tests

        [Test]
        public void Harvest_SegmentInCache_IsRemoved()
        {
            var agent = _compositeTestAgent.GetAgent();
            var sessionCache = new SessionCache(agent, 0);

            var transaction = _compositeTestAgent.GetAgent().CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.ASP),
                transactionDisplayName: "TransactionName",
                doNotTrackAsUnitOfWork: true);

            var segment = _compositeTestAgent.GetAgent().StartTransactionSegmentOrThrow("segment");

            var session = sessionCache.GetProfilingSession().Invoke();

            var cacheSizeBefore = GetSessionCacheSize(sessionCache);

            sessionCache.Harvest(segment);

            var cacheSizeAfter = GetSessionCacheSize(sessionCache);

            Assert.NotNull(session);
            Assert.True(cacheSizeBefore == 1);
            Assert.True(cacheSizeAfter == 0);
        }

        [Test]
        public void Harvest_SegmentNotInCache_DoesNothing()
        {
            var agent = _compositeTestAgent.GetAgent();
            var sessionCache = new SessionCache(agent, 0);

            var transaction = _compositeTestAgent.GetAgent().CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.ASP),
                transactionDisplayName: "TransactionName",
                doNotTrackAsUnitOfWork: true);

            var segment = _compositeTestAgent.GetAgent().StartTransactionSegmentOrThrow("segment");

            var session = sessionCache.GetProfilingSession().Invoke();

            var segmentTwo = _compositeTestAgent.GetAgent().StartTransactionSegmentOrThrow("segmentTwo");

            var cacheSizeBefore = GetSessionCacheSize(sessionCache);

            sessionCache.Harvest(segmentTwo);

            var cacheSizeAfter = GetSessionCacheSize(sessionCache);

            Assert.NotNull(session);
            Assert.True(cacheSizeBefore == 1);
            Assert.True(cacheSizeAfter == 1);
        }

        [Test]
        public void Harvest_SegmentInCache_IsRemoved_TransactionFinished_DoesNotThrow()
        {
            var agent = _compositeTestAgent.GetAgent();
            var sessionCache = new SessionCache(agent, 0);

            var transaction = _compositeTestAgent.GetAgent().CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.ASP),
                transactionDisplayName: "TransactionName",
                doNotTrackAsUnitOfWork: true);

            var segment = _compositeTestAgent.GetAgent().StartTransactionSegmentOrThrow("segment");

            var session = sessionCache.GetProfilingSession().Invoke();
            var cacheSizeBefore = GetSessionCacheSize(sessionCache);

            transaction.End();

            Assert.True(transaction.IsFinished);
            Assert.NotNull(session);
            Assert.True(cacheSizeBefore == 1);
            Assert.DoesNotThrow(() => sessionCache.Harvest(segment));

            var cacheSizeAfter = GetSessionCacheSize(sessionCache);
            Assert.True(cacheSizeAfter == 0);
        }

        #endregion

        #region Segments

        [Test]
        public void DoneSegment_NoProfilingSession()
        {
            var agent = _compositeTestAgent.GetAgent();
            var sessionCache = new SessionCache(agent, 0);

            var transaction = _compositeTestAgent.GetAgent().CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.ASP),
                transactionDisplayName: "TransactionName",
                doNotTrackAsUnitOfWork: true);

            var segment = _compositeTestAgent.GetAgent().StartTransactionSegmentOrThrow("segment");
            segment.End();

            var session = sessionCache.GetProfilingSession().Invoke();

            var cacheSize = GetSessionCacheSize(sessionCache);

            Assert.True(((ISegmentExperimental)segment).IsDone);
            Assert.Null(session);
            Assert.True(cacheSize == 0);
        }

        [Test]
        public void InvalidSegment_NoProfilingSession()
        {
            var agent = _compositeTestAgent.GetAgent();
            var sessionCache = new SessionCache(agent, 0);

            var transaction = _compositeTestAgent.GetAgent().CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.ASP),
                transactionDisplayName: "TransactionName",
                doNotTrackAsUnitOfWork: true);

            var segment = new NewRelic.Agent.Core.Segments.NoOpSegment();

            var session = sessionCache.GetProfilingSession().Invoke();

            var cacheSize = GetSessionCacheSize(sessionCache);

            Assert.False(segment.IsValid);
            Assert.Null(session);
            Assert.True(cacheSize == 0);
        }

        [Test]
        public void DatastoreSegment_NoProfilingSession()
        {
            var agent = _compositeTestAgent.GetAgent();
            var sessionCache = new SessionCache(agent, 0);

            var transaction = _compositeTestAgent.GetAgent().CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.ASP),
                transactionDisplayName: "TransactionName",
                doNotTrackAsUnitOfWork: true);

            var segment = _compositeTestAgent.GetAgent().StartDatastoreRequestSegmentOrThrow("select", DatastoreVendor.MSSQL, "model", "segment");

            var session = sessionCache.GetProfilingSession().Invoke();

            var cacheSize = GetSessionCacheSize(sessionCache);

            Assert.Null(session);
            Assert.True(cacheSize == 0);
        }

        [Test]
        public void ActiveSegment_NotCleaned()
        {
            var agent = _compositeTestAgent.GetAgent();
            var sessionCache = new SessionCache(agent, 0);

            var transaction = _compositeTestAgent.GetAgent().CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.ASP),
                transactionDisplayName: "TransactionName",
                doNotTrackAsUnitOfWork: true);

            var segment = _compositeTestAgent.GetAgent().StartTransactionSegmentOrThrow("segment");

            _ = sessionCache.GetProfilingSession().Invoke();

            var cleanupCount = WaitForMetric();
            var cacheSize = GetSessionCacheSize(sessionCache);

            Assert.False(((ISegmentExperimental)segment).IsDone);
            Assert.True(cleanupCount != null);
            Assert.True(cacheSize == 1);
        }

        [Test]
        public void OrphanedSegment_IsCleaned()
        {
            var agent = _compositeTestAgent.GetAgent();
            var sessionCache = new SessionCache(agent, 0);

            var transaction = _compositeTestAgent.GetAgent().CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.ASP),
                transactionDisplayName: "TransactionName",
                doNotTrackAsUnitOfWork: true);

            var segment = _compositeTestAgent.GetAgent().StartTransactionSegmentOrThrow("segment");

            _ = sessionCache.GetProfilingSession().Invoke();

            // Since the session cache is not set in the agent, it will not attempt to harvest the session.
            segment.End();

            var cleanupCount = WaitForMetric();
            var cacheSize = GetSessionCacheSize(sessionCache);

            Assert.True(cleanupCount != null);
            Assert.True(cacheSize == 0);
        }

        #endregion

        #region Transactions

        [Test]
        public void FinishedTransaction_NoProfilingSession()
        {
            var agent = _compositeTestAgent.GetAgent();
            var sessionCache = new SessionCache(agent, 0);

            var transaction = _compositeTestAgent.GetAgent().CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.ASP),
                transactionDisplayName: "TransactionName",
                doNotTrackAsUnitOfWork: true);

            var segment = _compositeTestAgent.GetAgent().StartTransactionSegmentOrThrow("segment");
            segment.End();
            transaction.End();

            var session = sessionCache.GetProfilingSession().Invoke();

            var cacheSize = GetSessionCacheSize(sessionCache);

            Assert.True(transaction.IsFinished);
            Assert.Null(session);
            Assert.True(cacheSize == 0);
        }

        [Test]
        public void InvalidTransaction_NoProfilingSession()
        {
            var agent = _compositeTestAgent.GetAgent();
            var sessionCache = new SessionCache(agent, 0);

            var transaction = new NewRelic.Agent.Core.Transactions.NoOpTransaction(); // Not finished and not valid

            var session = sessionCache.GetProfilingSession().Invoke();

            var cacheSize = GetSessionCacheSize(sessionCache);

            Assert.False(transaction.IsValid);
            Assert.Null(session);
            Assert.True(cacheSize == 0);
        }

        [Test]
        public void ActiveTransaction_NotCleaned()
        {
            var agent = _compositeTestAgent.GetAgent();
            var sessionCache = new SessionCache(agent, 0);

            var transaction = _compositeTestAgent.GetAgent().CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.ASP),
                transactionDisplayName: "TransactionName",
                doNotTrackAsUnitOfWork: true);

            var segment = _compositeTestAgent.GetAgent().StartTransactionSegmentOrThrow("segment");

            _ = sessionCache.GetProfilingSession().Invoke();

            var cleanupCount = WaitForMetric();
            var cacheSize = GetSessionCacheSize(sessionCache);

            Assert.False(transaction.IsFinished);
            Assert.True(cleanupCount != null);
            Assert.True(cacheSize == 1);
        }

        #endregion

        [Test]
        public void Cleanup_IsScheduled()
        {
            var agent = _compositeTestAgent.GetAgent();
            var sessionCache = new SessionCache(agent, 0);

            var sssFieldType = typeof(SimpleSchedulingService).GetField("_executingActions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var value = sssFieldType.GetValue(agent.SimpleSchedulingService) as List<Action>;

            var cleanupAction = value.FirstOrDefault(a => a.Method.Name == "CleanUp");

            Assert.NotNull(cleanupAction);

            sessionCache.Dispose();

            var noAction = value.FirstOrDefault(a => a.Method.Name == "CleanUp");

            Assert.Null(noAction);
        }

        private int GetSessionCacheSize(SessionCache sessionCache)
        {
            var cacheFieldType = typeof(SessionCache).GetField("_sessionCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var value = cacheFieldType.GetValue(sessionCache) as ConcurrentDictionary<ISegment, (WeakReference<ITransaction> transaction, ProfilingSession session)>;

            return value.Count;
        }

        private MetricWireModel WaitForMetric()
        {
            // Each look lasts 100 ms, want to want at most 3000ms
            const int maxLoops = 30;
            var loops = 0;
            _compositeTestAgent.Harvest();
            var cleanupCount = _compositeTestAgent.Metrics.FirstOrDefault(m => m.MetricName.Name == "Supportability/Dotnet/RedisSessionCacheCleanup/Count");
            while (cleanupCount == null && loops < maxLoops)
            {
                _compositeTestAgent.Harvest();
                cleanupCount = _compositeTestAgent.Metrics.FirstOrDefault(m => m.MetricName.Name == "Supportability/Dotnet/RedisSessionCacheCleanup/Count");
                loops++;
                Thread.Sleep(100);
            }

            return cleanupCount;
        }
    }
}
