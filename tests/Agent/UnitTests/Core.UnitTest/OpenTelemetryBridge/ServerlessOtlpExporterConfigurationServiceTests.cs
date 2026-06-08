// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.OpenTelemetryBridge.Metrics;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.UnitTest.OpenTelemetryBridge;

[TestFixture]
public class ServerlessOtlpExporterConfigurationServiceTests
{
    private IConfigurationService _mockConfigService;
    private IConfiguration _mockConfig;
    private IServerlessModeDataTransportService _mockDataTransportService;
    private ServerlessOtlpExporterConfigurationService _service;

    [SetUp]
    public void SetUp()
    {
        _mockConfigService = Mock.Create<IConfigurationService>();
        _mockConfig = Mock.Create<IConfiguration>();
        _mockDataTransportService = Mock.Create<IServerlessModeDataTransportService>();

        Mock.Arrange(() => _mockConfigService.Configuration).Returns(_mockConfig);
        Mock.Arrange(() => _mockConfig.ApplicationNames).Returns(new[] { "TestLambdaApp" });

        _service = new ServerlessOtlpExporterConfigurationService(_mockConfigService, _mockDataTransportService);
    }

    [TearDown]
    public void TearDown()
    {
        _service?.Dispose();
    }

    [Test]
    public void GetOrCreateMeterProvider_CreatesMeterProviderOnFirstCall()
    {
        // Act
        var result = _service.GetOrCreateMeterProvider();

        // Assert
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public void GetOrCreateMeterProvider_ReturnsSameInstanceOnSubsequentCalls()
    {
        // Act
        var first = _service.GetOrCreateMeterProvider();
        var second = _service.GetOrCreateMeterProvider();

        // Assert
        Assert.That(first, Is.SameAs(second));
    }

    [Test]
    public void GetOrCreateMeterProvider_WithConnectionInfo_DelegatesToNoArgOverload()
    {
        // Arrange
        var mockConnectionInfo = Mock.Create<IConnectionInfo>();

        // Act
        var withArgs = _service.GetOrCreateMeterProvider(mockConnectionInfo, "some-guid");
        var withoutArgs = _service.GetOrCreateMeterProvider();

        // Assert — both return the same instance
        Assert.That(withArgs, Is.SameAs(withoutArgs));
    }

    [Test]
    public void GetOrCreateMeterProvider_WiresSetOtelPayloadFunc()
    {
        // Act
        _service.GetOrCreateMeterProvider();

        // Assert — SetOtelPayloadFunc was called on the data transport service
        Mock.Assert(() => _mockDataTransportService.SetOtelPayloadFunc(Arg.IsAny<Func<byte[]>>()), Occurs.Once());
    }

    [Test]
    public void GetOrCreateMeterProvider_CreatesHttpClient()
    {
        // Act
        _service.GetOrCreateMeterProvider();

        // Assert
        Assert.That(_service.HttpClient, Is.Not.Null);
    }

    [Test]
    public void RecreateMeterProvider_IsNoOp()
    {
        // Arrange
        _service.GetOrCreateMeterProvider();
        var providerBefore = _service.GetOrCreateMeterProvider();

        // Act
        _service.RecreateMeterProvider();
        var providerAfter = _service.GetOrCreateMeterProvider();

        // Assert — same instance, not recreated
        Assert.That(providerAfter, Is.SameAs(providerBefore));
    }

    [Test]
    public void Dispose_CleansUpResources()
    {
        // Arrange
        _service.GetOrCreateMeterProvider();
        Assert.That(_service.HttpClient, Is.Not.Null);

        // Act
        _service.Dispose();

        // Assert
        Assert.That(_service.HttpClient, Is.Null);
    }

    [Test]
    public void Dispose_BeforeCreation_DoesNotThrow()
    {
        // Act & Assert — disposing without ever creating the provider should not throw
        Assert.DoesNotThrow(() => _service.Dispose());
    }

    [Test]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        _service.GetOrCreateMeterProvider();

        // Act & Assert
        Assert.DoesNotThrow(() =>
        {
            _service.Dispose();
            _service.Dispose();
        });
    }
}
