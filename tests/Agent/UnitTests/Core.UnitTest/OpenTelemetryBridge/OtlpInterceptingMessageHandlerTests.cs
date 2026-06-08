// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NewRelic.Agent.Core.OpenTelemetryBridge.Metrics;
using NUnit.Framework;

namespace NewRelic.Agent.Core.UnitTest.OpenTelemetryBridge;

[TestFixture]
public class OtlpInterceptingMessageHandlerTests
{
    private OtlpInterceptingMessageHandler _handler;
    private HttpClient _httpClient;

    [SetUp]
    public void SetUp()
    {
        _handler = new OtlpInterceptingMessageHandler();
        _httpClient = new HttpClient(_handler)
        {
            BaseAddress = new System.Uri("https://localhost/")
        };
    }

    [TearDown]
    public void TearDown()
    {
        _httpClient?.Dispose();
        _handler?.Dispose();
    }

    [Test]
    public async Task SendAsync_CapturesRequestBodyBytes()
    {
        // Arrange
        var expectedBytes = new byte[] { 0x0a, 0x06, 0x12, 0x04, 0x74, 0x65, 0x73, 0x74 };
        var content = new ByteArrayContent(expectedBytes);

        // Act
        await _httpClient.PostAsync("/v1/metrics", content);
        var captured = _handler.Drain();

        // Assert
        Assert.That(captured, Is.EqualTo(expectedBytes));
    }

    [Test]
    public async Task SendAsync_ReturnsSyntheticOkResponse()
    {
        // Arrange
        var content = new ByteArrayContent(new byte[] { 0x01 });

        // Act
        var response = await _httpClient.PostAsync("/v1/metrics", content);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public void Drain_WhenNoExportOccurred_ReturnsNull()
    {
        // Act
        var result = _handler.Drain();

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task Drain_ClearsAfterFirstCall()
    {
        // Arrange
        var content = new ByteArrayContent(new byte[] { 0x01, 0x02, 0x03 });
        await _httpClient.PostAsync("/v1/metrics", content);

        // Act
        var first = _handler.Drain();
        var second = _handler.Drain();

        // Assert
        Assert.That(first, Is.Not.Null);
        Assert.That(second, Is.Null);
    }

    [Test]
    public async Task SendAsync_OverwritesPreviousCapture()
    {
        // Arrange
        var firstBytes = new byte[] { 0x01, 0x02 };
        var secondBytes = new byte[] { 0x03, 0x04, 0x05 };

        // Act
        await _httpClient.PostAsync("/v1/metrics", new ByteArrayContent(firstBytes));
        await _httpClient.PostAsync("/v1/metrics", new ByteArrayContent(secondBytes));
        var captured = _handler.Drain();

        // Assert — only the last export is retained
        Assert.That(captured, Is.EqualTo(secondBytes));
    }

    [Test]
    public async Task SendAsync_WithNullContent_DoesNotCapture()
    {
        // Arrange — GET request has no content body
        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/metrics");

        // Act
        await _httpClient.SendAsync(request);
        var captured = _handler.Drain();

        // Assert
        Assert.That(captured, Is.Null);
    }

    [Test]
    public async Task SendAsync_WithEmptyContent_CapturesEmptyArray()
    {
        // Arrange
        var content = new ByteArrayContent(System.Array.Empty<byte>());

        // Act
        await _httpClient.PostAsync("/v1/metrics", content);
        var captured = _handler.Drain();

        // Assert
        Assert.That(captured, Is.Not.Null);
        Assert.That(captured, Is.Empty);
    }
}
