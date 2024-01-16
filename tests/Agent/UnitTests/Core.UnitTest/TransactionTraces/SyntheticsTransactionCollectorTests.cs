// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Utilities;
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

        [Test]
        public void DoesNotThrowWhenNullTransactionTraceIsCollected()
        {
            ObjectUnderTest.Collect(null);
            var samples = ObjectUnderTest.GetCollectedSamples().ToArray();
            ClassicAssert.IsEmpty(samples);
        }

        [Test]
        public void GettingCollectedSamplesWorksWhenEmpty()
        {
            var samples = ObjectUnderTest.GetCollectedSamples().ToArray();
            ClassicAssert.IsEmpty(samples);
        }

        [Test]
        public void TransactionTraceIsCollectedWhenItIsSynthetic()
        {
            var input = new TransactionTraceWireModelComponents(new TransactionMetricName("bleep", "bloop"), TimeSpan.FromSeconds(1), true, null);

            ObjectUnderTest.Collect(input);
            var samples = ObjectUnderTest.GetCollectedSamples().ToArray();

            ClassicAssert.AreEqual(1, samples.Length);
            ClassicAssert.AreSame(input, samples[0]);
        }

        [Test]
        public void GettingCollectedSamplesClearsStorage()
        {
            ObjectUnderTest.Collect(new TransactionTraceWireModelComponents(new TransactionMetricName("bleep", "bloop"), TimeSpan.FromSeconds(1), true, null));

            var firstHarvest = ObjectUnderTest.GetCollectedSamples().ToArray();
            var secondHarvest = ObjectUnderTest.GetCollectedSamples().ToArray();

            ClassicAssert.AreEqual(1, firstHarvest.Length);
            ClassicAssert.IsEmpty(secondHarvest);
        }

        [Test]
        public void TransactionTraceIsNotCollectedWhenItIsNotSynthetic()
        {
            var input = new TransactionTraceWireModelComponents(new TransactionMetricName("bleep", "bloop"), TimeSpan.FromSeconds(1), false, null);

            ObjectUnderTest.Collect(input);
            var samples = ObjectUnderTest.GetCollectedSamples().ToArray();

            ClassicAssert.IsEmpty(samples);
        }

        [Test]
        public void MultipleSyntheticTransactionTracesAreCollected()
        {
            var firstSyntheticTrace = new TransactionTraceWireModelComponents(new TransactionMetricName("bleep", "bloop"), TimeSpan.FromSeconds(1), true, null);
            var secondSyntheticTrace = new TransactionTraceWireModelComponents(new TransactionMetricName("bleep", "bloop"), TimeSpan.FromSeconds(1), true, null);

            ObjectUnderTest.Collect(firstSyntheticTrace);
            ObjectUnderTest.Collect(secondSyntheticTrace);

            var samples = ObjectUnderTest.GetCollectedSamples().ToArray();

            ClassicAssert.AreEqual(2, samples.Length);
            ClassicAssert.Contains(firstSyntheticTrace, samples);
            ClassicAssert.Contains(secondSyntheticTrace, samples);
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

            ClassicAssert.AreEqual(SyntheticsHeader.MaxTraceCount, samples.Length);
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

                ClassicAssert.AreEqual(SyntheticsHeader.MaxTraceCount, samples.Length);
            }
        }
    }
}
