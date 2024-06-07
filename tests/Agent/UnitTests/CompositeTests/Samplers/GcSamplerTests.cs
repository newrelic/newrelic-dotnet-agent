// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Linq;
using CompositeTests;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transformers;
using NewRelic.SystemInterfaces;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Samplers
{
    [TestFixture]
    public class GcSamplerTests
    {
        private IPerformanceCounterProxyFactory _perfCounterProxyFactory;

        private Action _sampleAction;

        private CompositeTestAgent _compositeTestAgent;

        private IScheduler _scheduler;

        private static readonly GCSampleType[] _expectedSampleTypes = new GCSampleType[]
        {
                GCSampleType.Gen0Size,
                GCSampleType.Gen0Promoted,
                GCSampleType.Gen1Size,
                GCSampleType.Gen1Promoted,
                GCSampleType.Gen2Size,
                GCSampleType.LOHSize,
                GCSampleType.HandlesCount,
                GCSampleType.InducedCount,
                GCSampleType.PercentTimeInGc,
                GCSampleType.Gen0CollectionCount,
                GCSampleType.Gen1CollectionCount,
                GCSampleType.Gen2CollectionCount
        };

        private readonly int _countSampleTypes = _expectedSampleTypes.Count();

        [SetUp]
        public void SetUp()
        {
            _compositeTestAgent = new CompositeTestAgent();

            _scheduler = Mock.Create<IScheduler>();

            Mock.Arrange(() => _scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>()))
                .DoInstead<Action, TimeSpan, TimeSpan?>((action, _, __) => _sampleAction = action);

            _perfCounterProxyFactory = Mock.Create<IPerformanceCounterProxyFactory>();
        }

        [TearDown]
        public void TearDown()
        {
            _compositeTestAgent.Dispose();
        }

        /// <summary>
        /// Ensures that the sampler calculates the values correctly.  Most are direct pass-through of the value
        /// obtained from the windows perf counter, but some are deltas from prior value.
        /// </summary>
        [Test]
        public void SamplerCalculatesValuesCorrectly()
        {
            const int expectedSampleCount = 2;
            const float startVal = 3f;
            const float deltaVal = 10f;
            var lastPerfCounterValue = 0f;
            var testPerfCounterValue = startVal;

            //Create a mock proxy that allows us to return a specific value and ensure that the proxy factory returns this proxy.
            var mockProxy = Mock.Create<IPerformanceCounterProxy>();
            Mock.Arrange(() => mockProxy.NextValue())
                .Returns(() => { return testPerfCounterValue; });

            Mock.Arrange(() => _perfCounterProxyFactory.CreatePerformanceCounterProxy(Arg.IsAny<string>(), Arg.IsAny<string>(), Arg.IsAny<string>()))
                .Returns(mockProxy);

            Mock.Arrange(() => _perfCounterProxyFactory.GetCurrentProcessInstanceNameForCategory(Arg.IsAny<string>(),Arg.IsAny<string>()))
                .Returns("Test Value");

            //Holds the results of the perf counter captures so that we may compare them as part of our assertions.
            List<Dictionary<GCSampleType, float>> perfCounterValues = new List<Dictionary<GCSampleType, float>>();


            //Intercept the transform method so that we can collect the values that were sampled from the performance counters
            var gcSampleTransformer = Mock.Create<IGcSampleTransformer>();
            Mock.Arrange(() => gcSampleTransformer.Transform(Arg.IsAny<Dictionary<GCSampleType, float>>()))
                .DoInstead<Dictionary<GCSampleType, float>>((sampleValues) =>
                {
                    lastPerfCounterValue = testPerfCounterValue;
                    perfCounterValues.Add(sampleValues);
                    testPerfCounterValue += deltaVal;
                });

            var sampler = new GcSampler(_scheduler, gcSampleTransformer, _perfCounterProxyFactory);

            //Act
            sampler.Start();
            _sampleAction();    //Collect Sample 1
            _sampleAction();    //Collect Sample 2

            //Assert
            NrAssert.Multiple(
                () => Assert.That(perfCounterValues, Has.Count.EqualTo(expectedSampleCount), $"There should have been {expectedSampleCount} samples collected"),
                () => Assert.That(perfCounterValues[0], Has.Count.EqualTo(_countSampleTypes), $"There should be {_countSampleTypes} in the first sample"),
                () => Assert.That(perfCounterValues[1], Has.Count.EqualTo(_countSampleTypes), $"There should be {_countSampleTypes} in the second sample")
            );

            //Validate the sampled(calculated) value for each counter type.
            foreach (var gcSampleType in perfCounterValues[0].Keys)
            {
                //For the first sample, all should have the same value
                Assert.That(perfCounterValues[0][gcSampleType], Is.EqualTo(startVal));

                //For the second sample...
                switch (gcSampleType)
                {
                    //...delta values should represent the size of the delta
                    case GCSampleType.Gen0CollectionCount:
                    case GCSampleType.Gen1CollectionCount:
                    case GCSampleType.Gen2CollectionCount:
                    case GCSampleType.InducedCount:
                        Assert.That(perfCounterValues[1][gcSampleType], Is.EqualTo(deltaVal));
                        break;

                    //...other measurements should be passed through with the perf counter value
                    default:
                        Assert.That(perfCounterValues[1][gcSampleType], Is.EqualTo(lastPerfCounterValue));
                        break;
                }
            }
        }

        /// <summary>
        /// Test to ensure that if a failure occurs while setting up a proxy to pull values from
        /// performance counter, the setup process will continue, attempting to create other proxies
        /// </summary>
        [Test]
        public void CreatePerfCounterSingleFailureContinuesToNext()
        {
            var countAttempts = 0;
            var countFails = 0;
            var countSuccess = 0;

            //Create a mock proxies, one that fails and one that succeeds
            var mockProxy = Mock.Create<IPerformanceCounterProxy>();

            Mock.Arrange(() => _perfCounterProxyFactory.CreatePerformanceCounterProxy(Arg.IsAny<string>(), Arg.IsAny<string>(), Arg.IsAny<string>()))
                .Returns<string, string>((catName, perfCounterName) =>
                 {
                     if ((++countAttempts) % 2 == 0)
                     {
                         countSuccess++;
                         return mockProxy;
                     }
                     else
                     {
                         countFails++;
                         throw new Exception();
                     }
                 });

            Mock.Arrange(() => _perfCounterProxyFactory.GetCurrentProcessInstanceNameForCategory(Arg.IsAny<string>(),Arg.IsAny<string>()))
                .Returns("Test Value");

            var gcSampleTransformer = Mock.Create<IGcSampleTransformer>();
            var sampler = new GcSampler(_scheduler, gcSampleTransformer, _perfCounterProxyFactory);

            //Intercept the stop call for the scheduler to see if the sampler was shut down.
            var stopWasCalled = false;

            Mock.Arrange(() => _scheduler.StopExecuting(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan?>()))
                .DoInstead(() => { stopWasCalled = true; });

            //Act
            Assert.DoesNotThrow(sampler.Start);
            Assert.DoesNotThrow(sampler.Sample);

            Assert.Multiple(() =>
            {
                //Assert
                Assert.That(countAttempts, Is.EqualTo(_countSampleTypes));
                Assert.That(countFails, Is.GreaterThan(0));
                Assert.That(countSuccess, Is.GreaterThan(0));
                Assert.That(stopWasCalled, Is.False);
            });
        }

        [Test]
        public void SamplingStopsAfterExceedConsecutiveFailureLimit()
        {
            var countAttempts = 0;

            Mock.Arrange(() => _perfCounterProxyFactory.GetCurrentProcessInstanceNameForCategory(Arg.IsAny<string>(),Arg.IsAny<string>()))
                .Returns<string>((catName) =>
                {
                    countAttempts++;
                    if (countAttempts == 3)
                    {
                        return "Test Instance Name";
                    }

                    throw new Exception();
                });

            //Intercept the stop call for the scheduler to see if the sampler was shut down.
            var stopWasCalled = false;

            Mock.Arrange(() => _scheduler.StopExecuting(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan?>()))
                .DoInstead(() => { stopWasCalled = true; });

            var gcSampleTransformer = Mock.Create<IGcSampleTransformer>();
            var sampler = new GcSampler(_scheduler, gcSampleTransformer, _perfCounterProxyFactory);

            //enabling the sampler will publish 3 configuration updated events which will in-turn,
            //perform the start actions.   Reset our test values prior to manually trying to start
            //the sampler
            countAttempts = 0;
            stopWasCalled = false;

            Assert.DoesNotThrow(sampler.Start);
            Assert.DoesNotThrow(() => _sampleAction());     //fail
            Assert.That(stopWasCalled, Is.False);
            Assert.DoesNotThrow(() => _sampleAction());     //fail
            Assert.That(stopWasCalled, Is.False);
            Assert.DoesNotThrow(() => _sampleAction());     //success
            Assert.That(stopWasCalled, Is.False);
            Assert.DoesNotThrow(() => _sampleAction());     //fail
            Assert.That(stopWasCalled, Is.False);
            Assert.DoesNotThrow(() => _sampleAction());     //fail
            Assert.That(stopWasCalled, Is.False);
            Assert.DoesNotThrow(() => _sampleAction());     //fail
            Assert.That(stopWasCalled, Is.False);
            Assert.DoesNotThrow(() => _sampleAction());     //fail
            Assert.That(stopWasCalled, Is.False);
            Assert.DoesNotThrow(() => _sampleAction());     //fail
            Assert.That(stopWasCalled, Is.True);
        }



        /// <summary>
        /// Test to ensure that if all proxies fail to be created, the Sampler is shut down
        /// </summary>
        [Test]
        public void CreatePerfCounterAllFailuresShutsDownSampler()
        {
            var countAttempts = 0;

            Mock.Arrange(() => _perfCounterProxyFactory.CreatePerformanceCounterProxy(Arg.IsAny<string>(), Arg.IsAny<string>(), Arg.IsAny<string>()))
                .Returns<string, string>((catName, perfCounterName) =>
                {
                    countAttempts++;
                    throw new Exception();
                });

            Mock.Arrange(() => _perfCounterProxyFactory.GetCurrentProcessInstanceNameForCategory(Arg.IsAny<string>(),Arg.IsAny<string>()))
           .Returns("Test Value");

            //Intercept the stop call for the scheduler to see if the sampler was shut down.
            var stopWasCalled = false;

            Mock.Arrange(() => _scheduler.StopExecuting(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan?>()))
                .DoInstead(() => { stopWasCalled = true; });

            var gcSampleTransformer = Mock.Create<IGcSampleTransformer>();
            var sampler = new GcSampler(_scheduler, gcSampleTransformer, _perfCounterProxyFactory);

            //enabling the sampler will publish 3 configuration updated events which will in-turn,
            //perform the start actions.   Reset our test values prior to manually trying to start
            //the sampler
            countAttempts = 0;
            stopWasCalled = false;

            //Act
            Assert.DoesNotThrow(sampler.Start);
            Assert.DoesNotThrow(() => _sampleAction());
            Assert.That(stopWasCalled, Is.False);
            Assert.DoesNotThrow(() => _sampleAction());
            Assert.That(stopWasCalled, Is.False);
            Assert.DoesNotThrow(() => _sampleAction());
            Assert.That(stopWasCalled, Is.False);
            Assert.DoesNotThrow(() => _sampleAction());
            Assert.That(stopWasCalled, Is.False);
            Assert.DoesNotThrow(() => _sampleAction());

            Assert.Multiple(() =>
            {
                //Assert
                Assert.That(countAttempts, Is.EqualTo(_countSampleTypes * 5));
                Assert.That(stopWasCalled, Is.True);
            });
        }

        /// <summary>
        /// Test to ensure that if an exception is thrown when attempting to read 
        /// the value of a performance counter, the Sampler is shut down
        /// </summary>
        [Test]
        public void FailureOnSampleShutsDownSampler()
        {
            var countAttempts = 0;

            //Create a mock proxy that throws an exception and that updates a counter
            //of the number of times it was called.
            var mockProxy = Mock.Create<IPerformanceCounterProxy>();
            Mock.Arrange(() => mockProxy.NextValue())
                .Returns(() =>
                {
                    countAttempts++;
                    throw new Exception();
                });

            Mock.Arrange(() => _perfCounterProxyFactory.CreatePerformanceCounterProxy(Arg.IsAny<string>(), Arg.IsAny<string>(), Arg.IsAny<string>()))
                .Returns(mockProxy);

            Mock.Arrange(() => _perfCounterProxyFactory.GetCurrentProcessInstanceNameForCategory(Arg.IsAny<string>(),Arg.IsAny<string>()))
                .Returns("Test Value");

            var gcSampleTransformer = Mock.Create<IGcSampleTransformer>();
            var sampler = new GcSampler(_scheduler, gcSampleTransformer, _perfCounterProxyFactory);

            //Intercept the stop call for the scheduler to see if the sampler was shut down.
            var stopWasCalled = false;

            Mock.Arrange(() => _scheduler.StopExecuting(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan?>()))
                .DoInstead(() => { stopWasCalled = true; });


            //Act
            Assert.DoesNotThrow(sampler.Start);
            Assert.DoesNotThrow(() => _sampleAction());
            Assert.That(stopWasCalled, Is.False);
            Assert.DoesNotThrow(() => _sampleAction());
            Assert.That(stopWasCalled, Is.False);
            Assert.DoesNotThrow(() => _sampleAction());
            Assert.That(stopWasCalled, Is.False);
            Assert.DoesNotThrow(() => _sampleAction());
            Assert.That(stopWasCalled, Is.False);
            Assert.DoesNotThrow(() => _sampleAction());

            Assert.Multiple(() =>
            {
                //Assert
                Assert.That(countAttempts, Is.GreaterThan(0));
                Assert.That(stopWasCalled, Is.True);
            });
        }

        /// <summary>
        /// Ensure that the performance counter proxies get disposed of when Stop() is called on the sampler
        /// </summary>
        [Test]
        public void WhenSamplerIsStopped_PerfCounterProxiesAreDisposed()
        {
            var disposeAttempts = 0;

            //Create a mock proxy that updates a counter
            //of the number of times it is disposed.
            var mockProxy = Mock.Create<IPerformanceCounterProxy>();
            Mock.Arrange(() => mockProxy.Dispose()).DoInstead(() => disposeAttempts++);

            Mock.Arrange(() => _perfCounterProxyFactory.CreatePerformanceCounterProxy(Arg.IsAny<string>(), Arg.IsAny<string>(), Arg.IsAny<string>()))
                .Returns(mockProxy);

            Mock.Arrange(() => _perfCounterProxyFactory.GetCurrentProcessInstanceNameForCategory(Arg.IsAny<string>(),Arg.IsAny<string>()))
                .Returns("Test Value");

            var gcSampleTransformer = Mock.Create<IGcSampleTransformer>();
            var sampler = new GcSampler(_scheduler, gcSampleTransformer, _perfCounterProxyFactory);

            //Act
            sampler.Start();
            sampler.Sample();
            sampler.Dispose(); // calls sampler.Stop()

            //Assert
            Assert.That(disposeAttempts, Is.EqualTo(_countSampleTypes), "Perf counter proxies were not disposed when the GcSampler was stopped.");
        }
    }
}
#endif
