// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.AwsSdk;
using NUnit.Framework;

namespace Agent.Extensions.Tests.Helpers;

[TestFixture]
public class AwsRegionValidatorTests
{
    [TestCase("us-east-1")]
    [TestCase("us-east-2")]
    [TestCase("us-west-2")]
    [TestCase("eu-west-1")]
    [TestCase("ap-southeast-2")]
    [TestCase("cn-north-1")]
    [TestCase("us-gov-west-1")]
    [TestCase("us-iso-east-1")]
    [TestCase("us-isob-east-1")]
    public void LooksLikeARegion_ReturnsTrue_ForRegionShapedValues(string candidate)
    {
        Assert.That(AwsRegionValidator.LooksLikeARegion(candidate), Is.True);
    }

    // "queue" and "amazonaws" are what the SQS legacy endpoint hosts yield when the second host
    // label is assumed to be the region. "(unknown)" is the ArnBuilder default for an unknown region.
    [TestCase("queue")]
    [TestCase("amazonaws")]
    [TestCase("sqs")]
    [TestCase("localhost")]
    [TestCase("(unknown)")]
    [TestCase("us-east")]
    [TestCase("us-east-22")]
    [TestCase("US-EAST-2")]
    [TestCase("my-emulator:4566")]
    [TestCase("")]
    [TestCase(null)]
    public void LooksLikeARegion_ReturnsFalse_ForValuesThatAreNotRegionShaped(string candidate)
    {
        Assert.That(AwsRegionValidator.LooksLikeARegion(candidate), Is.False);
    }
}
