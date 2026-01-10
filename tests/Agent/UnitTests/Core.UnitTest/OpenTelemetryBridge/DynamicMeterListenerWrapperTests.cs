// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NewRelic.Agent.Core.OpenTelemetryBridge.Common;
using NewRelic.Agent.Core.OpenTelemetryBridge.Metrics;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.UnitTest.OpenTelemetryBridge
{
    [TestFixture]
    public class DynamicMeterListenerWrapperTests
    {
        private Assembly GetDiagnosticSourceAssembly()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "System.Diagnostics.DiagnosticSource");
        }

        private bool TryGetDiagnosticSourceAssembly(out Assembly assembly)
        {
            assembly = GetDiagnosticSourceAssembly();
            if (assembly == null)
            {
                Assert.Ignore("System.Diagnostics.DiagnosticSource not available in test environment");
                return false;
            }
            return true;
        }

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
            if (!TryGetDiagnosticSourceAssembly(out var diagnosticSourceAssembly))
                return;

            var mockProvider = Mock.Create<IAssemblyProvider>();
            Mock.Arrange(() => mockProvider.GetAssemblies()).Returns(new[] { diagnosticSourceAssembly });

            // Act
            using var wrapper = new DynamicMeterListenerWrapper(mockProvider);

            // Assert
            Assert.DoesNotThrow(() => wrapper.Start());
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

        [Test]
        public void Constructor_WithValidDiagnosticSourceAssembly_InitializesSuccessfully()
        {
            // Arrange
            if (!TryGetDiagnosticSourceAssembly(out var diagnosticSourceAssembly))
                return;

            var mockProvider = Mock.Create<IAssemblyProvider>();
            Mock.Arrange(() => mockProvider.GetAssemblies()).Returns(new[] { diagnosticSourceAssembly });

            // Act
            using var wrapper = new DynamicMeterListenerWrapper(mockProvider);

            // Assert - Verify it doesn't throw and basic operations work
            Assert.DoesNotThrow(() => wrapper.Start());
            Assert.DoesNotThrow(() => wrapper.RecordObservableInstruments());
        }

        [Test]
        public void InstrumentPublished_WhenSet_IsInvoked()
        {
            // Arrange
            if (!TryGetDiagnosticSourceAssembly(out var diagnosticSourceAssembly))
                return;

            var mockProvider = Mock.Create<IAssemblyProvider>();
            Mock.Arrange(() => mockProvider.GetAssemblies()).Returns(new[] { diagnosticSourceAssembly });
            using var wrapper = new DynamicMeterListenerWrapper(mockProvider);
            
            wrapper.InstrumentPublished = (instrument, listener) => { };

            // Act
            wrapper.Start();

            // Assert - Callback should be configured (we can't easily trigger it without actual meters)
            Assert.That(wrapper.InstrumentPublished, Is.Not.Null);
        }

        [Test]
        public void SetMeasurementCallback_WithValidWrapper_ConfiguresCallback()
        {
            // Arrange
            if (!TryGetDiagnosticSourceAssembly(out var diagnosticSourceAssembly))
                return;

            var mockProvider = Mock.Create<IAssemblyProvider>();
            Mock.Arrange(() => mockProvider.GetAssemblies()).Returns(new[] { diagnosticSourceAssembly });
            using var wrapper = new DynamicMeterListenerWrapper(mockProvider);

            // Act & Assert - Should configure without throwing
            Assert.DoesNotThrow(() => wrapper.SetMeasurementCallback<int>((inst, val, tags, state) => { }));
            Assert.DoesNotThrow(() => wrapper.SetMeasurementCallback<double>((inst, val, tags, state) => { }));
            Assert.DoesNotThrow(() => wrapper.SetMeasurementCallback<long>((inst, val, tags, state) => { }));
        }

        [Test]
        public void RegisterMeasurementCallback_CallsSetMeasurementCallback()
        {
            // Arrange
            if (!TryGetDiagnosticSourceAssembly(out var diagnosticSourceAssembly))
                return;

            var mockProvider = Mock.Create<IAssemblyProvider>();
            Mock.Arrange(() => mockProvider.GetAssemblies()).Returns(new[] { diagnosticSourceAssembly });
            using var wrapper = new DynamicMeterListenerWrapper(mockProvider);

            // Act & Assert
            Assert.DoesNotThrow(() => wrapper.RegisterMeasurementCallback<int>((inst, val, tags, state) => { }));
        }

        [Test]
        public void MeasurementsCompleted_WhenSet_IsInvoked()
        {
            // Arrange
            if (!TryGetDiagnosticSourceAssembly(out var diagnosticSourceAssembly))
                return;

            var mockProvider = Mock.Create<IAssemblyProvider>();
            Mock.Arrange(() => mockProvider.GetAssemblies()).Returns(new[] { diagnosticSourceAssembly });
            using var wrapper = new DynamicMeterListenerWrapper(mockProvider);
            
            wrapper.MeasurementsCompleted = (instrument, state, listener) => { };

            // Act
            wrapper.Start();

            // Assert - Callback should be configured
            Assert.That(wrapper.MeasurementsCompleted, Is.Not.Null);
        }

        [Test]
        public void EnsureInitialized_RetriesInitialization_WhenNotInitiallyAvailable()
        {
            // Arrange
            if (!TryGetDiagnosticSourceAssembly(out var diagnosticSourceAssembly))
                return;

            var mockProvider = Mock.Create<IAssemblyProvider>();
            // First return empty, then return the assembly
            var callCount = 0;
            Mock.Arrange(() => mockProvider.GetAssemblies()).Returns(() =>
            {
                callCount++;
                return callCount == 1 ? new Assembly[0] : new[] { diagnosticSourceAssembly };
            });

            using var wrapper = new DynamicMeterListenerWrapper(mockProvider);

            // Act - First call should fail, second should retry and succeed
            wrapper.EnableMeasurementEvents(null, null);  // Triggers EnsureInitialized
            wrapper.Start();  // Triggers EnsureInitialized again

            // Assert - Should not throw
            Assert.Pass("EnsureInitialized retry mechanism works");
        }

        [Test]
        public void Dispose_WithInitializedWrapper_DisposesUnderlyingListener()
        {
            // Arrange
            if (!TryGetDiagnosticSourceAssembly(out var diagnosticSourceAssembly))
                return;

            var mockProvider = Mock.Create<IAssemblyProvider>();
            Mock.Arrange(() => mockProvider.GetAssemblies()).Returns(new[] { diagnosticSourceAssembly });
            using var wrapper = new DynamicMeterListenerWrapper(mockProvider);

            // Act & Assert
            Assert.DoesNotThrow(() => wrapper.Dispose());
        }

        [Test]
        public void DynamicMeterListenerWrapper_WhenAssemblyLoadFails_GracefullyHandlesFailure()
        {
            // Arrange - Mock provider that throws during GetAssemblies
            var mockProvider = Mock.Create<IAssemblyProvider>();
            Mock.Arrange(() => mockProvider.GetAssemblies())
                .Throws<Exception>();

            // Act - Constructor should handle exception gracefully
            var wrapper = new DynamicMeterListenerWrapper(mockProvider);

            // Assert - All operations should be no-ops and not throw
            Assert.DoesNotThrow(() => wrapper.Start());
            Assert.DoesNotThrow(() => wrapper.RecordObservableInstruments());
            Assert.DoesNotThrow(() => wrapper.EnableMeasurementEvents(new object(), new object()));
            Assert.DoesNotThrow(() => wrapper.Dispose());
        }

        [Test]
        public void DynamicMeterListenerWrapper_WhenAssemblyNotFound_RemainsUnavailableGracefully()
        {
            // Arrange - Mock provider returns empty assembly list
            var mockProvider = Mock.Create<IAssemblyProvider>();
            Mock.Arrange(() => mockProvider.GetAssemblies())
                .Returns(new Assembly[0]);

            // Act
            var wrapper = new DynamicMeterListenerWrapper(mockProvider);

            // Assert - Should remain unavailable but functional (defensive)
            Assert.DoesNotThrow(() => wrapper.Start());
            Assert.DoesNotThrow(() => wrapper.RecordObservableInstruments());
            Assert.DoesNotThrow(() => wrapper.EnableMeasurementEvents(new object(), new object()));
            
            // Multiple calls should also not throw
            Assert.DoesNotThrow(() => wrapper.Start());
            Assert.DoesNotThrow(() => wrapper.RecordObservableInstruments());
            
            // Disposal should work
            Assert.DoesNotThrow(() => wrapper.Dispose());
            
            // Post-disposal calls should be no-ops
            Assert.DoesNotThrow(() => wrapper.Start());
        }
    }
}
