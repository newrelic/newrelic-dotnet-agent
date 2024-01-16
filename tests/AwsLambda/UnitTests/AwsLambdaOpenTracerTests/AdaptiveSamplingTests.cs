// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Tests.AwsLambda.AwsLambdaOpenTracerTests
{
    [TestFixture]
    public class AdaptiveSamplingTests
    {
        private AdaptiveSampler _adaptiveSampler;
        private const int Target = 10;
        private const int Interval = 10;
        private const int DefaultSeedForTesting = 6351;

        [SetUp]
        public void Setup()
        {
            _adaptiveSampler = new AdaptiveSampler(Target, Interval, new Random(DefaultSeedForTesting));
        }

        [Test]
        public void ComputeSampled_FirstPeriod()
        {
            var trueCount = 0;
            for (var i = 0; i < Target; i++)
            {
                if (_adaptiveSampler.ComputeSampled())
                {
                    trueCount++;
                }
            }

            Assert.That(trueCount, Is.EqualTo(Target));
        }

        [Test]
        public void ComputeSampled_FirstPeriod_SamplesTargetNumber()
        {
            var trueCount = 0;
            for (var i = 0; i < Target + 1; i++)
            {
                if (_adaptiveSampler.ComputeSampled())
                {
                    trueCount++;
                }
            }

            Assert.That(trueCount, Is.EqualTo(Target));
        }


        [Test]
        public void ComputeSampled_StartRequest_PostReset()
        {
            _adaptiveSampler.RequestStarted();
            var trueCount = 0;
            for (var i = 0; i < Target - 1; i++)
            {
                if (_adaptiveSampler.ComputeSampled())
                {
                    trueCount++;
                }
            }

            _adaptiveSampler.Reset();
            Assert.That(_adaptiveSampler.ComputeSampled(), Is.True);
        }

        [Test]
        public void ComputeSampled_ExpotentialBackoff()
        {
            _adaptiveSampler.RequestStarted();
            var trueCount = 0;
            for (var i = 0; i < Target - 1; i++)
            {
                if (_adaptiveSampler.ComputeSampled())
                {
                    trueCount++;
                }
            }

            _adaptiveSampler.Reset();

            trueCount = 0;

            for (var i = 0; i < Target * 500; i++)
            {
                if (_adaptiveSampler.ComputeSampled())
                {
                    trueCount++;
                }
            }

            // This assertion really just detects changes to the exponential backoff algorithm,
            // as opposed to asserting on something we can derive from first principles
            Assert.That(trueCount, Is.EqualTo(16));
        }

        [Test]
        [TestCase(100, 30)]
        [TestCase(10, 10)]
        [TestCase(20, 40)]
        [TestCase(0, 20)]
        public void ComputeSampled_SecondHarvest(int firstHarvestTransactionCount, int secondHarvestTransactionCount)
        {
            var testCaseName = MakeTestCaseName(firstHarvestTransactionCount, secondHarvestTransactionCount);
            // Arrange
            _adaptiveSampler.RequestStarted();
            for (var i = 0; i < firstHarvestTransactionCount; ++i)
            {
                _adaptiveSampler.ComputeSampled();
            }

            _adaptiveSampler.Reset();

            var sampleSequence = _expectedSampleSequences[testCaseName];

            Assert.That(sampleSequence.Length, Is.EqualTo(secondHarvestTransactionCount), $"testCaseName {testCaseName} firstHarvestTransactionCount {firstHarvestTransactionCount} secondHarvestTransactionCount {secondHarvestTransactionCount}");
            // Act
            for (var callCounter = 0; callCounter < secondHarvestTransactionCount; ++callCounter)
            {
                var expectedSamplingResult = sampleSequence[callCounter];
                var actualSamplingResult = _adaptiveSampler.ComputeSampled();
                var message = $"callCounter: {callCounter}";
                ClassicAssert.AreEqual(expectedSamplingResult, actualSamplingResult, message);
            }
        }

        private readonly Dictionary<string, bool[]> _expectedSampleSequences = new Dictionary<string, bool[]>()
        {
            { MakeTestCaseName(100, 30),
                new []{ false, false, true, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false }
            },
            { MakeTestCaseName(10, 10),
                new []{ true, true, true, true, true, true, true, true, true, true }
            },
            { MakeTestCaseName(20, 40),
                new []
                {
                    false, true, true, true, false, false, false, false, false, true, true, false, false, false, false, true, false, false, false, true, false, false, true, false,
                    true, true, false, false, false, false, false, false, true, false, false, true, false, false, false, true
                }
            },
            { MakeTestCaseName(0, 20),
            new []
            {
                true,true,true,true,true,true,true,true,true,true,true,false,false,false,false,false,false,false,false,false
            }
        }
        };

        private static string MakeTestCaseName(int firstHarvestTransactionCount, int secondHarvestTransactionCount)
        {
            return $"{firstHarvestTransactionCount}_{secondHarvestTransactionCount}";
        }

    }
}
