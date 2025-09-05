// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using NewRelic.Agent.Api;
using NewRelic.Agent.Core.Segments;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.OpenTelemetryBridge
{
    [TestFixture]
    public class RuntimeNewRelicActivityTests
    {
        private ActivityMock _mockActivity;
        private ISegment _mockSegment;

        [SetUp]
        public void Setup()
        {
            _mockActivity = Mock.Create<ActivityMock>();
            _mockSegment = Mock.Create<ISegment>();
        }

        [Test]
        public void Constructor_StoresActivityReference()
        {
            // Act
            var runtimeActivity = new RuntimeNewRelicActivity(_mockActivity);

            // Assert - We can't directly verify the private field, but we can test that methods on the activity are called
            Mock.ArrangeSet(() => _mockActivity.IsStopped = Arg.IsAny<bool>());
            Assert.DoesNotThrow(() => runtimeActivity.Stop());
        }

        [Test]
        public void IsStopped_ReturnsActivityIsStopped()
        {
            // Arrange
            Mock.Arrange(() => _mockActivity.IsStopped).Returns(true);
            var runtimeActivity = new RuntimeNewRelicActivity(_mockActivity);

            // Act
            var result = runtimeActivity.IsStopped;

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void SpanId_ReturnsFormattedSpanId()
        {
            // Arrange
            var mockSpanId = Mock.Create<SpanIdMock>();
            Mock.Arrange(() => _mockActivity.SpanId).Returns(mockSpanId);
            Mock.Arrange(() => mockSpanId.ToString()).Returns("test-span-id");
            var runtimeActivity = new RuntimeNewRelicActivity(_mockActivity);

            // Act
            var result = runtimeActivity.SpanId;

            // Assert
            Assert.That(result, Is.EqualTo("test-span-id"));
        }

        [Test]
        public void TraceId_ReturnsFormattedTraceId()
        {
            // Arrange
            var mockTraceId = Mock.Create<TraceIdMock>();
            Mock.Arrange(() => _mockActivity.TraceId).Returns(mockTraceId);
            Mock.Arrange(() => mockTraceId.ToString()).Returns("test-trace-id");
            var runtimeActivity = new RuntimeNewRelicActivity(_mockActivity);

            // Act
            var result = runtimeActivity.TraceId;

            // Assert
            Assert.That(result, Is.EqualTo("test-trace-id"));
        }

        [Test]
        public void DisplayName_ReturnsActivityDisplayName()
        {
               // Arrange
            Mock.Arrange(() => _mockActivity.DisplayName).Returns("TestActivity");
            var runtimeActivity = new RuntimeNewRelicActivity(_mockActivity);

            // Act
            var result = runtimeActivity.DisplayName;

            // Assert
            Assert.That(result, Is.EqualTo("TestActivity"));
        }

        [Test]
        public void Id_ReturnsActivityId()
        {
            // Arrange
            Mock.Arrange(() => _mockActivity.Id).Returns("TestActivityId");
            var runtimeActivity = new RuntimeNewRelicActivity(_mockActivity);
            // Act
            var result = runtimeActivity.Id;
            // Assert
            Assert.That(result, Is.EqualTo("TestActivityId"));
        }

        [Test]
        public void Id_ReturnsNull_WhenActivityIsNull()
        {
            // Arrange
            var runtimeActivity = new RuntimeNewRelicActivity(null);
            // Act
            var result = runtimeActivity.Id;
            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void Segment_Get_CallsGetSegmentFromActivity()
        {
            // Arrange
            Mock.Arrange(() => _mockActivity.GetCustomProperty(NewRelicActivitySourceProxy.SegmentCustomPropertyName))
                .Returns(_mockSegment);
            var runtimeActivity = new RuntimeNewRelicActivity(_mockActivity);

            // Act
            var result = runtimeActivity.GetSegment();

            // Assert
            Assert.That(result, Is.SameAs(_mockSegment));
        }

        [Test]
        public void Segment_Set_CallsSetCustomPropertyOnActivity()
        {
            // Arrange
            var runtimeActivity = new RuntimeNewRelicActivity(_mockActivity);

            // Act
            runtimeActivity.SetSegment(_mockSegment);

            // Assert
            Mock.Assert(() => _mockActivity.SetCustomProperty(NewRelicActivitySourceProxy.SegmentCustomPropertyName, _mockSegment), 
                Occurs.Once());
        }

        [Test]
        public void Start_CallsStartOnActivity()
        {
            // Arrange
            var runtimeActivity = new RuntimeNewRelicActivity(_mockActivity);

            // Act
            runtimeActivity.Start();

            // Assert
            Mock.Assert(() => _mockActivity.Start(), Occurs.Once());
        }

        [Test]
        public void Stop_CallsStopOnActivity()
        {
            // Arrange
            var runtimeActivity = new RuntimeNewRelicActivity(_mockActivity);

            // Act
            runtimeActivity.Stop();

            // Assert
            Mock.Assert(() => _mockActivity.Stop(), Occurs.Once());
        }

        [Test]
        public void Dispose_CallsDisposeOnActivity()
        {
            // Arrange
            var runtimeActivity = new RuntimeNewRelicActivity(_mockActivity);

            // Act
            runtimeActivity.Dispose();

            // Assert
            Mock.Assert(() => _mockActivity.Dispose(), Occurs.Once());
        }

        [Test]
        public void GetSegmentFromActivity_ReturnsSegmentFromCustomProperty()
        {
            // Arrange
            Mock.Arrange(() => _mockActivity.GetCustomProperty(NewRelicActivitySourceProxy.SegmentCustomPropertyName))
                .Returns(_mockSegment);

            // Act
            var result = RuntimeNewRelicActivity.GetSegmentFromActivity(_mockActivity);

            // Assert
            Assert.That(result, Is.SameAs(_mockSegment));
        }

        [Test]
        public void GetSegmentFromActivity_WhenPropertyNotSet_ReturnsNull()
        {
            // Arrange
            Mock.Arrange(() => _mockActivity.GetCustomProperty(NewRelicActivitySourceProxy.SegmentCustomPropertyName))
                .Returns((object)null);

            // Act
            var result = RuntimeNewRelicActivity.GetSegmentFromActivity(_mockActivity);

            // Assert
            Assert.That(result, Is.Null);
        }

        // Mock classes to replace dynamic
        public class ActivityMock
        {
            public virtual bool IsStopped { get; set; }
            public virtual SpanIdMock SpanId { get; set; }
            public virtual TraceIdMock TraceId { get; set; }
            public virtual string DisplayName { get; set; }
            public virtual string Id { get; set; }
            public virtual object GetCustomProperty(string propertyName) { return null; }
            public virtual void SetCustomProperty(string propertyName, object value) { }
            public virtual void Start() { }
            public virtual void Stop() { }
            public virtual void Dispose() { }
        }

        public class SpanIdMock
        {
            public override string ToString() { return string.Empty; }
        }

        public class TraceIdMock
        {
            public override string ToString() { return string.Empty; }
        }
    }
}
