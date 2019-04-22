using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.DistributedTracing
{
	[TestFixture]
	[Parallelizable(ParallelScope.None)]
	public class AdaptiveSamplerTests
	{
		private AdaptiveSampler _adaptiveSampler;
		private const float DefaultPriority = 0.5f;
		private const float PriorityBoost = 1.0f;  //must be the same as in the AdaptiveSampler
		private const float Epsilon = 1e-6f;
		private const int DefaultSeedForTesting = 6351;

		[SetUp]
		public void BeforeEachTest()
		{
			_adaptiveSampler = new AdaptiveSampler(AdaptiveSampler.DefaultTargetSamplesPerInterval, DefaultSeedForTesting);
		}

		[TearDown]
		public void AfterEachTest()
		{
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

		private readonly Dictionary<uint, bool[]> _testResult = new Dictionary<uint, bool[]>()
		{
			{ (100u<<16)+30u,
				new []{ false, false, true, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false }
			},
			{ (10u<<16)+10u,
				new []{ true, true, true, true, true, true, true, true, true, true }
			},
			{ (20u<<16)+40u,
				new []
				{
					false, true, true, true, false, false, false, false, false, true, true, false, false, false, false, true, false, false, false, true, false, false, true, false, 
					true, true, false, false, false, false, false, false, true, false, false, true, false, false, false, true
				}
			},
			{ (0u<<16)+20u,
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

		[Test]
		[TestCase(100u, 30u)]
		[TestCase(10u, 10u)]
		[TestCase(20u, 40u)]
		[TestCase(0u, 20u)]
		public void ComputeSampled_SecondHarvest(uint firstHarvestTransactionCount, uint secondHarvestTransactionCount)
		{
			var testKey = ((firstHarvestTransactionCount & ushort.MaxValue) << 16) + (secondHarvestTransactionCount & ushort.MaxValue);
			// Arrange
			for (var i = 0; i < firstHarvestTransactionCount; ++i)
			{
				var pr = DefaultPriority;
				_adaptiveSampler.ComputeSampled(ref pr);
			}
			//end of Harvest
			_adaptiveSampler.EndOfSamplingInterval();

			var rand = new Random();
			var sampleSequence = _testResult[testKey];

			Assert.That(sampleSequence.Length, Is.EqualTo(secondHarvestTransactionCount), $"testKey {testKey:X8} firstHarvestTransactionCount {firstHarvestTransactionCount} secondHarvestTransactionCount {secondHarvestTransactionCount}");
			// Act
			for (var callCounter = 0; callCounter < secondHarvestTransactionCount; ++callCounter)
			{
				var prePriority = Sanitize((float)rand.NextDouble());
				var priority = prePriority;
				var sampled = _adaptiveSampler.ComputeSampled(ref priority);
				//Console.Write($"{sampled},");
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
	}
}
