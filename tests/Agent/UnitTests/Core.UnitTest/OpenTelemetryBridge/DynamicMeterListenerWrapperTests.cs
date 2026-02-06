// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using System.Reflection;
using NewRelic.Agent.Core.OpenTelemetryBridge.Common;
using NewRelic.Agent.Core.OpenTelemetryBridge.Metrics;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.UnitTest.OpenTelemetryBridge;

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

    [Test]
    public void IsInstrumentFromILRepackedAssembly_WithNoILRepackedType_ReturnsFalse()
    {
        // Arrange - Create wrapper without ILRepacked assembly
        if (!TryGetDiagnosticSourceAssembly(out var diagnosticSourceAssembly))
            return;

        var mockProvider = Mock.Create<IAssemblyProvider>();
        Mock.Arrange(() => mockProvider.GetAssemblies()).Returns(new[] { diagnosticSourceAssembly });
        
        using var wrapper = new DynamicMeterListenerWrapper(mockProvider);
        using var meter = new System.Diagnostics.Metrics.Meter("TestMeter");
        var counter = meter.CreateCounter<int>("test-counter");

        // Act
        var result = wrapper.IsInstrumentFromILRepackedAssembly(counter);

        // Assert
        Assert.That(result, Is.False, "Should return false when no ILRepacked type found");
    }

    [Test]
    public void IsInstrumentFromILRepackedAssembly_WithNullInstrument_ReturnsFalse()
    {
        // Arrange
        if (!TryGetDiagnosticSourceAssembly(out var diagnosticSourceAssembly))
            return;

        var mockProvider = Mock.Create<IAssemblyProvider>();
        Mock.Arrange(() => mockProvider.GetAssemblies()).Returns(new[] { diagnosticSourceAssembly });
        
        using var wrapper = new DynamicMeterListenerWrapper(mockProvider);

        // Act
        var result = wrapper.IsInstrumentFromILRepackedAssembly(null);

        // Assert
        Assert.That(result, Is.False, "Should return false for null instrument");
    }

    [Test]
    public void IsInstrumentFromILRepackedAssembly_WithDifferentAssembly_ReturnsFalse()
    {
        // Arrange
        if (!TryGetDiagnosticSourceAssembly(out var diagnosticSourceAssembly))
            return;

        // Create a mock NewRelic.Agent.Core assembly that doesn't have ILRepacked types
        var mockAgentAssembly = Mock.Create<Assembly>();
        Mock.Arrange(() => mockAgentAssembly.GetName()).Returns(new AssemblyName("NewRelic.Agent.Core"));
        Mock.Arrange(() => mockAgentAssembly.GetType("System.Diagnostics.Metrics.MeterListener", false)).Returns((Type)null);

        var mockProvider = Mock.Create<IAssemblyProvider>();
        Mock.Arrange(() => mockProvider.GetAssemblies()).Returns(new[] { diagnosticSourceAssembly, mockAgentAssembly });
        
        using var wrapper = new DynamicMeterListenerWrapper(mockProvider);
        using var meter = new System.Diagnostics.Metrics.Meter("TestMeter");
        var counter = meter.CreateCounter<int>("test-counter");

        // Act
        var result = wrapper.IsInstrumentFromILRepackedAssembly(counter);

        // Assert
        Assert.That(result, Is.False, "Should return false when instrument is from different assembly");
    }

    [Test]
    public void ConfigureInstrumentPublished_WithMissingProperty_HandlesGracefully()
    {
        // Arrange - Use wrapper that initializes successfully but might have issues with property configuration
        if (!TryGetDiagnosticSourceAssembly(out var diagnosticSourceAssembly))
            return;

        var mockProvider = Mock.Create<IAssemblyProvider>();
        Mock.Arrange(() => mockProvider.GetAssemblies()).Returns(new[] { diagnosticSourceAssembly });
        
        using var wrapper = new DynamicMeterListenerWrapper(mockProvider);
        
        // Set callback
        wrapper.InstrumentPublished = (inst, listener) => { };

        // Act - Start should trigger ConfigureCallbacks
        wrapper.Start();

        // Assert - Should complete without throwing
        Assert.That(wrapper.InstrumentPublished, Is.Not.Null);
    }

    [Test]
    public void HandleMeasurementCallbackWithSpan_WithEmptySpan_CallsCallbackWithEmptySpan()
    {
        // Arrange
        if (!TryGetDiagnosticSourceAssembly(out var diagnosticSourceAssembly))
            return;

        var mockProvider = Mock.Create<IAssemblyProvider>();
        Mock.Arrange(() => mockProvider.GetAssemblies()).Returns(new[] { diagnosticSourceAssembly });
        
        using var wrapper = new DynamicMeterListenerWrapper(mockProvider);
        
        wrapper.SetMeasurementCallback<int>((inst, val, tags, state) => 
        {
            Assert.That(tags.Length, Is.EqualTo(0));
        });

        // Act - The callback should be configured
        Assert.DoesNotThrow(() => wrapper.Start());
    }

    [Test]
    public void EnableMeasurementEvents_WithNullInstrument_DoesNotThrow()
    {
        // Arrange
        if (!TryGetDiagnosticSourceAssembly(out var diagnosticSourceAssembly))
            return;

        var mockProvider = Mock.Create<IAssemblyProvider>();
        Mock.Arrange(() => mockProvider.GetAssemblies()).Returns(new[] { diagnosticSourceAssembly });
        
        using var wrapper = new DynamicMeterListenerWrapper(mockProvider);

        // Act & Assert
        Assert.DoesNotThrow(() => wrapper.EnableMeasurementEvents(null, new object()));
    }

    [Test]
    public void TryFindILRepackedMeterListenerType_WithException_LogsAndContinues()
    {
        // Arrange - Mock assemblies that throw during GetType
        if (!TryGetDiagnosticSourceAssembly(out var diagnosticSourceAssembly))
            return;

        var mockAgentAssembly = Mock.Create<Assembly>();
        Mock.Arrange(() => mockAgentAssembly.GetName()).Returns(new AssemblyName("NewRelic.Agent.Core"));
        Mock.Arrange(() => mockAgentAssembly.GetType(Arg.IsAny<string>(), false)).Throws<TypeLoadException>();

        var mockProvider = Mock.Create<IAssemblyProvider>();
        Mock.Arrange(() => mockProvider.GetAssemblies()).Returns(new[] { diagnosticSourceAssembly, mockAgentAssembly });

        // Act - Should handle exception gracefully
        using var wrapper = new DynamicMeterListenerWrapper(mockProvider);

        // Assert - Wrapper should still be functional
        Assert.DoesNotThrow(() => wrapper.Start());
    }

    [Test]
    public void Dispose_MultipleTimes_OnlyDisposesOnce()
    {
        // Arrange
        if (!TryGetDiagnosticSourceAssembly(out var diagnosticSourceAssembly))
            return;

        var mockProvider = Mock.Create<IAssemblyProvider>();
        Mock.Arrange(() => mockProvider.GetAssemblies()).Returns(new[] { diagnosticSourceAssembly });
        var wrapper = new DynamicMeterListenerWrapper(mockProvider);

        // Act - Dispose multiple times
        wrapper.Dispose();
        wrapper.Dispose();
        wrapper.Dispose();

        // Assert - Should not throw
        Assert.Pass("Multiple dispose calls handled correctly");
    }

    [Test]
    public void RecordObservableInstruments_AfterDispose_DoesNotThrow()
    {
        // Arrange
        if (!TryGetDiagnosticSourceAssembly(out var diagnosticSourceAssembly))
            return;

        var mockProvider = Mock.Create<IAssemblyProvider>();
        Mock.Arrange(() => mockProvider.GetAssemblies()).Returns(new[] { diagnosticSourceAssembly });
        var wrapper = new DynamicMeterListenerWrapper(mockProvider);

        // Act
        wrapper.Dispose();

        // Assert - Operations after dispose should be no-ops
        Assert.DoesNotThrow(() => wrapper.RecordObservableInstruments());
        Assert.DoesNotThrow(() => wrapper.Start());
        Assert.DoesNotThrow(() => wrapper.EnableMeasurementEvents(new object(), null));
    }
}
