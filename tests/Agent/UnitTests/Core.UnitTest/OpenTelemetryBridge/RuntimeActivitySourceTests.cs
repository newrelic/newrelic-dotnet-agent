// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;
using Telerik.JustMock;
using Telerik.JustMock.Helpers;

namespace NewRelic.Agent.Core.OpenTelemetryBridge
{
    [TestFixture]
    public class RuntimeActivitySourceTests
    {
        private Type _activitySourceType;
        private Type _activityKindType;
        private MockActivitySource _mockActivitySource;
        private MockActivity _mockActivity;

        [SetUp]
        public void Setup()
        {
            _activitySourceType = typeof(MockActivitySource);
            _activityKindType = typeof(MockActivityKind);

            _mockActivitySource = Mock.Create<MockActivitySource>("TestSource", "1.0");
            _mockActivity = Mock.Create<MockActivity>();

            // Mock the CreateActivity method to return our mock activity
            Mock.Arrange(() => _mockActivitySource.CreateActivity(Arg.IsAny<string>(), Arg.IsAny<MockActivityKind>()))
                .Returns(_mockActivity);
        }

        [TearDown]
        public void TearDown()
        {
            _mockActivitySource?.Dispose();
        }

        [Test]
        public void Constructor_InitializesActivitySource()
        {
            // Arrange
            var mockFactory = Mock.Create<MockActivitySourceFactory>();
            Mock.Arrange(() => mockFactory.CreateActivitySource("TestSource", "1.0"))
                .Returns(_mockActivitySource).MustBeCalled();

            // Act
            var runtimeSource = new RuntimeActivitySource("TestSource", "1.0", _activitySourceType, _activityKindType, mockFactory);

            // Assert
            Mock.Assert(mockFactory);
        }

        [Test]
        public void Dispose_CallsDisposeOnActivitySource()
        {
            // Arrange
            var mockFactory = Mock.Create<MockActivitySourceFactory>();
            Mock.Arrange(() => mockFactory.CreateActivitySource("TestSource", "1.0"))
                .Returns(_mockActivitySource);

            var runtimeSource = new RuntimeActivitySource("TestSource", "1.0", _activitySourceType, _activityKindType, mockFactory);

            // Act
            runtimeSource.Dispose();

            // Assert
            Assert.That(_mockActivitySource.IsDisposed, Is.True);
        }

        [Test]
        public void Dispose_WhenActivitySourceIsNull_DoesNotThrow()
        {
            // Arrange
            var mockFactory = Mock.Create<MockActivitySourceFactory>();
            Mock.Arrange(() => mockFactory.CreateActivitySource("TestSource", "1.0"))
                .Returns((MockActivitySource)null);

            var runtimeSource = new RuntimeActivitySource("TestSource", "1.0", _activitySourceType, _activityKindType, mockFactory);

            // Act & Assert
            Assert.DoesNotThrow(() => runtimeSource.Dispose());
        }

        [Test]
        public void CreateActivity_CallsStartActivityOnSource()
        {
            // Arrange
            var mockFactory = Mock.Create<MockActivitySourceFactory>();
            Mock.Arrange(() => mockFactory.CreateActivitySource("TestSource", "1.0"))
                .Returns(_mockActivitySource);

            var runtimeSource = new RuntimeActivitySource("TestSource", "1.0", _activitySourceType, _activityKindType, mockFactory);

            // Act
            var result = runtimeSource.CreateActivity("TestActivity", ActivityKind.Internal);

            // Assert
            Mock.Assert(() => _mockActivitySource.CreateActivity("TestActivity", MockActivityKind.Internal), Occurs.Once());
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.InstanceOf<RuntimeNewRelicActivity>());
        }
    }

    // Factory class to encapsulate the creation of activity sources
    public class MockActivitySourceFactory : IActivitySourceFactory
    {
        public virtual object CreateActivitySource(string name, string version)
        {
            return new MockActivitySource(name, version);
        }
    }
}
