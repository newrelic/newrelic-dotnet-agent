// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK

using System.Net.Http;
using System.Net;
using System;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using NUnit.Framework;
using System.Threading.Tasks;
using System.Threading;
using NewRelic.Agent.Core.DataTransport.Client;
using NewRelic.Agent.Core.DataTransport.Client.Interfaces;
using NewRelic.Agent.Core.Exceptions;
using NewRelic.Agent.Core.Logging;
using Serilog;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.DataTransport
{
    public class WebRequestTestResponse
    {
        public HttpStatusCode StatusCode { get; set; }
        public string StringContent { get; set; }

        public byte[] ByteArrayContent { get; set; }

        public long ContentLength => ByteArrayContent?.Length ?? StringContent?.Length ?? 0;

        public string ContentEncoding { get; set; }
    }

    [TestFixture]
    public class HttpCollectorWireWebRequestTests
    {
        private IConfiguration _configuration;
        private IAgentHealthReporter _agentHealthReporter;
        private IHttpClientFactory _httpClientFactory;
        private ILogger _mockILogger;
        private IHttpClient _httpClient;
        private HttpWebRequest _httpWebRequest;
        private HttpWebResponse _mockWebResponse;

        [SetUp]
        public void SetUp()
        {
            _configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => _configuration.AgentRunId).Returns("1234");

            _agentHealthReporter = Mock.Create<IAgentHealthReporter>();
            _httpClientFactory = Mock.Create<IHttpClientFactory>();

            _mockILogger = Mock.Create<ILogger>();
            Log.Logger = _mockILogger;
        }

        private HttpCollectorWire CreateHttpCollectorWire(Dictionary<string, string> requestHeadersMap = null)
        {
            return new HttpCollectorWire(_configuration, requestHeadersMap, _agentHealthReporter, _httpClientFactory);
        }

        private void CreateMockHttpWebRequest(WebRequestTestResponse response, bool throwWebRequestException)
        {
            _httpClient = new NRWebRequestClient(null, Mock.Create<IConfiguration>());

            _httpWebRequest = Mock.Create<HttpWebRequest>();

            if (throwWebRequestException)
                Mock.Arrange(() => _httpWebRequest.GetResponseAsync()).Throws(new WebException());
            else
            {
                _mockWebResponse = Mock.Create<HttpWebResponse>();
                Mock.Arrange(() => _mockWebResponse.StatusCode).Returns(response.StatusCode);
                Mock.Arrange(() => _mockWebResponse.ContentLength).Returns(response.ContentLength);

                if (response.ContentEncoding == "gzip") // assume it's zipped byte array content
                {
                    var ms = new MemoryStream(response.ByteArrayContent);
                    Mock.Arrange(() => _mockWebResponse.GetResponseStream()).Returns(ms);
                }
                else if (response.StringContent != null)
                {
                    var ms = new MemoryStream();
                    var writer = new StreamWriter(ms);
                    writer.Write(response.StringContent);
                    writer.Flush();
                    ms.Position = 0;

                    Mock.Arrange(() => _mockWebResponse.GetResponseStream()).Returns(ms);
                }
                else
                    Mock.Arrange(() => _mockWebResponse.GetResponseStream()).Returns(new MemoryStream());

                var headers = new WebHeaderCollection();
                if (!string.IsNullOrEmpty(response.ContentEncoding))
                    headers["content-encoding"] = response.ContentEncoding;
                Mock.Arrange(() => _mockWebResponse.Headers).Returns(headers);

                Mock.Arrange(() => _httpWebRequest.GetResponseAsync()).TaskResult((WebResponse)_mockWebResponse);
            }

            ((NRWebRequestClient)_httpClient).SetHttpWebRequestFunc(uri => _httpWebRequest);

            Mock.Arrange(() => _httpClientFactory.CreateClient(Arg.IsAny<IWebProxy>(), Arg.IsAny<IConfiguration>())).Returns(_httpClient);
        }

        [Test]
        [TestCase(false)]
        [TestCase(true)]
        public void SendData_ShouldSendRequestWithValidParameters(bool usePutForSend)
        {
            // Arrange
            Mock.Arrange(() => _configuration.AgentLicenseKey).Returns("license_key");
            Mock.Arrange(() => _configuration.CollectorTimeout).Returns(5000);
            Mock.Arrange(() => _configuration.CollectorMaxPayloadSizeInBytes).Returns(1024 * 1024);
            Mock.Arrange(() => _configuration.PutForDataSend).Returns(usePutForSend);

            var connectionInfo = new ConnectionInfo(_configuration);
            var serializedData = "{ \"key\": \"value\" }";

            var testResponse = new WebRequestTestResponse() { StatusCode = HttpStatusCode.OK, StringContent = "{}", };

            CreateMockHttpWebRequest(testResponse, false);

            var collectorWire = CreateHttpCollectorWire();

            // Act
            var response = collectorWire.SendData("test_method", connectionInfo, serializedData, Guid.NewGuid());

            // Assert
            Assert.AreEqual("{}", response);
            Mock.Assert(() => _httpClientFactory.CreateClient(Arg.IsAny<IWebProxy>(), Arg.IsAny<IConfiguration>()), Occurs.Once());
        }

        [Test]
        public void SendData_ShouldReturnEmptyResponseBody_WhenResponseContentIsNull()
        {
            // Arrange
            Mock.Arrange(() => _configuration.AgentLicenseKey).Returns("license_key");
            Mock.Arrange(() => _configuration.CollectorTimeout).Returns(5000);
            Mock.Arrange(() => _configuration.CollectorMaxPayloadSizeInBytes).Returns(1024 * 1024);

            var connectionInfo = new ConnectionInfo(_configuration);
            var serializedData = "{ \"key\": \"value\" }";

            var httpResponse = new WebRequestTestResponse
            {
                StatusCode = HttpStatusCode.OK,
                StringContent = null
            };

            CreateMockHttpWebRequest(httpResponse, false);

            var collectorWire = CreateHttpCollectorWire();

            // Act
            var response = collectorWire.SendData("test_method", connectionInfo, serializedData, Guid.NewGuid());

            // Assert
            Assert.AreEqual("{}", response);
            Mock.Assert(() => _httpClientFactory.CreateClient(Arg.IsAny<IWebProxy>(), Arg.IsAny<IConfiguration>()), Occurs.Once());
            Mock.Assert(() => _mockWebResponse.GetResponseStream(), Occurs.Once());
        }

        [Test]
        public void SendData_ShouldReturnEmptyResponse_WhenResponseContentIsEmpty()
        {
            // Arrange
            Mock.Arrange(() => _configuration.AgentLicenseKey).Returns("license_key");
            Mock.Arrange(() => _configuration.CollectorTimeout).Returns(5000);
            Mock.Arrange(() => _configuration.CollectorMaxPayloadSizeInBytes).Returns(1024 * 1024);

            var connectionInfo = new ConnectionInfo(_configuration);
            var serializedData = "{ \"key\": \"value\" }";

            var testResponse = new WebRequestTestResponse() { StatusCode = HttpStatusCode.OK, StringContent = "{}", };

            CreateMockHttpWebRequest(testResponse, false);

            var collectorWire = CreateHttpCollectorWire();

            // Act
            var response = collectorWire.SendData("test_method", connectionInfo, serializedData, Guid.NewGuid());

            // Assert
            Assert.AreEqual("{}", response);
            Mock.Assert(() => _httpClientFactory.CreateClient(Arg.IsAny<IWebProxy>(), Arg.IsAny<IConfiguration>()), Occurs.Once());
            Mock.Assert(() => _mockWebResponse.GetResponseStream(), Occurs.Once());
        }

        [Test]
        public void SendData_DecompressesGZipResponse()
        {
            // Arrange
            Mock.Arrange(() => _configuration.AgentLicenseKey).Returns("license_key");
            Mock.Arrange(() => _configuration.CollectorTimeout).Returns(5000);
            Mock.Arrange(() => _configuration.CollectorMaxPayloadSizeInBytes).Returns(1024 * 1024);

            var connectionInfo = new ConnectionInfo(_configuration);
            var serializedData = "{ \"key\": \"value\" }";

            byte[] zippedBytes;
            using (var memoryStream = new MemoryStream())
            {
                using (var gzipStream = new GZipStream(memoryStream, CompressionLevel.Fastest))
                {
                    var bytes = Encoding.UTF8.GetBytes(serializedData);
                    gzipStream.Write(bytes, 0, bytes.Length);
                }

                memoryStream.Flush();
                zippedBytes = memoryStream.ToArray();
            }

            var testResponse = new WebRequestTestResponse() { StatusCode = HttpStatusCode.OK, ByteArrayContent = zippedBytes, ContentEncoding = "gzip" };

            CreateMockHttpWebRequest(testResponse, false);

            var collectorWire = CreateHttpCollectorWire();

            // Act
            var response = collectorWire.SendData("test_method", connectionInfo, serializedData, Guid.NewGuid());

            // Assert
            Assert.AreEqual(serializedData, response);
            Mock.Assert(() => _httpClientFactory.CreateClient(Arg.IsAny<IWebProxy>(), Arg.IsAny<IConfiguration>()), Occurs.Once());
            Mock.Assert(() => _mockWebResponse.GetResponseStream(), Occurs.Once());
        }

        [Test]
        public void SendData_ReturnsEmptyResponse_WhenGZipResponseIsInvalid()
        {
            // Arrange
            Mock.Arrange(() => _configuration.AgentLicenseKey).Returns("license_key");
            Mock.Arrange(() => _configuration.CollectorTimeout).Returns(5000);
            Mock.Arrange(() => _configuration.CollectorMaxPayloadSizeInBytes).Returns(1024 * 1024);

            var connectionInfo = new ConnectionInfo(_configuration);
            var serializedData = "{ \"key\": \"value\" }";

            byte[] zippedBytes;
            using (var memoryStream = new MemoryStream())
            {
                using (var gzipStream = new GZipStream(memoryStream, CompressionLevel.Fastest))
                {
                    var bytes = Encoding.UTF8.GetBytes(serializedData);
                    gzipStream.Write(bytes, 0, bytes.Length);
                }

                memoryStream.Flush();
                zippedBytes = memoryStream.ToArray();
            }
            // make the array invalid by twiddling a byte...
            zippedBytes[0] = Byte.MaxValue;

            var testResponse = new WebRequestTestResponse() { StatusCode = HttpStatusCode.OK, ByteArrayContent = zippedBytes, ContentEncoding = "gzip" };
            CreateMockHttpWebRequest(testResponse, false);

            var collectorWire = CreateHttpCollectorWire();

            // Act
            var response = collectorWire.SendData("test_method", connectionInfo, serializedData, Guid.NewGuid());

            // Assert
            Assert.AreEqual("{}", response);
            Mock.Assert(() => _httpClientFactory.CreateClient(Arg.IsAny<IWebProxy>(), Arg.IsAny<IConfiguration>()), Occurs.Once());
            Mock.Assert(() => _mockWebResponse.GetResponseStream(), Occurs.Once());
        }

        [Test]
        public void SendData_ShouldThrowWebException_WhenRequestFails()
        {
            // Arrange
            Mock.Arrange(() => _configuration.AgentLicenseKey).Returns("license_key");
            Mock.Arrange(() => _configuration.CollectorTimeout).Returns(5000);
            Mock.Arrange(() => _configuration.CollectorMaxPayloadSizeInBytes).Returns(1024 * 1024);

            var connectionInfo = new ConnectionInfo(_configuration);
            var serializedData = "{ \"key\": \"value\" }";

            var testResponse = new WebRequestTestResponse() { StatusCode = HttpStatusCode.InternalServerError, StringContent = "{}", };
            CreateMockHttpWebRequest(testResponse, true);

            var collectorWire = CreateHttpCollectorWire();

            // Act and Assert
            Assert.Throws<WebException>(() => collectorWire.SendData("test_method", connectionInfo, serializedData, Guid.NewGuid()));

            Mock.Assert(() => _httpClientFactory.CreateClient(Arg.IsAny<IWebProxy>(), Arg.IsAny<IConfiguration>()), Occurs.Once());
        }

        [Test]
        public void SendData_ShouldThrowHttpException_WhenResponse_IsNotSuccessful()
        {
            // Arrange
            Mock.Arrange(() => _configuration.AgentLicenseKey).Returns("license_key");
            Mock.Arrange(() => _configuration.CollectorTimeout).Returns(5000);
            Mock.Arrange(() => _configuration.CollectorMaxPayloadSizeInBytes).Returns(1024 * 1024);

            var connectionInfo = new ConnectionInfo(_configuration);
            var serializedData = "invalidjson";

            var testResponse = new WebRequestTestResponse() { StatusCode = HttpStatusCode.UnsupportedMediaType, StringContent = "{}", };
            CreateMockHttpWebRequest(testResponse, false);

            var collectorWire = CreateHttpCollectorWire();

            // Act and Assert
            Assert.Throws<HttpException>(() => collectorWire.SendData("test_method", connectionInfo, serializedData, Guid.NewGuid()));

            Mock.Assert(() => _httpClientFactory.CreateClient(Arg.IsAny<IWebProxy>(), Arg.IsAny<IConfiguration>()), Occurs.Once());
            Mock.Assert(() => _mockWebResponse.GetResponseStream(), Occurs.Once());
        }

        [Test]
        public void SendData_ShouldDropPayload_WhenPayloadSizeExceedsMaxSize()
        {
            // Arrange
            Mock.Arrange(() => _configuration.AgentLicenseKey).Returns("license_key");
            Mock.Arrange(() => _configuration.CollectorTimeout).Returns(5000);
            Mock.Arrange(() => _configuration.CollectorMaxPayloadSizeInBytes).Returns(1024);
            Mock.Arrange(() => _configuration.CompressedContentEncoding).Returns("gzip");

            var connectionInfo = new ConnectionInfo(_configuration);
            var largeSerializedData = new string('x', 1024 * 1024 + 1); // Create a string larger than the maximum allowed payload size

            var testResponse = new WebRequestTestResponse() { StatusCode = HttpStatusCode.OK, StringContent = "{}", };
            CreateMockHttpWebRequest(testResponse, false);

            var collectorWire = CreateHttpCollectorWire();

            // Act
            var result = collectorWire.SendData("test_method", connectionInfo, largeSerializedData, Guid.NewGuid());

            // Assert
            Assert.AreEqual("{}", result);
            Mock.Assert(() => _agentHealthReporter.ReportSupportabilityPayloadsDroppeDueToMaxPayloadSizeLimit("test_method"), Occurs.Once());
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SendData_ShouldNotCallAuditLog_UnlessAuditLogIsEnabled(bool isEnabled)
        {
            // Arrange
            AuditLog.ResetLazyLogger();
            Mock.Arrange(() => _configuration.AgentLicenseKey).Returns("license_key");
            Mock.Arrange(() => _configuration.CollectorMaxPayloadSizeInBytes).Returns(1024);

            var connectionInfo = new ConnectionInfo(_configuration);
            var serializedData = "{ \"key\": \"value\" }";

            var testResponse = new WebRequestTestResponse() { StatusCode = HttpStatusCode.OK, StringContent = "{}", };
            CreateMockHttpWebRequest(testResponse, false);

            var collectorWire = CreateHttpCollectorWire();

            var mockForContextLogger = Mock.Create<ILogger>();
            Mock.Arrange(() => _mockILogger.ForContext(Arg.AnyString, Arg.AnyObject, false))
                .Returns(() => mockForContextLogger);

            AuditLog.IsAuditLogEnabled = isEnabled;

            // Act
            _ = collectorWire.SendData("test_method", connectionInfo, serializedData, Guid.NewGuid());

            // Assert
            Mock.Assert(() => mockForContextLogger.Fatal(Arg.AnyString), isEnabled ? Occurs.Exactly(3) : Occurs.Never());
        }
    }
}
#endif
