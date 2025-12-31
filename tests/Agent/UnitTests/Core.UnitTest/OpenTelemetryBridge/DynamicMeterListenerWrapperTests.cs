// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NewRelic.Agent.Core.OpenTelemetryBridge;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.UnitTest.OpenTelemetryBridge
{
    [TestFixture]
    public class DynamicMeterListenerWrapperTests
    {
        [Test]
        public void Constructor_WithMissingAssembly_CreatesNoOpWrapper()
        {
            // Arrange
            var mockProvider = Mock.Create<IAssemblyProvider>();
            Mock.Arrange(() => mockProvider.GetAssemblies()).Returns(new Assembly[0]);

            // Act
            var wrapper = new DynamicMeterListenerWrapper(mockProvider);

            // Assert - Should not throw
            Assert.DoesNotThrow(() => wrapper.Start());
            Assert.DoesNotThrow(() => wrapper.EnableMeasurementEvents(null, null));
            Assert.DoesNotThrow(() => wrapper.RecordObservableInstruments());
        }

        [Test]
        public void Constructor_WithValidAssembly_CreatesWrapper()
        {
            // Arrange
            var mockProvider = Mock.Create<IAssemblyProvider>();
            var diagnosticSourceAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "System.Diagnostics.DiagnosticSource");

            if (diagnosticSourceAssembly == null)
            {
                Assert.Ignore("System.Diagnostics.DiagnosticSource not available in test environment");
                return;
            }

            Mock.Arrange(() => mockProvider.GetAssemblies()).Returns(new[] { diagnosticSourceAssembly });

            // Act & Assert - Should not throw
            Assert.DoesNotThrow(() => new DynamicMeterListenerWrapper(mockProvider));
        }

        [Test]
        public void Start_WhenNotAvailable_DoesNotThrow()
        {
            // Arrange
            var mockProvider = Mock.Create<IAssemblyProvider>();
            Mock.Arrange(() => mockProvider.GetAssemblies()).Returns(new Assembly[0]);
            var wrapper = new DynamicMeterListenerWrapper(mockProvider);

            // Act & Assert
            Assert.DoesNotThrow(() => wrapper.Start());
        }

        [Test]
        public void EnableMeasurementEvents_WhenNotAvailable_DoesNotThrow()
        {
            // Arrange
            var mockProvider = Mock.Create<IAssemblyProvider>();
            Mock.Arrange(() => mockProvider.GetAssemblies()).Returns(new Assembly[0]);
            var wrapper = new DynamicMeterListenerWrapper(mockProvider);

            // Act & Assert
            Assert.DoesNotThrow(() => wrapper.EnableMeasurementEvents(new object(), new object()));
        }

        [Test]
        public void RecordObservableInstruments_WhenNotAvailable_DoesNotThrow()
        {
            // Arrange
            var mockProvider = Mock.Create<IAssemblyProvider>();
            Mock.Arrange(() => mockProvider.GetAssemblies()).Returns(new Assembly[0]);
            var wrapper = new DynamicMeterListenerWrapper(mockProvider);

            // Act & Assert
            Assert.DoesNotThrow(() => wrapper.RecordObservableInstruments());
        }

        [Test]
        public void SetMeasurementCallback_WhenNotAvailable_DoesNotThrow()
        {
            // Arrange
            var mockProvider = Mock.Create<IAssemblyProvider>();
            Mock.Arrange(() => mockProvider.GetAssemblies()).Returns(new Assembly[0]);
            var wrapper = new DynamicMeterListenerWrapper(mockProvider);

            // Act & Assert
            Assert.DoesNotThrow(() => wrapper.SetMeasurementCallback<int>((inst, val, tags, state) => { }));
        }

        [Test]
        public void Dispose_DoesNotThrow()
        {
            // Arrange
            var mockProvider = Mock.Create<IAssemblyProvider>();
            Mock.Arrange(() => mockProvider.GetAssemblies()).Returns(new Assembly[0]);
            var wrapper = new DynamicMeterListenerWrapper(mockProvider);

            // Act & Assert
            Assert.DoesNotThrow(() => wrapper.Dispose());
        }
    }
}
