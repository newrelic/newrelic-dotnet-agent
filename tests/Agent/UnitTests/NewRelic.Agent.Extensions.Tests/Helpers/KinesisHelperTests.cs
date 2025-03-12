// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Aws.Kinesis.Models;
using NewRelic.Agent.Extensions.AwsSdk;
using NewRelic.Testing.Assertions;
using NUnit.Framework;

namespace Agent.Extensions.Tests.Helpers
{
    [TestFixture]
    public class KinesisHelperTests
    {

        [Test]
        [TestCase("arn:aws:kinesis:us-west-2:111111111111:deliverystream/FirehoseStreamName", "FirehoseStreamName")]
        [TestCase("arn:aws:kinesis:us-west-2:111111111111:stream/KinesisStreamName", "KinesisStreamName")]
        [TestCase("notAnArn", null)]
        [TestCase("arn:with:no:slash:in:last:part", null)]
        public void GetStreamNameFromArn(string arn, string expectedStreamName)
        {
            // Act
            var streamName = KinesisHelper.GetStreamNameFromArn(arn);

            // Assert
            Assert.That(streamName.IsEqualTo(expectedStreamName));
        }

        [Test]
        [TestCase("KinesisStreamName", "arn:aws:kinesis:us-west-2:111111111111:stream/KinesisStreamName", "KinesisStreamName")]
        [TestCase("KinesisStreamName", null, "KinesisStreamName")]
        [TestCase(null, "arn:aws:kinesis:us-west-2:111111111111:stream/KinesisStreamName", "KinesisStreamName")]
        [TestCase(null, null, null)]
        public void GetStreamNameFromRequest(string streamName, string streamArn, string expectedStreamName)
        {
            dynamic request = new MockKinesisDataStreamRequest();
            request.StreamName = streamName;
            request.StreamArn = streamArn;

            // Act
            var streamNameFromHelper = KinesisHelper.GetStreamNameFromRequest(request) as string;

            // Assert
            Assert.That(streamNameFromHelper.IsEqualTo(expectedStreamName));
        }

        [Test]
        [TestCase("FirehoseStreamName", "arn:aws:kinesis:us-west-2:111111111111:deliverystream/FirehoseStreamName", "FirehoseStreamName")]
        [TestCase("FirehoseStreamName", null, "FirehoseStreamName")]
        [TestCase(null, "arn:aws:kinesis:us-west-2:111111111111:stream/FirehoseStreamName", "FirehoseStreamName")]
        [TestCase(null, null, null)]
        public void GetDeliveryStreamNameFromRequest(string streamName, string streamArn, string expectedStreamName)
        {
            dynamic request = new MockKinesisFirehoseRequest();
            request.DeliveryStreamName = streamName;
            request.DeliveryStreamArn = streamArn;

            // Act
            var streamNameFromHelper = KinesisHelper.GetDeliveryStreamNameFromRequest(request) as string;

            // Assert
            Assert.That(streamNameFromHelper.IsEqualTo(expectedStreamName));
        }

        [Test]
        public void GetStreamNameFromRequest_UnknownRequestType_ReturnsNull()
        {
            dynamic request = new MockUnknownRequest();
            request.NotTheDroidsYoureLookingFor = "Daleks";

            // Act
            var streamNameFromHelper = KinesisHelper.GetStreamNameFromRequest(request) as string;

            // Assert
            Assert.That(streamNameFromHelper.IsEqualTo(null));
        }

        [Test]
        public void GetDeliveryStreamNameFromRequest_UnknownRequestType_ReturnsNull()
        {
            dynamic request = new MockUnknownRequest();
            request.NotTheDroidsYoureLookingFor = "Lore";

            // Act
            var streamNameFromHelper = KinesisHelper.GetDeliveryStreamNameFromRequest(request) as string;

            // Assert
            Assert.That(streamNameFromHelper.IsEqualTo(null));
        }

    }
}

namespace Aws.Kinesis.Models
{
    public class MockKinesisDataStreamRequest
    {
        public string StreamName { get; set; }
        public string StreamArn { get; set; }
    }

    public class MockKinesisFirehoseRequest
    {
        public string DeliveryStreamName { get; set; }
        public string DeliveryStreamArn { get; set; }
    }

    public class MockUnknownRequest
    {
        public string NotTheDroidsYoureLookingFor { get; set; }
    }
}

