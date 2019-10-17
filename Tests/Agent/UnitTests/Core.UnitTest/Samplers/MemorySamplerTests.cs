using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transformers;
using NewRelic.SystemInterfaces;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Samplers
{
	[TestFixture]
	public class MemorySamplerTests
	{
		private MemorySampler _memorySampler;

		private IMemorySampleTransformer _memorySampleTransformer;

		private Action _sampleAction;

		[SetUp]
		public void SetUp()
		{
			var scheduler = Mock.Create<IScheduler>();
			Mock.Arrange(() => scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>()))
				.DoInstead<Action, TimeSpan, TimeSpan?>((action, _, __) => _sampleAction = action);
			_memorySampleTransformer = Mock.Create<IMemorySampleTransformer>();
			_memorySampler = new MemorySampler(scheduler, _memorySampleTransformer, new ProcessStatic());
			_memorySampler.Start();
		}

		[TearDown]
		public void TearDown()
		{
			_memorySampler.Dispose();
		}

		[Test]
		public void memory_sample_generated_on_sample()
		{
			// Arrange
			var memorySample = null as ImmutableMemorySample;
			Mock.Arrange(() => _memorySampleTransformer.Transform(Arg.IsAny<ImmutableMemorySample>()))
				.DoInstead<ImmutableMemorySample>(sample => memorySample = sample);

			// Act
			_sampleAction();

			// Assert
			Assert.NotNull(memorySample);
		}

		[Test]
		public void memory_values_increase_over_time()
		{
			// Arrange
			var memorySampleBefore = null as ImmutableMemorySample;
			Mock.Arrange(() => _memorySampleTransformer.Transform(Arg.IsAny<ImmutableMemorySample>()))
				.DoInstead<ImmutableMemorySample>(sample => memorySampleBefore = sample);

			// Act
			_sampleAction();

			// allocate some memory to increase .PrivateMemorySize64 and .WorkingSet64
			var someBytes = IncreaseMemoryUsage();

			// Arrange
			var memorySampleAfter = null as ImmutableMemorySample;
			Mock.Arrange(() => _memorySampleTransformer.Transform(Arg.IsAny<ImmutableMemorySample>()))
				.DoInstead<ImmutableMemorySample>(sample => memorySampleAfter = sample);

			// Act
			_sampleAction();

			// Assert
			Assert.IsTrue(memorySampleBefore.MemoryPrivate < memorySampleAfter.MemoryPrivate, "PrivateMemorySize64 did not increase as expected");
			Assert.IsTrue(memorySampleBefore.MemoryWorkingSet < memorySampleAfter.MemoryWorkingSet, "WorkingSet64 did not increase as expected");
		}

		[MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
		private byte[] IncreaseMemoryUsage()
		{
			var size = 10000000;
			byte[] bytes = new byte[size];

			Parallel.For(0, size, i => bytes[i] = 0x77);

			return bytes;
		}
	}
}
