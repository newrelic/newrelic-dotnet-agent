// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Transactions;
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
    public class SyntheticsTransactionCollectorTests
    {
        public SyntheticsTransactionCollector ObjectUnderTest;

        [SetUp]
        public void SetUp()
        {
            ObjectUnderTest = new SyntheticsTransactionCollector();
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
        public void TransactionTraceIsCollectedWhenItIsSynthetic()
        {
            var input = new TransactionTraceWireModelComponents(new TransactionMetricName("bleep", "bloop"), TimeSpan.FromSeconds(1), true, null);

            ObjectUnderTest.Collect(input);
            var samples = ObjectUnderTest.GetCollectedSamples().ToArray();

            Assert.That(samples, Has.Length.EqualTo(1));
            Assert.That(samples[0], Is.SameAs(input));
        }

        [Test]
        public void GettingCollectedSamplesClearsStorage()
        {
            ObjectUnderTest.Collect(new TransactionTraceWireModelComponents(new TransactionMetricName("bleep", "bloop"), TimeSpan.FromSeconds(1), true, null));

            var firstHarvest = ObjectUnderTest.GetCollectedSamples().ToArray();
            var secondHarvest = ObjectUnderTest.GetCollectedSamples().ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(firstHarvest, Has.Length.EqualTo(1));
                Assert.That(secondHarvest, Is.Empty);
            });
        }

        [Test]
        public void TransactionTraceIsNotCollectedWhenItIsNotSynthetic()
        {
            var input = new TransactionTraceWireModelComponents(new TransactionMetricName("bleep", "bloop"), TimeSpan.FromSeconds(1), false, null);

            ObjectUnderTest.Collect(input);
            var samples = ObjectUnderTest.GetCollectedSamples().ToArray();

            Assert.That(samples, Is.Empty);
        }

        [Test]
        public void MultipleSyntheticTransactionTracesAreCollected()
        {
            var firstSyntheticTrace = new TransactionTraceWireModelComponents(new TransactionMetricName("bleep", "bloop"), TimeSpan.FromSeconds(1), true, null);
            var secondSyntheticTrace = new TransactionTraceWireModelComponents(new TransactionMetricName("bleep", "bloop"), TimeSpan.FromSeconds(1), true, null);

            ObjectUnderTest.Collect(firstSyntheticTrace);
            ObjectUnderTest.Collect(secondSyntheticTrace);

            var samples = ObjectUnderTest.GetCollectedSamples().ToArray();

            Assert.That(samples, Has.Length.EqualTo(2));
            Assert.That(samples, Does.Contain(firstSyntheticTrace));
            Assert.That(samples, Does.Contain(secondSyntheticTrace));
        }

        [Test]
        public void OnlyGlobalMaxSyntheticTransactionTracesAreCollected()
        {
            // NOTE: The object under test currently looks at the global constant SyntheticsHeader.MaxTraceCount
            for (var i = 0; i < SyntheticsHeader.MaxTraceCount + 1; i++)
            {
                ObjectUnderTest.Collect(new TransactionTraceWireModelComponents(new TransactionMetricName("bleep", "bloop"), TimeSpan.FromSeconds(1), true, null));
            }

            var samples = ObjectUnderTest.GetCollectedSamples().ToArray();

            Assert.That(samples, Has.Length.EqualTo(SyntheticsHeader.MaxTraceCount));
        }

        [Test]
        public void OnlyGlobalMaxSyntheticTransactionTracesAreCollectedInThreadedScenario()
        {
            // This is testing for a race condition, so try it 100 times just to make sure! :)
            for (var tries = 0; tries < 100; tries++)
            {
                var transactionTraces = new List<TransactionTraceWireModelComponents>();
                for (var i = 0; i < 1000; i++)
                {
                    transactionTraces.Add(new TransactionTraceWireModelComponents(new TransactionMetricName("bleep", "bloop"), TimeSpan.FromSeconds(1), true, null));
                }

                var collectionActions = new List<Action>();
                foreach (var transactionTrace in transactionTraces)
                {
                    collectionActions.Add(() => ObjectUnderTest.Collect(transactionTrace));
                }

                Parallel.Invoke(collectionActions.ToArray());

                var samples = ObjectUnderTest.GetCollectedSamples().ToArray();

                Assert.That(samples, Has.Length.EqualTo(SyntheticsHeader.MaxTraceCount));
            }
        }
    }
}
