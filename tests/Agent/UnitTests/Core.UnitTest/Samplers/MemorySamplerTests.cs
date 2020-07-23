using System;
using JetBrains.Annotations;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transformers;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Samplers
{
    [TestFixture]
    public class MemorySamplerTests
    {
        [NotNull]
        private MemorySampler _memorySampler;

        [NotNull]
        private IAgentHealthReporter _agentHealthReporter;

        [NotNull]
        private IMemorySampleTransformer _memorySampleTransformer;

        [NotNull]
        private Action _sampleAction;

        [SetUp]
        public void SetUp()
        {
            var scheduler = Mock.Create<IScheduler>();
            Mock.Arrange(() => scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>()))
                .DoInstead<Action, TimeSpan, TimeSpan?>((action, _, __) => _sampleAction = action);
            _agentHealthReporter = Mock.Create<IAgentHealthReporter>();
            _memorySampleTransformer = Mock.Create<IMemorySampleTransformer>();
            _memorySampler = new MemorySampler(scheduler, _memorySampleTransformer, _agentHealthReporter);
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
    }
}
