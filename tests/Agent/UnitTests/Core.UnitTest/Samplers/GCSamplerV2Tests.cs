// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
#if NET

using System;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transformers;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Samplers
{
    [TestFixture]
    public class GCSamplerV2Tests
    {
        private IScheduler _scheduler;
        private IGCSampleTransformerV2 _transformer;
        private IGCSamplerV2ReflectionHelper _reflectionHelper;
        private GCSamplerV2 _gcSamplerV2;

        [SetUp]
        public void SetUp()
        {
            _scheduler = Mock.Create<IScheduler>();
            _transformer = Mock.Create<IGCSampleTransformerV2>();
            _reflectionHelper = Mock.Create<IGCSamplerV2ReflectionHelper>();

            _gcSamplerV2 = new GCSamplerV2(_scheduler, _transformer, _reflectionHelper);
        }

        [TearDown]
        public void TearDown()
        {
            _gcSamplerV2.Dispose();
        }

        [Test]
        public void Sample_ShouldStop_WhenReflectionFails()
        {
            // Arrange
            Mock.Arrange(() => _reflectionHelper.ReflectionFailed).Returns(true);

            // Act
            _gcSamplerV2.Sample();

            // Assert
            Mock.Assert(() => _scheduler.StopExecuting(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan?>()), Occurs.Once());
        }

        [Test]
        public void Sample_ShouldNotTransform_WhenNoGCOccurred()
        {
            // Arrange
            Mock.Arrange(() => _reflectionHelper.ReflectionFailed).Returns(false);
            Mock.Arrange(() => _reflectionHelper.HasGCOccurred).Returns(false);

            // Act
            _gcSamplerV2.Sample();

            // Assert
            Mock.Assert(() => _transformer.Transform(Arg.IsAny<ImmutableGCSample>()), Occurs.Never());
        }

        [Test]
        public void Sample_Transforms_WhenGCHasOccurred()
        {
            // Arrange
            Mock.Arrange(() => _reflectionHelper.ReflectionFailed).Returns(false);
            Mock.Arrange(() => _reflectionHelper.HasGCOccurred).Returns(true);

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
            _gcSamplerV2.Sample();

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
#endif
