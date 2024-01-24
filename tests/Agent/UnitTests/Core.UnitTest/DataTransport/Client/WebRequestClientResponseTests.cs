// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.DataTransport.Client
{
    [TestFixture]
    public class WebRequestClientResponseTests
    {
        private Guid _requestGuid;
        private HttpWebResponse _response;

        [SetUp]
        public void SetUp()
        {
            _requestGuid = Guid.NewGuid();
            _response = Mock.Create<HttpWebResponse>();
        }

        [TearDown]
        public void TearDown()
        {
            _response.Dispose();
        }

        [Test]
        public async Task GetContentAsync_ShouldReturnValidResponse_WhenResponseStreamIsNotNullAndNotGzipped()
        {
            // Arrange
            var fakeStream = new MemoryStream(Encoding.UTF8.GetBytes("Test Response"));
            Mock.Arrange(() => _response.GetResponseStream()).Returns(fakeStream);
            var webRequestClientResponse = new WebRequestClientResponse(_requestGuid, _response);

            // Act
            var content = await webRequestClientResponse.GetContentAsync();

            // Assert
            Assert.That(content, Is.EqualTo("Test Response"));
        }

        [Test]
        public async Task GetContentAsync_ShouldReturnEmpty_WhenResponseStreamIsNull()
        {
            // Arrange
            Mock.Arrange(() => _response.GetResponseStream()).Returns(() => null);
            var webRequestClientResponse = new WebRequestClientResponse(_requestGuid, _response);

            // Act
            var content = await webRequestClientResponse.GetContentAsync();

            // Assert
            Assert.That(content, Is.EqualTo(Constants.EmptyResponseBody));
        }

        [Test]
        public async Task GetContentAsync_ShouldReturnEmpty_WhenResponseHasException()
        {
            // Arrange
            var fakeStream = new ExceptionThrowingStream();
            Mock.Arrange(() => _response.GetResponseStream()).Returns(fakeStream);
            var webRequestClientResponse = new WebRequestClientResponse(_requestGuid, _response);

            // Act
            var content = await webRequestClientResponse.GetContentAsync();

            // Assert
            Assert.That(content, Is.EqualTo(Constants.EmptyResponseBody));
        }
        [Test]
        public async Task GetContentAsync_ShouldReturnEmpty_WhenResponseHeadersIsNull()
        {
            // Arrange
            Mock.Arrange(() => _response.GetResponseStream()).Returns(new MemoryStream()); // Any dummy stream
            Mock.Arrange(() => _response.Headers).Returns((WebHeaderCollection)null); // Set Headers to null
            var webRequestClientResponse = new WebRequestClientResponse(_requestGuid, _response);

            // Act
            var content = await webRequestClientResponse.GetContentAsync();

            // Assert
            Assert.That(content, Is.EqualTo(Constants.EmptyResponseBody));
        }

        [Test]
        public async Task GetContentAsync_ShouldReturnDecompressedResponse_WhenResponseIsGzipped()
        {
            // Arrange
            var originalText = "Test GZIP Response";
            var compressedData = CompressString(originalText);
            var fakeStream = new MemoryStream(compressedData);
            Mock.Arrange(() => _response.GetResponseStream()).Returns(fakeStream);
            Mock.Arrange(() => _response.Headers.Get("content-encoding")).Returns("gzip");
            var webRequestClientResponse = new WebRequestClientResponse(_requestGuid, _response);

            // Act
            var content = await webRequestClientResponse.GetContentAsync();

            // Assert
            Assert.That(content, Is.EqualTo(originalText));
        }

        [Test]
        public void IsSuccessStatusCode_ShouldReturnTrue_WhenStatusCodeIs2xx()
        {
            // Arrange
            Mock.Arrange(() => _response.StatusCode).Returns(HttpStatusCode.OK); // 200 OK
            var webRequestClientResponse = new WebRequestClientResponse(_requestGuid, _response);

            // Act & Assert
            Assert.That(webRequestClientResponse.IsSuccessStatusCode, Is.True);
        }

        [Test]
        public void IsSuccessStatusCode_ShouldReturnFalse_WhenStatusCodeIsNot2xx()
        {
            // Arrange
            Mock.Arrange(() => _response.StatusCode).Returns(HttpStatusCode.BadRequest); // 400 Bad Request
            var webRequestClientResponse = new WebRequestClientResponse(_requestGuid, _response);

            // Act & Assert
            Assert.That(webRequestClientResponse.IsSuccessStatusCode, Is.False);
        }

        [Test]
        public void StatusCode_ShouldReturnCorrectStatusCode()
        {
            // Arrange
            Mock.Arrange(() => _response.StatusCode).Returns(HttpStatusCode.NotFound); // 404 Not Found
            var webRequestClientResponse = new WebRequestClientResponse(_requestGuid, _response);

            // Act & Assert
            Assert.That(webRequestClientResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        private byte[] CompressString(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            using (var msi = new MemoryStream(bytes))
            using (var mso = new MemoryStream())
            using (var gs = new GZipStream(mso, CompressionMode.Compress))
            {
                msi.CopyTo(gs);
                gs.Close();
                return mso.ToArray();
            }
        }

        private class ExceptionThrowingStream : Stream
        {
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotImplementedException();

            public override long Position
            {
                get => throw new NotImplementedException();
                set => throw new NotImplementedException();
            }

            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) => throw new Exception("Stream Error");
            public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();
            public override void SetLength(long value) => throw new NotImplementedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();
        }
    }
}
#endif
