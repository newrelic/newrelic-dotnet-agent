// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Utilities;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.TransactionTraces
{
    [TestFixture]
    public class SlowestTransactionCollectorTests
    {
        public SlowestTransactionCollector ObjectUnderTest;

        [SetUp]
        public void SetUp()
        {
            ObjectUnderTest = new SlowestTransactionCollector();

            // SlowestTransactionCollector has an uninjected ConfigurationObserver, so let's update the config! :)
            var configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.TransactionTraceThreshold).Returns(TimeSpan.FromSeconds(5));
            Mock.Arrange(() => configuration.ConfigurationVersion).Returns(500);
            EventBus<ConfigurationUpdatedEvent>.Publish(new ConfigurationUpdatedEvent(configuration, ConfigurationUpdateSource.Unknown));
        }

        [TearDown]
        public void TearDown()
        {
            ObjectUnderTest.Dispose();
        }

        [Test]
        public void DoesNotThrowWhenNullTransactionTraceIsCollected()
        {
            ObjectUnderTest.Collect(null);
            var samples = ObjectUnderTest.GetCollectedSamples().ToArray();
            Assert.That(samples, Is.Empty);
        }

        [Test]
        public void GettingCollectedSamplesWorksWhenEmpty()
        {
            var samples = ObjectUnderTest.GetCollectedSamples().ToArray();
            Assert.That(samples, Is.Empty);
        }

        [Test]
        public void TransactionTraceIsCollectedWhenOverConfiguredThreshold()
        {
            var input = new TransactionTraceWireModelComponents(new TransactionMetricName("bleep", "bloop"), TimeSpan.FromSeconds(10), false, null);

            ObjectUnderTest.Collect(input);
            var samples = ObjectUnderTest.GetCollectedSamples().ToArray();

            Assert.That(samples, Has.Length.EqualTo(1));
            Assert.That(samples[0], Is.SameAs(input));
        }

        [Test]
        public void GettingCollectedSamplesClearsStorage()
        {
            ObjectUnderTest.Collect(new TransactionTraceWireModelComponents(new TransactionMetricName("bleep", "bloop"), TimeSpan.FromSeconds(10), false, null));

            var firstHarvest = ObjectUnderTest.GetCollectedSamples().ToArray();
            var secondHarvest = ObjectUnderTest.GetCollectedSamples().ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(firstHarvest, Has.Length.EqualTo(1));
                Assert.That(secondHarvest, Is.Empty);
            });
        }

        [Test]
        public void TransactionTraceIsNotCollectedWhenUnderConfiguredThreshold()
        {
            var input = new TransactionTraceWireModelComponents(new TransactionMetricName("bleep", "bloop"), TimeSpan.FromSeconds(1), false, null);

            ObjectUnderTest.Collect(input);
            var samples = ObjectUnderTest.GetCollectedSamples().ToArray();

            Assert.That(samples, Is.Empty);
        }

        [Test]
        public void SlowestTransactionTraceIsCollected()
        {
            var slowTransactionTrace = new TransactionTraceWireModelComponents(new TransactionMetricName("bleep", "bloop"), TimeSpan.FromSeconds(10), false, null);
            var slowestTransactionTrace = new TransactionTraceWireModelComponents(new TransactionMetricName("bleep", "bloop"), TimeSpan.FromSeconds(15), false, null);

            ObjectUnderTest.Collect(slowTransactionTrace);
            ObjectUnderTest.Collect(slowestTransactionTrace);

            var samples = ObjectUnderTest.GetCollectedSamples().ToArray();

            Assert.That(samples, Has.Length.EqualTo(1));
            Assert.That(samples[0], Is.SameAs(slowestTransactionTrace));

            // Now do it in reverse order
            ObjectUnderTest.Collect(slowestTransactionTrace);
            ObjectUnderTest.Collect(slowTransactionTrace);

            samples = ObjectUnderTest.GetCollectedSamples().ToArray();

            Assert.That(samples, Has.Length.EqualTo(1));
            Assert.That(samples[0], Is.SameAs(slowestTransactionTrace));
        }

        [Test]
        public void FirstSlowTransactionTraceIsCollected()
        {
            var firstTransactionTrace = new TransactionTraceWireModelComponents(new TransactionMetricName("bleep", "bloop"), TimeSpan.FromSeconds(10), false, null);
            var secondTransactionTrace = new TransactionTraceWireModelComponents(new TransactionMetricName("bleep", "bloop"), TimeSpan.FromSeconds(10), false, null);

            ObjectUnderTest.Collect(firstTransactionTrace);
            ObjectUnderTest.Collect(secondTransactionTrace);

            var samples = ObjectUnderTest.GetCollectedSamples().ToArray();

            Assert.That(samples, Has.Length.EqualTo(1));
            Assert.That(samples[0], Is.SameAs(firstTransactionTrace));
        }

        [Test]
        public void SlowestTransactionTraceIsCollectedInThreadedScenario()
        {
            // This is testing for a race condition, so try it 100 times just to make sure! :)
            for (var tries = 0; tries < 100; tries++)
            {
                var transactionTraces = new List<TransactionTraceWireModelComponents>();
                for (var i = 0; i < 1000; i++)
                {
                    transactionTraces.Add(new TransactionTraceWireModelComponents(new TransactionMetricName("bleep", "bloop"), TimeSpan.FromSeconds(10 + i), false, null));
                }

                var collectionActions = new List<Action>();
                foreach (var transactionTrace in transactionTraces)
                {
                    collectionActions.Add(() => ObjectUnderTest.Collect(transactionTrace));
                }

                Parallel.Invoke(collectionActions.ToArray());

                var samples = ObjectUnderTest.GetCollectedSamples().ToArray();

                Assert.That(samples, Has.Length.EqualTo(1));
                Assert.That(samples[0].Duration, Is.EqualTo(transactionTraces.Last().Duration));
            }
        }
    }
}
