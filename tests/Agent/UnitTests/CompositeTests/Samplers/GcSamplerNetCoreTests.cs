// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using CompositeTests;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transformers;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Samplers
{
    [TestFixture]
    public class GcSamplerNetCoreTests
    {
        private CompositeTestAgent _compositeTestAgent;
        private IScheduler _mockScheduler;
        private ISampledEventListener<Dictionary<GCSampleType, float>> _mockEventListener;
        private Func<ISampledEventListener<Dictionary<GCSampleType, float>>> _mockEventListenerFactory;
        private IGcSampleTransformer _mockTransformer;

        private readonly static Func<GCSamplerNetCore.SamplerIsApplicableToFrameworkResult> _fxSamplerValidForFrameworkOverride = () => new GCSamplerNetCore.SamplerIsApplicableToFrameworkResult(true);

        /// <summary>
        /// This list of sample types collected by the sampler
        /// </summary>
        public static readonly GCSampleType[] ExpectedSampleTypes = new GCSampleType[]
        {
            GCSampleType.Gen0Size,
            GCSampleType.Gen0Promoted,
            GCSampleType.Gen1Size,
            GCSampleType.Gen1Promoted,
            GCSampleType.Gen2Size,
            GCSampleType.Gen2Survived,
            GCSampleType.LOHSize,
            GCSampleType.LOHSurvived,
            GCSampleType.InducedCount,
            GCSampleType.HandlesCount,
            GCSampleType.Gen0CollectionCount,
            GCSampleType.Gen1CollectionCount,
            GCSampleType.Gen2CollectionCount
        };


        [SetUp]
        public void Setup()
        {
            _compositeTestAgent = new CompositeTestAgent();
            _compositeTestAgent.SetEventListenerSamplersEnabled(true);

            _mockScheduler = Mock.Create<IScheduler>();

            //Prevents the scheduler from actually running
            Mock.Arrange(() => _mockScheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>()))
                .DoNothing();


            _mockEventListener = Mock.Create<ISampledEventListener<Dictionary<GCSampleType, float>>>();
            _mockEventListenerFactory = () => _mockEventListener;
            _mockTransformer = Mock.Create<IGcSampleTransformer>();
        }

        [TearDown]
        public void TearDown()
        {
            _compositeTestAgent.Dispose();
        }

        [Test]
        public void SamplerStartsWithoutException()
        {
            var sampler = new GCSamplerNetCore(_mockScheduler, _mockEventListenerFactory, _mockTransformer, _fxSamplerValidForFrameworkOverride);
            sampler.Start();
        }

        [Test]
        public void SamplerDisposesEventListenerOnException()
        {
            var samplerWasStopped = false;
            var listenerWasDisposed = false;

            var mockListener = Mock.Create<ISampledEventListener<Dictionary<GCSampleType, float>>>();

            Mock.Arrange(() => mockListener.Dispose())
                .DoInstead(() => { listenerWasDisposed = true; });

            //This is our mechanism for shutting down the sampler.  If a config change is used, it starts/stops 3x which makes
            //it difficult to determine current state.  Instead, throw an exception in the eventListener's sample method.
            Mock.Arrange(() => mockListener.Sample())
                .DoInstead(() => throw new Exception());

            Func<ISampledEventListener<Dictionary<GCSampleType, float>>> mockListenerFactory = () =>
            {
                return mockListener;
            };

            var mockScheduler = Mock.Create<IScheduler>();

            //Prevents the scheduler from actually running
            Mock.Arrange(() => _mockScheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>()))
                .DoNothing();

            //Tracks the stop executing for the scheduler which indicates that the sampler
            //has requested it to stop;
            Mock.Arrange(() => mockScheduler.StopExecuting(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan?>()))
                .DoInstead<Action, TimeSpan?>((a, t) => { samplerWasStopped = true; });

            var sampler = new GCSamplerNetCore(mockScheduler, mockListenerFactory, _mockTransformer, _fxSamplerValidForFrameworkOverride);

            //Act
            sampler.Start();

            //Cause error which will shut down the sampler
            sampler.Sample();

            //Assert
            ClassicAssert.IsTrue(samplerWasStopped);
            ClassicAssert.IsTrue(listenerWasDisposed);
        }

        [Test]
        public void SamplerDisposesEventListenerWhenDisposed()
        {
            var listenerWasDisposed = false;

            var mockListener = Mock.Create<ISampledEventListener<Dictionary<GCSampleType, float>>>();

            Mock.Arrange(() => mockListener.Dispose())
                .DoInstead(() => { listenerWasDisposed = true; });

            Func<ISampledEventListener<Dictionary<GCSampleType, float>>> mockListenerFactory = () =>
            {
                return mockListener;
            };

            var sampler = new GCSamplerNetCore(_mockScheduler, mockListenerFactory, _mockTransformer, _fxSamplerValidForFrameworkOverride);

            //Act
            sampler.Start();
            sampler.Dispose();

            //Assert
            ClassicAssert.IsTrue(listenerWasDisposed);
        }

        [Test]
        public void SamplerStartsEventListenerWhenStarted()
        {
            var wasStarted = false;

            Func<ISampledEventListener<Dictionary<GCSampleType, float>>> mockListenerFactory = () =>
            {
                wasStarted = true;
                return _mockEventListener;
            };

            var sampler = new GCSamplerNetCore(_mockScheduler, mockListenerFactory, _mockTransformer, _fxSamplerValidForFrameworkOverride);

            //Act
            sampler.Start();

            //Assert
            ClassicAssert.IsTrue(wasStarted);
        }

        [Test]
        public void ExceptionOnStartupIsHandled()
        {
            Func<ISampledEventListener<Dictionary<GCSampleType, float>>> mockListenerFactory = () =>
            {
                throw new Exception();
            };

            var sampler = new GCSamplerNetCore(_mockScheduler, mockListenerFactory, _mockTransformer, _fxSamplerValidForFrameworkOverride);

            Assert.DoesNotThrow(sampler.Start);
        }

        [Test]
        public void FailureToCollectSampleShutsDownSampler()
        {
            var samplerWasStopped = false;
            var sampleAttempted = false;

            var mockListener = Mock.Create<ISampledEventListener<Dictionary<GCSampleType, float>>>();

            Mock.Arrange(() => mockListener.Sample())
                .DoInstead(() =>
                {
                    sampleAttempted = true;
                    throw new Exception();
                });

            Func<ISampledEventListener<Dictionary<GCSampleType, float>>> mockListenerFactory = () =>
            {
                return mockListener;
            };


            var mockScheduler = Mock.Create<IScheduler>();

            //Prevents the scheduler from actually running
            Mock.Arrange(() => _mockScheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>()))
                .DoNothing();

            //Tracks the stop executing for the scheduler which indicates that the sampler
            //has requested it to stop;
            Mock.Arrange(() => mockScheduler.StopExecuting(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan?>()))
                .DoInstead<Action, TimeSpan?>((a, t) => { samplerWasStopped = true; });



            //Act
            var sampler = new GCSamplerNetCore(mockScheduler, mockListenerFactory, _mockTransformer, _fxSamplerValidForFrameworkOverride);

            sampler.Start();
            sampler.Sample();

            ClassicAssert.IsTrue(sampleAttempted);
            ClassicAssert.IsTrue(samplerWasStopped);
        }

        [Test]
        public void UnsupportedPlatformPreventsSamplerFromStarting()
        {
            //Arrange a way to capture result
            var listenerWasStarted = false;
            var mockListener = Mock.Create<ISampledEventListener<Dictionary<GCSampleType, float>>>();
            Func<ISampledEventListener<Dictionary<GCSampleType, float>>> mockListenerFactory = () =>
            {
                listenerWasStarted = true;
                return mockListener;
            };


            //Act
            var sampler = new GCSamplerNetCore(_mockScheduler, mockListenerFactory, _mockTransformer, () => new GCSamplerNetCore.SamplerIsApplicableToFrameworkResult(false));
            sampler.Start();

            ClassicAssert.IsFalse(listenerWasStarted);
        }

        [Test]
        public void SupportedPlatformAllowsSamplerToStart()
        {
            //Arrange a way to capture result
            var listenerWasStarted = false;
            var mockListener = Mock.Create<ISampledEventListener<Dictionary<GCSampleType, float>>>();
            Func<ISampledEventListener<Dictionary<GCSampleType, float>>> mockListenerFactory = () =>
            {
                listenerWasStarted = true;
                return mockListener;
            };


            //Act
            var sampler = new GCSamplerNetCore(_mockScheduler, mockListenerFactory, _mockTransformer, _fxSamplerValidForFrameworkOverride);
            sampler.Start();

            ClassicAssert.IsTrue(listenerWasStarted);
        }

        [Test]
        public void SamplerInvokesTransformerTransformMethod()
        {
            var collectedSamples = new List<Dictionary<GCSampleType, float>>();

            var mockListener = Mock.Create<ISampledEventListener<Dictionary<GCSampleType, float>>>();

            Mock.Arrange(() => mockListener.Sample())
                .Returns(() => new Dictionary<GCSampleType, float>());

            Func<ISampledEventListener<Dictionary<GCSampleType, float>>> mockListenerFactory = () =>
            {
                return mockListener;
            };

            var mockTransfomer = Mock.Create<IGcSampleTransformer>();
            Mock.Arrange(() => mockTransfomer.Transform(Arg.IsAny<Dictionary<GCSampleType, float>>()))
                .DoInstead<Dictionary<GCSampleType, float>>((sampleValues) => { collectedSamples.Add(sampleValues); });

            var sampler = new GCSamplerNetCore(_mockScheduler, mockListenerFactory, mockTransfomer, _fxSamplerValidForFrameworkOverride);

            sampler.Start();
            sampler.Sample();

            ClassicAssert.AreEqual(1, collectedSamples.Count, $"Transform should have only been called once, it was called {collectedSamples.Count} time(s).");
        }

        [Test]
        public void SamplerTransformsValuesFromExpectedGCSampleTypes()
        {
            var collectedSamples = new List<Dictionary<GCSampleType, float>>();

            var mockListener = Mock.Create<ISampledEventListener<Dictionary<GCSampleType, float>>>();

            Mock.Arrange(() => mockListener.Sample())
                .Returns(() => ExpectedSampleTypes.ToDictionary(x => x, x => 0f));

            Func<ISampledEventListener<Dictionary<GCSampleType, float>>> mockListenerFactory = () =>
            {
                return mockListener;
            };

            var mockTransfomer = Mock.Create<IGcSampleTransformer>();
            Mock.Arrange(() => mockTransfomer.Transform(Arg.IsAny<Dictionary<GCSampleType, float>>()))
                .DoInstead<Dictionary<GCSampleType, float>>((sampleValues) => { collectedSamples.Add(sampleValues); });

            var sampler = new GCSamplerNetCore(_mockScheduler, mockListenerFactory, mockTransfomer, _fxSamplerValidForFrameworkOverride);

            sampler.Start();
            sampler.Sample();

            ClassicAssert.AreEqual(1, collectedSamples.Count, "Only one sample should have been taken");
            Assert.That(collectedSamples[0].Keys.ToArray(), Is.EquivalentTo(ExpectedSampleTypes), $"Mismatch between the GSampleTypes returned from Sample to the expectedList");
        }
    }
}
