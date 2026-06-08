// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Core.Fixtures;
using NewRelic.Agent.Core.SharedInterfaces;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.DataTransport;

[TestFixture]
public class ServerlessModeDataTransportServiceTests
{
    private ServerlessModeDataTransportService _service;
    private IDateTimeStatic _dateTimeStatic;
    private IServerlessModePayloadManager _payloadManager;
    private DisposableCollection _disposableCollection;

    [SetUp]
    public void SetUp()
    {
        _disposableCollection = new DisposableCollection();
        _dateTimeStatic = Mock.Create<IDateTimeStatic>();
        _payloadManager = Mock.Create<IServerlessModePayloadManager>();

        Mock.Arrange(() => _dateTimeStatic.UtcNow).Returns(DateTime.UtcNow);
        Mock.Arrange(() => _payloadManager.BuildPayload(Arg.IsAny<WireData>())).Returns("{}");
        Mock.Arrange(() => _payloadManager.WritePayload(Arg.IsAny<string>(), Arg.IsAny<string>())).DoNothing();

        _service = new ServerlessModeDataTransportService(_dateTimeStatic, _payloadManager);
        _disposableCollection.Add(new ConfigurationAutoResponder());
    }

    [TearDown]
    public void TearDown()
    {
        _service?.Dispose();
        _disposableCollection?.Dispose();
    }

    // ── FlushData: traditional telemetry path ──────────────────────────────

    [Test]
    public void FlushData_WithMissingTransactionId_ReturnsFalseAndLogsError()
    {
        var result = _service.FlushData(null);

        Assert.That(result, Is.False);
        Mock.Assert(() => _payloadManager.WritePayload(Arg.IsAny<string>(), Arg.IsAny<string>()), Occurs.Never());
    }

    [Test]
    public void FlushData_WithUnknownTransactionId_AndNoOtelPayloadFunc_ReturnsFalse()
    {
        var result = _service.FlushData("unknown-txn");

        Assert.That(result, Is.False);
        Mock.Assert(() => _payloadManager.WritePayload(Arg.IsAny<string>(), Arg.IsAny<string>()), Occurs.Never());
    }

    [Test]
    public void FlushData_WithKnownTransactionId_BuildsAndWritesPayload()
    {
        _service.Send(new List<CustomEventWireModel>(), "txn-1");

        var result = _service.FlushData("txn-1");

        Assert.That(result, Is.True);
        Mock.Assert(() => _payloadManager.BuildPayload(Arg.IsAny<WireData>()), Occurs.Once());
        Mock.Assert(() => _payloadManager.WritePayload(Arg.IsAny<string>(), Arg.IsAny<string>()), Occurs.Once());
    }

    // ── FlushData: OTel-only path ──────────────────────────────────────────

    [Test]
    public void FlushData_WithUnknownTransactionId_AndOtelPayloadFuncReturningBytes_WritesPayload()
    {
        // Arrange — simulate ForceFlush capturing some protobuf bytes
        var fakeProtoBytes = new byte[] { 0x0a, 0x06, 0x12, 0x04, 0x74, 0x65, 0x73, 0x74 };
        _service.SetOtelPayloadFunc(() => fakeProtoBytes);

        // Act — transactionId has no traditional telemetry
        var result = _service.FlushData("otel-only-txn");

        // Assert — payload still produced when OTel bytes exist
        Assert.That(result, Is.True);
        Mock.Assert(() => _payloadManager.BuildPayload(Arg.IsAny<WireData>()), Occurs.Once());
        Mock.Assert(() => _payloadManager.WritePayload(Arg.IsAny<string>(), Arg.IsAny<string>()), Occurs.Once());
    }

    [Test]
    public void FlushData_WithUnknownTransactionId_AndOtelPayloadFuncReturningNull_ReturnsFalse()
    {
        // Arrange — func set but returns null (nothing captured this invocation)
        _service.SetOtelPayloadFunc(() => null);

        var result = _service.FlushData("otel-only-txn");

        // Assert — empty payload skipped
        Assert.That(result, Is.False);
        Mock.Assert(() => _payloadManager.WritePayload(Arg.IsAny<string>(), Arg.IsAny<string>()), Occurs.Never());
    }

    [Test]
    public void FlushData_OtelPayload_IncludedUnderOtlpPayloadKey()
    {
        // Arrange
        WireData capturedData = null;
        Mock.Arrange(() => _payloadManager.BuildPayload(Arg.IsAny<WireData>()))
            .DoInstead((WireData d) => capturedData = d)
            .Returns("{}");

        var fakeProtoBytes = new byte[] { 0x0a, 0x01, 0x02, 0x03 };
        _service.SetOtelPayloadFunc(() => fakeProtoBytes);

        // Act
        _service.FlushData("txn-otel");

        // Assert — otlp_payload key present; value is a single-element object[] containing the byte[]
        Assert.That(capturedData, Is.Not.Null);
        Assert.That(capturedData.ContainsKey("otlp_payload"), Is.True);
        Assert.That(capturedData["otlp_payload"], Has.Length.EqualTo(1));
        Assert.That(capturedData["otlp_payload"][0], Is.EqualTo(fakeProtoBytes));
    }

    [Test]
    public void FlushData_OtelPayloadFunc_CalledOncePerInvocation()
    {
        // Arrange
        var callCount = 0;
        _service.SetOtelPayloadFunc(() =>
        {
            callCount++;
            return new byte[] { 0x01 };
        });

        // Act
        _service.FlushData("txn-1");
        _service.FlushData("txn-2");

        // Assert — called exactly once per FlushData
        Assert.That(callCount, Is.EqualTo(2));
    }

    // ── FlushData: mixed path ──────────────────────────────────────────────

    [Test]
    public void FlushData_OtelPayload_AppendedToExistingTelemetry()
    {
        // Arrange
        WireData capturedData = null;
        Mock.Arrange(() => _payloadManager.BuildPayload(Arg.IsAny<WireData>()))
            .DoInstead((WireData d) => capturedData = d)
            .Returns("{}");

        _service.Send(new List<CustomEventWireModel>(), "txn-mixed");

        var fakeProtoBytes = new byte[] { 0x0a, 0x02, 0x08, 0x01 };
        _service.SetOtelPayloadFunc(() => fakeProtoBytes);

        // Act
        _service.FlushData("txn-mixed");

        // Assert — both traditional telemetry key and otlp_payload present
        Assert.That(capturedData, Is.Not.Null);
        Assert.That(capturedData.ContainsKey("custom_event_data"), Is.True);
        Assert.That(capturedData.ContainsKey("otlp_payload"), Is.True);
        Assert.That(capturedData["otlp_payload"][0], Is.EqualTo(fakeProtoBytes));
    }

    // ── SetOtelPayloadFunc ─────────────────────────────────────────────────

    [Test]
    public void SetOtelPayloadFunc_WhenNotSet_FlushDoesNotThrow()
    {
        // Arrange — no payload func set (field stays null)
        _service.Send(new List<CustomEventWireModel>(), "txn-no-otel");

        // Act & Assert
        Assert.DoesNotThrow(() => _service.FlushData("txn-no-otel"));
    }
}
