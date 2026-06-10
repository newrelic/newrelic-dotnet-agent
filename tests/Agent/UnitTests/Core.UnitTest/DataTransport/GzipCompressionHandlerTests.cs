// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NewRelic.Agent.Core.OpenTelemetryBridge;
using NUnit.Framework;

namespace NewRelic.Agent.Core.UnitTest.DataTransport;

[TestFixture]
public class GzipCompressionHandlerTests
{
    private TestHttpMessageHandler _innerHandler;
    private GzipCompressionHandler _gzipHandler;
    private HttpClient _httpClient;

    [SetUp]
    public void SetUp()
    {
        _innerHandler = new TestHttpMessageHandler();
        _gzipHandler = new GzipCompressionHandler { InnerHandler = _innerHandler };
        _httpClient = new HttpClient(_gzipHandler);
    }

    [TearDown]
    public void TearDown()
    {
        _httpClient?.Dispose();
        _gzipHandler?.Dispose();
        _innerHandler?.Dispose();
    }

    [Test]
    public async Task SendAsync_WithContent_SetsGzipContentEncodingHeader()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "http://test.com")
        {
            Content = new ByteArrayContent(Encoding.UTF8.GetBytes("hello otlp"))
        };

        await _httpClient.SendAsync(request);

        var contentEncoding = _innerHandler.RequestSent.Content.Headers.ContentEncoding;
        Assert.That(contentEncoding, Does.Contain("gzip"));
    }

    [Test]
    public async Task SendAsync_WithContent_CompressedBodyDecompressesToOriginal()
    {
        var originalBytes = Encoding.UTF8.GetBytes("the quick brown fox jumps over the lazy dog");
        var request = new HttpRequestMessage(HttpMethod.Post, "http://test.com")
        {
            Content = new ByteArrayContent(originalBytes)
        };

        await _httpClient.SendAsync(request);

        var sentBytes = await _innerHandler.RequestSent.Content.ReadAsByteArrayAsync();
        var decompressed = GzipDecompress(sentBytes);

        Assert.That(decompressed, Is.EqualTo(originalBytes));
    }

    [Test]
    public async Task SendAsync_WithContent_PreservesContentType()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "http://test.com")
        {
            Content = new ByteArrayContent(new byte[] { 1, 2, 3 })
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-protobuf");

        await _httpClient.SendAsync(request);

        Assert.That(_innerHandler.RequestSent.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/x-protobuf"));
    }

    [Test]
    public async Task SendAsync_WithNullContent_PassesThroughUnmodified()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "http://test.com");

        var response = await _httpClient.SendAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(_innerHandler.RequestSent.Content, Is.Null);
        });
    }

    private static byte[] GzipDecompress(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }

    private class TestHttpMessageHandler : HttpMessageHandler
    {
        public HttpRequestMessage RequestSent { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Buffer the content now so it remains readable after this handler returns.
            if (request.Content != null)
            {
                await request.Content.LoadIntoBufferAsync();
            }

            RequestSent = request;
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
