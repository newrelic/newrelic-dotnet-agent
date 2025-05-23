// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Spans;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NUnit.Framework;
using System;
using NewRelic.Agent.Core.Metrics;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.OpenTelemetryBridge;
using Telerik.JustMock;
using NewRelic.Agent.Core.Transactions;
using Telerik.JustMock.Helpers;

namespace NewRelic.Agent.Core.Segments.Tests
{
    [TestFixture]
    public class SegmentTests
    {
        [Test]
        public void End_WithException_HasErrorData()
        {
            var segment = new Segment(TransactionSegmentStateHelpers.GetItransactionSegmentState(), new MethodCallData("Type", "Method", 1));

            segment.End(new Exception("Unhandled exception"));

            Assert.That(segment.ErrorData, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(segment.ErrorData.ErrorTypeName, Is.EqualTo("System.Exception"));
                Assert.That(segment.ErrorData.ErrorMessage, Is.EqualTo("Unhandled exception"));
            });
        }

        [Test]
        public void SetMessageBrokerDestination_SetsDestination_IfSegmentData_IsMessageBrokerSegmentData()
        {
            var segment = new Segment(TransactionSegmentStateHelpers.GetItransactionSegmentState(), new MethodCallData("Type", "Method", 1));
            var messageBrokerSegmentData = new MessageBrokerSegmentData("broker", "unknown", MetricNames.MessageBrokerDestinationType.Topic, MetricNames.MessageBrokerAction.Consume);
            segment.SetSegmentData(messageBrokerSegmentData);

            segment.SetMessageBrokerDestination("destination");

            Assert.That(((MessageBrokerSegmentData)segment.SegmentData).Destination, Is.EqualTo("destination"));
        }

        [Test]
        public void DurationOrZero_ReturnsZero_IfDurationIsNotSet()
        {
            var segment = new Segment(TransactionSegmentStateHelpers.GetItransactionSegmentState(), new MethodCallData("Type", "Method", 1));

            var duration = segment.DurationOrZero;

            Assert.That(duration, Is.EqualTo(TimeSpan.Zero));
        }
        [Test]
        public void DurationOrZero_ReturnsDuration_IfDurationIsSet()
        {
            var segment = new Segment(TransactionSegmentStateHelpers.GetItransactionSegmentState(), new MethodCallData("Type", "Method", 1), TimeSpan.Zero, TimeSpan.FromSeconds(1));

            var duration = segment.DurationOrZero;

            Assert.That(duration, Is.EqualTo(TimeSpan.FromSeconds(1)));
        }
        [Test]
        public void Misc_Segment_Setters()
        {
            var segment = new Segment(TransactionSegmentStateHelpers.GetItransactionSegmentState(), new MethodCallData("Type", "Method", 1));
            Assert.That(segment.IsLeaf, Is.False);
            segment.MakeLeaf();
            Assert.That(segment.IsLeaf, Is.True);

            segment.SetName("foo");
            Assert.That(segment.GetTransactionTraceName(), Is.EqualTo("foo"));

            Assert.That(segment.Combinable, Is.False);
            segment.MakeCombinable();
            Assert.That(segment.Combinable, Is.True);

            Assert.That(segment.IsExternal, Is.False);
        }

        [Test]
        public void NoOpSegment()
        {
            var segment = new NoOpSegment();
            Assert.That(segment.IsDone, Is.True);
            Assert.That(segment.IsValid, Is.False);
            Assert.That(segment.IsDone, Is.True);
            Assert.That(segment.DurationShouldBeDeductedFromParent, Is.False);
            Assert.That(segment.IsLeaf, Is.False);
            Assert.That(segment.IsExternal, Is.False);
            Assert.That(segment.SpanId, Is.Null);
            Assert.That(segment.SegmentData, Is.Not.Null);
            Assert.That(segment.AttribDefs, Is.Not.Null);
            Assert.That(segment.AttribValues, Is.Not.Null);
            Assert.That(segment.TypeName, Is.EqualTo(string.Empty));
            Assert.That(segment.UserCodeFunction, Is.EqualTo(string.Empty));
            Assert.That(segment.UserCodeNamespace, Is.EqualTo(string.Empty));
            Assert.That(segment.SegmentNameOverride, Is.Null);

            Assert.DoesNotThrow(() => segment.End());
            Assert.DoesNotThrow(() => segment.End(new Exception()));
            Assert.DoesNotThrow(() => segment.EndStackExchangeRedis());
            Assert.DoesNotThrow(() => segment.MakeCombinable());
            Assert.DoesNotThrow(() => segment.MakeLeaf());
            Assert.DoesNotThrow(() => segment.RemoveSegmentFromCallStack());
            Assert.DoesNotThrow(() => segment.SetMessageBrokerDestination("destination"));
            Assert.DoesNotThrow(() => segment.SetSegmentData(null));
            Assert.DoesNotThrow(() => segment.AddCustomAttribute("key", "value"));
            Assert.DoesNotThrow(() => segment.AddCloudSdkAttribute("key", "value"));
            Assert.DoesNotThrow(() => segment.SetName("name"));
            Assert.That(segment.GetCategory(), Is.EqualTo(string.Empty));
            Assert.That(segment.DurationOrZero, Is.EqualTo(TimeSpan.Zero));
        }

        [Test]
        public void TryGetActivityTraceId_ReturnsNull_WhenNoActivitySet()
        {
            // Arrange
            var transactionSegmentState = Mock.Create<ITransactionSegmentState>();
            Mock.Arrange(() => transactionSegmentState.CurrentManagedThreadId).Returns(1);
            Mock.Arrange(() => transactionSegmentState.GetRelativeTime()).Returns(TimeSpan.Zero);
            Mock.Arrange(() => transactionSegmentState.ParentSegmentId()).Returns((int?)null);
            Mock.Arrange(() => transactionSegmentState.CallStackPush(Arg.IsAny<Segment>())).Returns(1);

            var methodCallData = new MethodCallData("Type", "Method", 1);
            var segment = new Segment(transactionSegmentState, methodCallData);

            // Act
            var traceId = segment.TryGetActivityTraceId();

            // Assert
            Assert.That(traceId, Is.Null);
        }

        [Test]
        public void TryGetActivityTraceId_ReturnsTraceId_WhenActivitySet()
        {
            // Arrange
            var transactionSegmentState = Mock.Create<ITransactionSegmentState>();
            Mock.Arrange(() => transactionSegmentState.CurrentManagedThreadId).Returns(1);
            Mock.Arrange(() => transactionSegmentState.GetRelativeTime()).Returns(TimeSpan.Zero);
            Mock.Arrange(() => transactionSegmentState.ParentSegmentId()).Returns((int?)null);
            Mock.Arrange(() => transactionSegmentState.CallStackPush(Arg.IsAny<Segment>())).Returns(1);

            var methodCallData = new MethodCallData("Type", "Method", 1);
            var segment = new Segment(transactionSegmentState, methodCallData);

            var mockActivity = Mock.Create<INewRelicActivity>();
            Mock.Arrange(() => mockActivity.TraceId).Returns("trace-id-123");

            segment.SetActivity(mockActivity);

            // Act
            var traceId = segment.TryGetActivityTraceId();

            // Assert
            Assert.That(traceId, Is.EqualTo("trace-id-123"));
        }

        [Test]
        public void SpanId_ReturnsExistingSpanId_IfAlreadySet()
        {
            // Arrange
            var transactionSegmentState = Mock.Create<ITransactionSegmentState>();
            Mock.Arrange(() => transactionSegmentState.CurrentManagedThreadId).Returns(1);
            Mock.Arrange(() => transactionSegmentState.GetRelativeTime()).Returns(TimeSpan.Zero);
            Mock.Arrange(() => transactionSegmentState.ParentSegmentId()).Returns((int?)null);
            Mock.Arrange(() => transactionSegmentState.CallStackPush(Arg.IsAny<Segment>())).Returns(1);

            var methodCallData = new MethodCallData("Type", "Method", 1);
            var segment = new Segment(transactionSegmentState, methodCallData);

            // Set _spanId via property
            segment.SpanId = "existing-span-id";

            // Act
            var result = segment.SpanId;

            // Assert
            Assert.That(result, Is.EqualTo("existing-span-id"));
        }

        [Test]
        public void SpanId_UsesActivitySpanId_IfNotSetAndActivityPresent()
        {
            // Arrange
            var transactionSegmentState = Mock.Create<ITransactionSegmentState>();
            Mock.Arrange(() => transactionSegmentState.CurrentManagedThreadId).Returns(1);
            Mock.Arrange(() => transactionSegmentState.GetRelativeTime()).Returns(TimeSpan.Zero);
            Mock.Arrange(() => transactionSegmentState.ParentSegmentId()).Returns((int?)null);
            Mock.Arrange(() => transactionSegmentState.CallStackPush(Arg.IsAny<Segment>())).Returns(1);

            var methodCallData = new MethodCallData("Type", "Method", 1);
            var segment = new Segment(transactionSegmentState, methodCallData);

            var mockActivity = Mock.Create<INewRelicActivity>();
            Mock.Arrange(() => mockActivity.SpanId).Returns("activity-span-id");
            segment.SetActivity(mockActivity);

            // Act
            var result = segment.SpanId;

            // Assert
            Assert.That(result, Is.EqualTo("activity-span-id"));
            // Should be cached
            Assert.That(segment.SpanId, Is.EqualTo("activity-span-id"));
        }

        [Test]
        public void SpanId_GeneratesNewGuid_IfNotSetAndNoActivity()
        {
            // Arrange
            var transactionSegmentState = Mock.Create<ITransactionSegmentState>();
            Mock.Arrange(() => transactionSegmentState.CurrentManagedThreadId).Returns(1);
            Mock.Arrange(() => transactionSegmentState.GetRelativeTime()).Returns(TimeSpan.Zero);
            Mock.Arrange(() => transactionSegmentState.ParentSegmentId()).Returns((int?)null);
            Mock.Arrange(() => transactionSegmentState.CallStackPush(Arg.IsAny<Segment>())).Returns(1);

            var methodCallData = new MethodCallData("Type", "Method", 1);
            var segment = new Segment(transactionSegmentState, methodCallData);

            // Act
            var result = segment.SpanId;

            // Assert
            Assert.That(result, Is.Not.Null.And.Not.Empty);
            // Should be cached
            Assert.That(segment.SpanId, Is.EqualTo(result));
        }

        [Test]
        public void End_CallsStop_WhenActivityIsNotNullAndNotStopped()
        {
            // Arrange
            var transactionSegmentState = Mock.Create<ITransactionSegmentState>();
            Mock.Arrange(() => transactionSegmentState.CurrentManagedThreadId).Returns(1);
            Mock.Arrange(() => transactionSegmentState.GetRelativeTime()).Returns(TimeSpan.Zero);
            Mock.Arrange(() => transactionSegmentState.ParentSegmentId()).Returns((int?)null);
            Mock.Arrange(() => transactionSegmentState.CallStackPush(Arg.IsAny<Segment>())).Returns(1);
            Mock.Arrange(() => transactionSegmentState.ErrorService).Returns(Mock.Create<IErrorService>());

            var methodCallData = new MethodCallData("Type", "Method", 1);
            var segment = new Segment(transactionSegmentState, methodCallData);

            var mockActivity = Mock.Create<INewRelicActivity>();
            Mock.Arrange(() => mockActivity.IsStopped).Returns(false);
            bool stopCalled = false;
            Mock.Arrange(() => mockActivity.Stop()).DoInstead(() => stopCalled = true);

            segment.SetActivity(mockActivity);

            // Act
            segment.End();

            // Assert
            Assert.That(stopCalled, Is.True);
        }

        [Test]
        public void End_DoesNotCallStop_WhenActivityIsAlreadyStopped()
        {
            // Arrange
            var transactionSegmentState = Mock.Create<ITransactionSegmentState>();
            Mock.Arrange(() => transactionSegmentState.CurrentManagedThreadId).Returns(1);
            Mock.Arrange(() => transactionSegmentState.GetRelativeTime()).Returns(TimeSpan.Zero);
            Mock.Arrange(() => transactionSegmentState.ParentSegmentId()).Returns((int?)null);
            Mock.Arrange(() => transactionSegmentState.CallStackPush(Arg.IsAny<Segment>())).Returns(1);
            Mock.Arrange(() => transactionSegmentState.ErrorService).Returns(Mock.Create<IErrorService>());

            var methodCallData = new MethodCallData("Type", "Method", 1);
            var segment = new Segment(transactionSegmentState, methodCallData);

            var mockActivity = Mock.Create<INewRelicActivity>();
            Mock.Arrange(() => mockActivity.IsStopped).Returns(true);
            bool stopCalled = false;
            Mock.Arrange(() => mockActivity.Stop()).DoInstead(() => stopCalled = true);

            segment.SetActivity(mockActivity);

            // Act
            segment.End();

            // Assert
            Assert.That(stopCalled, Is.False);
        }
    }
}
