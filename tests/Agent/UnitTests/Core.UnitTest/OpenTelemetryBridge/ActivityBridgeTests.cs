// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using NewRelic.Agent.Api;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.OpenTelemetryBridge.Tracing;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.OpenTelemetryBridge
{
    [TestFixture]
    public class ActivityBridgeTests
    {
        private IAgent _mockAgent;
        private IErrorService _mockErrorService;
        private IConfiguration _mockConfig;

        [SetUp]
        public void SetUp()
        {
            _mockAgent = Mock.Create<IAgent>();
            _mockErrorService = Mock.Create<IErrorService>();
            _mockConfig = Mock.Create<IConfiguration>();
            Mock.Arrange(() => _mockAgent.Configuration).Returns(_mockConfig);
            Mock.Arrange(() => _mockConfig.OpenTelemetryEnabled).Returns(true);

            Mock.Arrange(() => _mockConfig.OpenTelemetryTracingEnabled).Returns(true);
        }

        [Test]
        public void Start_ReturnsTrue_WhenOpenTelemetryBridgeDisabled()
        {
            // Arrange
            Mock.Arrange(() => _mockConfig.OpenTelemetryEnabled).Returns(false);
            Mock.Arrange(() => _mockConfig.OpenTelemetryTracingEnabled).Returns(true);

            var bridge = new ActivityBridge(_mockAgent, _mockErrorService);
            // Act
            var result = bridge.Start();
            // Assert
            Assert.That(result, Is.True, "Start should return true if OpenTelemetry is disabled.");
        }

        [Test]
        public void Start_ReturnsTrue_WhenOpenTelemetryBridgeEnabled_AndTracingDisabled()
        {
            // Arrange
            Mock.Arrange(() => _mockConfig.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _mockConfig.OpenTelemetryTracingEnabled).Returns(false);

            var bridge = new ActivityBridge(_mockAgent, _mockErrorService);

            // Act
            var result = bridge.Start();

            // Assert
            Assert.That(result, Is.True, "Start should return true if OpenTelemetry is disabled.");
        }

        [Test]
        public void Start_ReturnsFalse_WhenActivityListenerAlreadyCreated()
        {
            // Arrange
            var bridge = new ActivityBridge(_mockAgent, _mockErrorService);

            // Use reflection only to set up the test, not to access private members in production code.
            var activityListenerField = typeof(ActivityBridge).GetField("_activityListener", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            activityListenerField.SetValue(bridge, new object());

            // Act
            var result = bridge.Start();

            // Assert
            Assert.That(result, Is.False, "Start should return false if the activity listener has already been created.");
        }
    }
}
