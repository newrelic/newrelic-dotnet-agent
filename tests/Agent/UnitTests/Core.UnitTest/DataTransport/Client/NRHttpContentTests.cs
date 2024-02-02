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
            Assert.That(_nrHttpContent.Encoding, Is.EqualTo("identity")); // assuming uncompressed by default
        }

        [Test]
        public void PayloadBytes_InitializesAndReturnsBytes_WhenNotInitialized()
        {
            _nrHttpContent.SerializedData = "Test";
            var expectedBytes = new UTF8Encoding().GetBytes("Test");
            Assert.That(_nrHttpContent.PayloadBytes, Is.EqualTo(expectedBytes).AsCollection);
        }

        [Test]
        public void UncompressedByteCount_InitializesAndReturnsByteCount_WhenNotInitialized()
        {
            _nrHttpContent.SerializedData = "Test";
            var expectedCount = new UTF8Encoding().GetBytes("Test").Length;
            Assert.That(_nrHttpContent.UncompressedByteCount, Is.EqualTo(expectedCount));
        }

        [Test]
        public void IsCompressed_InitializesAndReturnsCompressedStatus_WhenNotInitialized()
        {
            // Assuming a condition where the data would be compressed based on the length
            _nrHttpContent.SerializedData = new string('a', Constants.CompressMinimumByteLength);
            Assert.That(_nrHttpContent.IsCompressed, Is.True);
        }

        [Test]
        public void CompressionType_InitializesAndReturnsCompressionType_WhenNotInitialized()
        {
            // Assuming a condition where the data would be compressed
            _mockConfiguration.Arrange(config => config.CompressedContentEncoding).Returns("gzip");
            _nrHttpContent.SerializedData = new string('a', Constants.CompressMinimumByteLength);
            Assert.That(_nrHttpContent.CompressionType, Is.EqualTo("gzip"));
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
