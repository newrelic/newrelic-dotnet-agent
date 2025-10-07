// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NET10_0_OR_GREATER
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using NewRelic.Agent.Core.OpenTelemetryBridge;
using NUnit.Framework;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace CompositeTests.HybridAgent
{
    [TestFixture]
    public class ActivityBridgeHelpersTests
    {
        private ActivitySource _activitySource = null;
        private TracerProvider _openTelemetry;

        [SetUp]
        public void SetUp()
        {
            // Initialize an ActivitySource for testing purposes
            _activitySource = new ActivitySource("TestSource");

            _openTelemetry = Sdk.CreateTracerProviderBuilder()
                .AddSource("TestSource")
                .Build();

        }
        [TearDown]
        public void TearDown() {
            _activitySource?.Dispose();
            _openTelemetry?.Dispose();
            ActivityBridgeHelpers.Reset();
        }


        // add a test for  SetCurrentActivity
        [Test]
        public void SetCurrentActivity_Should_Set_Current_Activity()
        {
            // Arrange
            var activity1 = _activitySource.StartActivity("TestActivity1");
            var activity2 = _activitySource.StartActivity("TestActivity2"); // this activity gets set as the current activity when it starts

            // Act
            ActivityBridgeHelpers.SetCurrentActivity(activity1);

            // Assert
            var actualCurrentActivity = Activity.Current;
            Assert.That(actualCurrentActivity, Is.EqualTo(activity1), "Current activity should be set to the provided activity.");
        }

        [Test]
        public void SetCurrentActivity_Should_Clear_Current_Activity_When_Null()
        {
            // Arrange
            var activity1 = _activitySource.StartActivity("TestActivity1");

            // Act
            ActivityBridgeHelpers.SetCurrentActivity(null);

            // Assert
            Assert.That(Activity.Current, Is.Null, "Current activity should be cleared when null is passed.");
        }
        [Test]
        public void GetCurrentActivity_Should_Return_Current_Activity()
        {
            // Arrange
            var activity1 = _activitySource.StartActivity("TestActivity1");

            // Act
            var currentActivity = ActivityBridgeHelpers.GetCurrentActivity();

            // Assert
            Assert.That(currentActivity, Is.EqualTo(activity1), "GetCurrentActivity should return the currently set activity.");
        }
        [Test]
        public void GetCurrentActivity_Should_Return_Null_When_No_Current_Activity()
        {
            // Arrange
            System.Diagnostics.Activity.Current = null;

            // Act
            var currentActivity = ActivityBridgeHelpers.GetCurrentActivity();

            // Assert
            Assert.That(currentActivity, Is.Null, "GetCurrentActivity should return null when no current activity is set.");
        }

        private System.Diagnostics.Activity GetCurrentActivityViaReflection()
        {
            var diagnosticSourceAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "System.Diagnostics.DiagnosticSource");
            var activityType = diagnosticSourceAssembly?.GetType("System.Diagnostics.Activity");
            var currentProperty = activityType?.GetProperty("Current", BindingFlags.Public | BindingFlags.Static);
            return (System.Diagnostics.Activity)currentProperty?.GetValue(null);
        }
    }
}
#endif
