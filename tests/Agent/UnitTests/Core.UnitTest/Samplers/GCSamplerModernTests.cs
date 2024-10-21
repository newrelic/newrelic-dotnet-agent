// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transformers;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Samplers
{
    [TestFixture]
    public class GCSamplerModernTests
    {
        private IScheduler _scheduler;
        private IGCSampleTransformerModern _transformer;
        private IGCSamplerModernReflectionHelper _reflectionHelper;
        private GCSamplerModern _gcSamplerModern;

        [SetUp]
        public void SetUp()
        {
            _scheduler = Mock.Create<IScheduler>();
            _transformer = Mock.Create<IGCSampleTransformerModern>();
            _reflectionHelper = Mock.Create<IGCSamplerModernReflectionHelper>();

            _gcSamplerModern = new GCSamplerModern(_scheduler, _transformer, _reflectionHelper);
        }

        [TearDown]
        public void TearDown()
        {
            _gcSamplerModern.Dispose();
        }

        [Test]
        public void Sample_ShouldStop_WhenReflectionFails()
        {
            // Arrange
            Mock.Arrange(() => _reflectionHelper.ReflectionFailed).Returns(true);

            // Act
            _gcSamplerModern.Sample();

            // Assert
            Mock.Assert(() => _scheduler.StopExecuting(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan?>()), Occurs.Once());
        }

        [Test]
        public void Sample_TransformsSuccessfully()
        {
            // Arrange
            Mock.Arrange(() => _reflectionHelper.ReflectionFailed).Returns(false);

            var gcMemoryInfo = new GCMemoryInfo { TotalCommittedBytes = 4096L };
            var generationInfo = new[]
            {
                new GenerationInfo { SizeAfterBytes = 100L, FragmentationAfterBytes = 10L },
                new GenerationInfo { SizeAfterBytes = 200L, FragmentationAfterBytes = 20L },
                new GenerationInfo { SizeAfterBytes = 300L, FragmentationAfterBytes = 30L },
                new GenerationInfo { SizeAfterBytes = 400L, FragmentationAfterBytes = 40L },
                new GenerationInfo { SizeAfterBytes = 500L, FragmentationAfterBytes = 50L }
            };

            Mock.Arrange(() => _reflectionHelper.GCGetMemoryInfo_Invoker(Arg.IsAny<object>())).Returns(gcMemoryInfo);
            Mock.Arrange(() => _reflectionHelper.GetGenerationInfo(Arg.IsAny<object>())).Returns(generationInfo);
            Mock.Arrange(() => _reflectionHelper.GCGetTotalAllocatedBytes_Invoker(Arg.IsAny<object>())).Returns(2048L);

            // Act
            _gcSamplerModern.Sample();

            // Assert
            Mock.Assert(() => _transformer.Transform(Arg.IsAny<ImmutableGCSample>()), Occurs.Once());
        }

        // Mock classes to replace anonymous types
        public class GCMemoryInfo
        {
            public long TotalCommittedBytes { get; set; }
        }

        public class GenerationInfo
        {
            public long SizeAfterBytes { get; set; }
            public long FragmentationAfterBytes { get; set; }
        }
    }
}
