// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using NewRelic.Agent.Api;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Extensions.Api.Experimental;
using NUnit.Framework;
using Telerik.JustMock;
using Telerik.JustMock.Helpers;

namespace NewRelic.Agent.Core.OpenTelemetryBridge
{
    [TestFixture]
    public class NewRelicActivitySourceProxyTests
    {
        private Type _activitySourceType;
        private Type _activityKindType;
        private FieldInfo _activitySourceField;
        private FieldInfo _usingRuntimeActivitySourceField;
        private NewRelicActivitySourceProxy _proxy;

        [SetUp]
        public void Setup()
        {
            _activitySourceType = typeof(MockActivitySource);
            _activityKindType = typeof(MockActivityKind);
            _activitySourceField = typeof(NewRelicActivitySourceProxy).GetField("_activitySource", BindingFlags.NonPublic | BindingFlags.Static);
            _usingRuntimeActivitySourceField = typeof(NewRelicActivitySourceProxy).GetField("_usingRuntimeActivitySource", BindingFlags.NonPublic | BindingFlags.Static);
            
            // Reset static fields between tests
            _activitySourceField.SetValue(null, null);
            _usingRuntimeActivitySourceField.SetValue(null, 0);
            
            _proxy = new NewRelicActivitySourceProxy();
        }

        [Test]
        public void SegmentCustomPropertyName_HasExpectedValue()
        {
            // Assert
            Assert.That(NewRelicActivitySourceProxy.SegmentCustomPropertyName, Is.EqualTo("NewRelicSegment"));
        }

        [Test]
        public void SetAndCreateRuntimeActivitySource_CreatesActivitySource()
        {
            // Act
            NewRelicActivitySourceProxy.SetAndCreateRuntimeActivitySource(_activitySourceType, _activityKindType);

            // Assert
            var activitySource = _activitySourceField.GetValue(null);
            Assert.That(activitySource, Is.Not.Null);
            Assert.That(activitySource, Is.InstanceOf<INewRelicActivitySource>());
            Assert.That(activitySource, Is.InstanceOf<RuntimeActivitySource>());
        }

        [Test]
        public void SetAndCreateRuntimeActivitySource_WithFactory_UsesFactoryToCreateActivitySource()
        {
            // Arrange
            var mockFactory = Mock.Create<IActivitySourceFactory>();
            var mockActivitySource = Mock.Create<MockActivitySource>();
            
            Mock.Arrange(() => mockFactory.CreateActivitySource(Arg.IsAny<string>(), Arg.IsAny<string>()))
                .Returns(mockActivitySource).MustBeCalled();

            // Act
            NewRelicActivitySourceProxy.SetAndCreateRuntimeActivitySource(_activitySourceType, _activityKindType, mockFactory);

            // Assert
            Mock.Assert(mockFactory);
            var activitySource = _activitySourceField.GetValue(null);
            Assert.That(activitySource, Is.Not.Null);
            Assert.That(activitySource, Is.InstanceOf<RuntimeActivitySource>());
        }

        [Test]
        public void SetAndCreateRuntimeActivitySource_OnlyCreatesActivitySourceOnce()
        {
            // Act
            NewRelicActivitySourceProxy.SetAndCreateRuntimeActivitySource(_activitySourceType, _activityKindType);
            var firstActivitySource = _activitySourceField.GetValue(null);
            
            // Call again to verify it doesn't create a new one
            NewRelicActivitySourceProxy.SetAndCreateRuntimeActivitySource(_activitySourceType, _activityKindType);
            var secondActivitySource = _activitySourceField.GetValue(null);

            // Assert
            Assert.That(secondActivitySource, Is.SameAs(firstActivitySource), "Activity source should only be created once");
            Assert.That(_usingRuntimeActivitySourceField.GetValue(null), Is.EqualTo(1), "Flag should be set to 1");
        }

        [Test]
        public void SetAndCreateRuntimeActivitySource_WithDifferentFactory_DoesNotCreateNewSourceIfAlreadyCreated()
        {
            // Arrange
            NewRelicActivitySourceProxy.SetAndCreateRuntimeActivitySource(_activitySourceType, _activityKindType);
            var firstActivitySource = _activitySourceField.GetValue(null);
            
            var mockFactory = Mock.Create<IActivitySourceFactory>();
            
            // Act - Call with a factory, but it should be ignored since source is already created
            NewRelicActivitySourceProxy.SetAndCreateRuntimeActivitySource(_activitySourceType, _activityKindType, mockFactory);
            var secondActivitySource = _activitySourceField.GetValue(null);

            // Assert
            Assert.That(secondActivitySource, Is.SameAs(firstActivitySource), "Activity source should not be recreated");
            
            // Factory should not be called
            Mock.Assert(() => mockFactory.CreateActivitySource(Arg.IsAny<string>(), Arg.IsAny<string>()), 
                Occurs.Never());
        }

        [Test]
        public void SetAndCreateRuntimeActivitySource_DisposesExistingActivitySource()
        {
            // Arrange
            var mockActivitySource = Mock.Create<INewRelicActivitySource>();
            Mock.Arrange(() => mockActivitySource.Dispose()).MustBeCalled();
            _activitySourceField.SetValue(null, mockActivitySource);
            
            // We need to explicitly set the flag to 0 to allow the method to run
            _usingRuntimeActivitySourceField.SetValue(null, 0);

            // Act
            NewRelicActivitySourceProxy.SetAndCreateRuntimeActivitySource(_activitySourceType, _activityKindType);

            // Assert
            Mock.Assert(() => (mockActivitySource as IDisposable).Dispose(), Occurs.Once());
        }

        [Test]
        public void TryCreateActivity_WhenActivitySourceIsNull_ReturnsNull()
        {
            // Arrange
            _activitySourceField.SetValue(null, null);

            // Act
            var result = _proxy.TryCreateActivity("TestActivity", ActivityKind.Internal);

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void TryCreateActivity_WhenActivitySourceExists_CreatesActivity()
        {
            // Arrange
            var mockActivitySource = Mock.Create<INewRelicActivitySource>();
            var mockActivity = Mock.Create<INewRelicActivity>();
            
            Mock.Arrange(() => mockActivitySource.CreateActivity("TestActivity", ActivityKind.Internal))
                .Returns(mockActivity);
            
            _activitySourceField.SetValue(null, mockActivitySource);

            // Act
            var result = _proxy.TryCreateActivity("TestActivity", ActivityKind.Internal);

            // Assert
            Assert.That(result, Is.SameAs(mockActivity));
            Mock.Assert(() => mockActivitySource.CreateActivity("TestActivity", ActivityKind.Internal), Occurs.Once());
        }

        [Test]
        public void SetAndCreateRuntimeActivitySource_IsThreadSafe()
        {
            // Arrange
            int callCount = 0;
            bool[] threadStarted = new bool[10];
            bool[] threadFinished = new bool[10];
            Exception[] exceptions = new Exception[10];
            
            // Act - call the method from multiple threads simultaneously
            for (int i = 0; i < 10; i++)
            {
                int threadIndex = i;
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        threadStarted[threadIndex] = true;
                        NewRelicActivitySourceProxy.SetAndCreateRuntimeActivitySource(_activitySourceType, _activityKindType);
                        Interlocked.Increment(ref callCount);
                        threadFinished[threadIndex] = true;
                    }
                    catch (Exception ex)
                    {
                        exceptions[threadIndex] = ex;
                    }
                });
            }

            // Wait for all threads to complete (or timeout after 5 seconds)
            int maxWaitMs = 5000;
            int waitInterval = 50;
            int elapsedWaitTime = 0;
            while (elapsedWaitTime < maxWaitMs)
            {
                if (Array.TrueForAll(threadFinished, finished => finished))
                    break;
                
                Thread.Sleep(waitInterval);
                elapsedWaitTime += waitInterval;
            }

            // Assert
            Assert.That(exceptions.All(ex => ex == null), Is.True, "No exceptions should be thrown");
            Assert.That(callCount, Is.EqualTo(10), "All method calls should complete");
            Assert.That(_usingRuntimeActivitySourceField.GetValue(null), Is.EqualTo(1), "Flag should be set exactly once");
            
            // Activity source should be created exactly once
            var runtimeActivitySources = _activitySourceField.GetValue(null);
            Assert.That(runtimeActivitySources, Is.Not.Null);
        }
    }
}
