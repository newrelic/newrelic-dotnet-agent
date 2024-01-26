// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.TestUtilities;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading;

namespace CompositeTests
{
    [TestFixture]
    public class AsyncLocalStorageContextTests
    {
        private static CompositeTestAgent _compositeTestAgent;
        private IAgent _agent;

        [SetUp]
        public void SetUp()
        {
            _compositeTestAgent = new CompositeTestAgent(false, true);
            _agent = _compositeTestAgent.GetAgent();
        }

        [TearDown]
        public static void TearDown()
        {
            _compositeTestAgent.Dispose();
        }

        [Test]
        public void SimpleTransaction_EndedMultipleTimes()
        {
            _compositeTestAgent.LocalConfiguration.service.completeTransactionsOnThread = true;
            _compositeTestAgent.PushConfiguration();

            var doBackgroundJob = new AutoResetEvent(false);
            var completedForegroundExternal = new AutoResetEvent(false);
            var completedBackgroundExternal = new AutoResetEvent(false);

            bool? transactionFlowedToBackgroundThread = null;

            InstrumentationThatStartsATransaction();
            HttpClientInstrumentation("foregroundExternal", completedForegroundExternal);
            System.Threading.Tasks.Task.Run((Action)BackgroundJob);

            completedForegroundExternal.WaitOne();

            InstrumentationThatEndsTheTransaction();

            doBackgroundJob.Set();

            completedBackgroundExternal.WaitOne();
            _compositeTestAgent.Harvest();

            var transactionEvents = _compositeTestAgent.TransactionEvents;
            var metrics = _compositeTestAgent.Metrics;
            var errors = _compositeTestAgent.ErrorEvents;

            Assert.Multiple(() =>
            {
                Assert.That(transactionFlowedToBackgroundThread, Is.EqualTo(true));
                Assert.That(transactionEvents, Has.Count.EqualTo(1));
            });
            Assert.Multiple(() =>
            {
                Assert.That(transactionEvents.First().AgentAttributes()["request.uri"], Is.EqualTo("foregroundExternal"));
                Assert.That(errors, Is.Empty);
                Assert.That(metrics.Where(x => x.MetricNameModel.Name.Contains("backgroundExternal")), Is.Empty);
                Assert.That(metrics.Where(x => x.MetricNameModel.Name.Contains("foregroundExternal")), Is.Not.Empty);
            });

            void InstrumentationThatStartsATransaction()
            {
                _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
                _agent.CurrentTransaction.AttachToAsync();
                _agent.CurrentTransaction.DetachFromPrimary();
            }

            void InstrumentationThatEndsTheTransaction()
            {
                _agent.CurrentTransaction.End();
            }

            void BackgroundJob()
            {
                transactionFlowedToBackgroundThread = _agent.CurrentTransaction.IsValid;
                doBackgroundJob.WaitOne();
                HttpClientInstrumentation("backgroundExternal", completedBackgroundExternal);
                _agent.CurrentTransaction.NoticeError(new Exception());
            };

            System.Threading.Tasks.Task HttpClientInstrumentation(string segmentName, AutoResetEvent autoResetEvent)
            {
                var transactionWrapperApi = _agent.CurrentTransaction;

                var segment = _agent.StartTransactionSegmentOrThrow(segmentName);

                _agent.CurrentTransaction.SetUri(segmentName);

                segment.RemoveSegmentFromCallStack();
                transactionWrapperApi.Hold();

                return System.Threading.Tasks.Task.Delay(1000).ContinueWith(task =>
                {
                    segment.End();
                    transactionWrapperApi.Release();
                    autoResetEvent.Set();
                });
            };
        }
    }
}
