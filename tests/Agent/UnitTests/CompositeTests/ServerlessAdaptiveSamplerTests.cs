// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace CompositeTests
{
    [TestFixture]
    [Parallelizable(ParallelScope.None)]
    public class ServerlessAdaptiveSamplerTests
    {
        private AdaptiveSampler _adaptiveSampler;
        private const float DefaultPriority = 0.5f;
        private const float PriorityBoost = 1.0f;  //must be the same as in the AdaptiveSampler
        private const float Epsilon = 1e-6f;
        private const int DefaultSeedForTesting = 6351;
        private const int DefaultSamplingTargetIntervalInSecondsForTesting = 5;

        [SetUp]
        public void BeforeEachTest()
        {
            // AdaptiveSampler checks the config for serverless mode
            _adaptiveSampler = new AdaptiveSampler(AdaptiveSampler.DefaultTargetSamplesPerInterval, DefaultSamplingTargetIntervalInSecondsForTesting, DefaultSeedForTesting, true);

            _adaptiveSampler.StartTransaction();
        }

        [TearDown]
        public void AfterEachTest()
        {
            _adaptiveSampler.Dispose();
            _adaptiveSampler = null;
        }

        [Test]
        public void ComputeSampled_FirstHarvest([Range(1, 15, 1)] int calls, [Values(0.1f, DefaultPriority, 0.9f)] float defaultPriority)
        {
            // Arrange

            // Act
            for (var callCounter = 0; callCounter < calls; ++callCounter)
            {
                var priority = defaultPriority;
                var sampled = _adaptiveSampler.ComputeSampled(ref priority);

                // Assert
                if (callCounter < _adaptiveSampler.TargetSamplesPerInterval)
                {
                    NrAssert.Multiple(
                        () => Assert.That(sampled, Is.True),
                        () => Assert.That(priority, Is.EqualTo(defaultPriority + PriorityBoost).Within(Epsilon))
                    );
                }
                else
                {
                    NrAssert.Multiple(
                        () => Assert.That(sampled, Is.False),
                        () => Assert.That(priority, Is.EqualTo(defaultPriority).Within(Epsilon))
                    );
                }
            }
        }

        [Test]
        [TestCase(100, 30)]
        [TestCase(10, 10)]
        [TestCase(20, 40)]
        public void ComputeSampled_SecondHarvest(int firstHarvestTransactionCount, int secondHarvestTransactionCount)
        {
            var testCaseName = MakeTestCaseName(firstHarvestTransactionCount, secondHarvestTransactionCount);
            // Arrange
            for (var i = 0; i < firstHarvestTransactionCount; ++i)
            {
                var pr = DefaultPriority;
                _adaptiveSampler.ComputeSampled(ref pr);
            }
            //end of Harvest, but don't start a new transaction. Should remain on the same interval, and not sample any of the new events
            System.Threading.Thread.Sleep(TimeSpan.FromSeconds(DefaultSamplingTargetIntervalInSecondsForTesting));

            var rand = new Random();
            var sampleSequence = _expectedSampleSequences[testCaseName];

            for (var callCounter = 0; callCounter < secondHarvestTransactionCount; ++callCounter)
            {
                var prePriority = Sanitize((float)rand.NextDouble());
                var priority = prePriority;
                var sampled = _adaptiveSampler.ComputeSampled(ref priority);
                //Console.Write($"{sampled},");
                var message = $"callCounter: {callCounter}";
                NrAssert.Multiple(
                    () => Assert.That(sampled, Is.False, message),
                    () => Assert.That(priority, Is.EqualTo(prePriority).Within(Epsilon), message)
                );
            }

            // This should start a new interval
            _adaptiveSampler.StartTransaction();

            Assert.That(sampleSequence, Has.Length.EqualTo(secondHarvestTransactionCount), $"testCaseName {testCaseName} firstHarvestTransactionCount {firstHarvestTransactionCount} secondHarvestTransactionCount {secondHarvestTransactionCount}");
            // Act
            for (var callCounter = 0; callCounter < secondHarvestTransactionCount; ++callCounter)
            {
                var prePriority = Sanitize((float)rand.NextDouble());
                var priority = prePriority;
                var sampled = _adaptiveSampler.ComputeSampled(ref priority);
                //Console.Write($"{testCaseName}: {callCounter}={sampled}\n");
                var message = $"callCounter: {callCounter}";
                if (sampleSequence[callCounter])
                {
                    NrAssert.Multiple(
                        () => Assert.That(sampled, Is.True, message),
                        () => Assert.That(priority, Is.EqualTo(Sanitize(prePriority + PriorityBoost)).Within(Epsilon), message)
                    );
                }
                else
                {
                    NrAssert.Multiple(
                        () => Assert.That(sampled, Is.False, message),
                        () => Assert.That(priority, Is.EqualTo(prePriority).Within(Epsilon), message)
                    );
                }
            }

        }

        // Note that while the inputs are the same as the AdaptiveSamplerTests, the outputs are different because
        // we're running the second set of transactions twice
        private readonly Dictionary<string, bool[]> _expectedSampleSequences = new Dictionary<string, bool[]>()
        {
            { MakeTestCaseName(100, 30),
                new []{
                    false, false, true, false, false,
                    false, false, false, false, false,
                    false, false, false, false, false,
                    false, false, false, false, false,
                    false, false, false, false, false,
                    false, false, false, false, false
                }
            },
            { MakeTestCaseName(10, 10),
                new []{
                    false, true, true, true, false,
                    false, false, false, false, true
                }
            },
            { MakeTestCaseName(20, 40),
                new []
                {
                    false, false, true, true, false, false, false, false, false, false,
                    false, false, false, false, false, false, false, false, false, false,
                    false, false, false, false, true, false, false, false, false, false,
                    false, false, false, false, false, true, false, false, false, true
                }
            },
            { MakeTestCaseName(0, 20),
            new []
            {
                true,true,true,true,true,true,true,true,true,true,true,false,false,false,false,false,false,false,false,false
            }
        }
        };

        private static float Sanitize(float priority)
        {
            const uint sanitizeShiftDecimalPoint = 1000000;
            //truncates to six digits to the right of the decimal point
            return (float)(uint)(priority * sanitizeShiftDecimalPoint) / sanitizeShiftDecimalPoint;
        }

        private static string MakeTestCaseName(int firstHarvestTransactionCount, int secondHarvestTransactionCount)
        {
            return $"{firstHarvestTransactionCount}_{secondHarvestTransactionCount}";
        }

    }
}
