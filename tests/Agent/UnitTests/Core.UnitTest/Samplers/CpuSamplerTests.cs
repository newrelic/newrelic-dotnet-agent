using System;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transformers;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Samplers
{
    [TestFixture]
    public class CpuSamplerTests
    {
        private CpuSampler _cpuSampler;
        private IAgentHealthReporter _agentHealthReporter;
        private ICpuSampleTransformer _cpuSampleTransformer;
        private Action _sampleAction;

        [SetUp]
        public void SetUp()
        {
            var scheduler = Mock.Create<IScheduler>();
            Mock.Arrange(() => scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>()))
                .DoInstead<Action, TimeSpan, TimeSpan?>((action, _, __) => _sampleAction = action);
            _agentHealthReporter = Mock.Create<IAgentHealthReporter>();
            _cpuSampleTransformer = Mock.Create<ICpuSampleTransformer>();
            _cpuSampler = new CpuSampler(scheduler, _cpuSampleTransformer, _agentHealthReporter);
        }

        [TearDown]
        public void TearDown()
        {
            _cpuSampler.Dispose();
        }

        [Test]
        public void cpu_sample_generated_on_sample()
        {
            // Arrange
            var cpuSample = null as ImmutableCpuSample;
            Mock.Arrange(() => _cpuSampleTransformer.Transform(Arg.IsAny<ImmutableCpuSample>()))
                .DoInstead<ImmutableCpuSample>(sample => cpuSample = sample);

            // Act
            _sampleAction();

            // Assert
            Assert.NotNull(cpuSample);
        }
    }
}
