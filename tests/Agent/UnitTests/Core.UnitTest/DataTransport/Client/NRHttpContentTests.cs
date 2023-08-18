// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Text;
using Telerik.JustMock.Helpers;

namespace NewRelic.Agent.Core.DataTransport.Client
{
    using global::NewRelic.Agent.Configuration;
    using NUnit.Framework;
    using Telerik.JustMock;

    [TestFixture]
    public class NRHttpContentTests
    {
        private IConfiguration _mockConfiguration;
        private NRHttpContent _nrHttpContent;

        [SetUp]
        public void Setup()
        {
            _mockConfiguration = Mock.Create<IConfiguration>();
            _mockConfiguration.Arrange(c => c.CollectorMaxPayloadSizeInBytes).Returns(Int32.MaxValue);
            _mockConfiguration.Arrange(c => c.CompressedContentEncoding).Returns("gzip");

            _nrHttpContent = new NRHttpContent(_mockConfiguration);

        }

        [Test]
        public void Encoding_InitializesAndReturnsCorrectEncoding_WhenNotInitialized()
        {
            _nrHttpContent.SerializedData = "Test";
            Assert.AreEqual("identity", _nrHttpContent.Encoding); // assuming uncompressed by default
        }

        [Test]
        public void PayloadBytes_InitializesAndReturnsBytes_WhenNotInitialized()
        {
            _nrHttpContent.SerializedData = "Test";
            var expectedBytes = new UTF8Encoding().GetBytes("Test");
            CollectionAssert.AreEqual(expectedBytes, _nrHttpContent.PayloadBytes);
        }

        [Test]
        public void UncompressedByteCount_InitializesAndReturnsByteCount_WhenNotInitialized()
        {
            _nrHttpContent.SerializedData = "Test";
            var expectedCount = new UTF8Encoding().GetBytes("Test").Length;
            Assert.AreEqual(expectedCount, _nrHttpContent.UncompressedByteCount);
        }

        [Test]
        public void IsCompressed_InitializesAndReturnsCompressedStatus_WhenNotInitialized()
        {
            // Assuming a condition where the data would be compressed based on the length
            _nrHttpContent.SerializedData = new string('a', Constants.CompressMinimumByteLength);
            Assert.IsTrue(_nrHttpContent.IsCompressed);
        }

        [Test]
        public void CompressionType_InitializesAndReturnsCompressionType_WhenNotInitialized()
        {
            // Assuming a condition where the data would be compressed
            _mockConfiguration.Arrange(config => config.CompressedContentEncoding).Returns("gzip");
            _nrHttpContent.SerializedData = new string('a', Constants.CompressMinimumByteLength);
            Assert.AreEqual("gzip", _nrHttpContent.CompressionType);
        }

        [Test]
        public void InitializePayload_ThrowsPayloadSizeExceededException_WhenPayloadSizeExceedsMax()
        {
            _mockConfiguration.Arrange(config => config.CollectorMaxPayloadSizeInBytes).Returns(10);
            _nrHttpContent.SerializedData = new string('a', 11);

            Assert.Throws<PayloadSizeExceededException>(() => { var _ = _nrHttpContent.PayloadBytes; });
        }
    }
}
