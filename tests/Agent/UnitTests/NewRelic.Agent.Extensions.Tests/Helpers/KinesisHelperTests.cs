// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.AwsSdk;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;

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
    }
}
