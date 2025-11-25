// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using NewRelic.Agent.Api;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.OpenTelemetryBridge;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.UnitTests.Core.UnitTest.OpenTelemetryBridge
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
            // enable globally
            Mock.Arrange(() => _mockConfig.OpenTelemetryBridgeEnabled).Returns(true);

            Mock.Arrange(() => _mockConfig.OpenTelemetryBridgeTracingEnabled).Returns(true);
        }

        [Test]
        public void Start_ReturnsFalse_WhenDiagnosticSourceAssemblyNotLoaded()
        {
            // Arrange
            // This test is only valid if System.Diagnostics.DiagnosticSource is not loaded in the current AppDomain.
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            var diagnosticSource = loadedAssemblies.FirstOrDefault(a => a.GetName().Name == "System.Diagnostics.DiagnosticSource");
            if (diagnosticSource != null)
            {
                Assert.Ignore("System.Diagnostics.DiagnosticSource is loaded in this AppDomain, so this test cannot be executed.");
            }

            var bridge = new ActivityBridge(_mockAgent, _mockErrorService);
            var result = bridge.Start();
            Assert.That(result, Is.False, "Start should return false if DiagnosticSource is not loaded.");
        }

        [Test]
        public void Start_ReturnsTrue_WhenOpenTelemetryBridgeTracingDisabled()
        {
            // Arrange
            Mock.Arrange(() => _mockConfig.OpenTelemetryBridgeTracingEnabled).Returns(false);

            var bridge = new ActivityBridge(_mockAgent, _mockErrorService);

            // Act
            var result = bridge.Start();

            // Assert
            Assert.That(result, Is.True, "Start should return true if OpenTelemetryBridgeTracing is disabled.");
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
