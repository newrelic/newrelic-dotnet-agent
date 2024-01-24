// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.IO;
using System.IO.Compression;
using Telerik.JustMock;
using NUnit.Framework;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using NewRelic.Agent.Core.DataTransport.Client.Interfaces;
using Telerik.JustMock.Helpers;

namespace NewRelic.Agent.Core.DataTransport.Client
{

    [TestFixture]
    public class HttpResponseTests
    {
        private HttpResponse _httpResponse;
        private IHttpResponseMessageWrapper _mockHttpResponseMessage;
        private const string TestResponseBody = "testResponseBody";
        private Guid _testGuid = Guid.NewGuid();

        [SetUp]
        public void Setup()
        {
            _mockHttpResponseMessage = Mock.Create<IHttpResponseMessageWrapper>();
            _httpResponse = new HttpResponse(_testGuid, _mockHttpResponseMessage);
        }
        [TearDown]
        public void TearDown()
        {
            _httpResponse.Dispose();
            _mockHttpResponseMessage.Dispose();
        }

        [Test]
        public async Task GetContentAsync_ReturnsEmptyResponseBody_WhenContentIsNull()
        {
            _mockHttpResponseMessage.Arrange(message => message.Content).Returns((IHttpContentWrapper)null);

            var result = await _httpResponse.GetContentAsync();

            Assert.That(result, Is.EqualTo(Constants.EmptyResponseBody));
        }

        [Test]
        public async Task GetContentAsync_ReturnsContent_WhenContentIsNotNull()
        {
            var mockContent = Mock.Create<IHttpContentWrapper>();
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(TestResponseBody));
            _mockHttpResponseMessage.Arrange(message => message.Content).Returns(mockContent);
            mockContent.Arrange(content => content.ReadAsStreamAsync()).ReturnsAsync((Stream)stream);

            var result = await _httpResponse.GetContentAsync();

            Assert.That(result, Is.EqualTo(TestResponseBody));
        }

        [Test]
        public async Task GetContentAsync_HandlesGzipDecompression_WhenContentEncodingIsGzip()
        {
            var compressedStream = new MemoryStream();
            using (var gzipStream = new GZipStream(compressedStream, CompressionMode.Compress, true))
            {
                var bytes = Encoding.UTF8.GetBytes(TestResponseBody);
                gzipStream.Write(bytes, 0, bytes.Length);
            }
            compressedStream.Position = 0;

            var mockContent = Mock.Create<IHttpContentWrapper>();
            _mockHttpResponseMessage.Arrange(message => message.Content).Returns(mockContent);

            var mockHeaders = Mock.Create<IHttpContentHeadersWrapper>();
            mockContent.Arrange(content => content.Headers).Returns(mockHeaders);
            mockHeaders.Arrange(headers => headers.ContentEncoding).Returns(new List<string> { "gzip" });
            mockContent.Arrange(content => content.ReadAsStreamAsync()).ReturnsAsync((Stream)compressedStream);

            var result = await _httpResponse.GetContentAsync();

            Assert.That(result, Is.EqualTo(TestResponseBody));
        }

        [Test]
        public void IsSuccessStatusCode_ReturnsCorrectStatusCode()
        {
            _mockHttpResponseMessage.Arrange(message => message.IsSuccessStatusCode).Returns(true);

            var result = _httpResponse.IsSuccessStatusCode;

            Assert.That(result, Is.True);
        }

        [Test]
        public void StatusCode_ReturnsCorrectStatusCode()
        {
            _mockHttpResponseMessage.Arrange(message => message.StatusCode).Returns(HttpStatusCode.OK);

            var result = _httpResponse.StatusCode;

            Assert.That(result, Is.EqualTo(HttpStatusCode.OK));
        }
    }
}
#endif
