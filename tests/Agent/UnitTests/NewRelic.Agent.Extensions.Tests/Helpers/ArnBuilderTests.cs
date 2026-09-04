// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.AwsSdk;
using NUnit.Framework;

namespace Agent.Extensions.Tests.Helpers;

[TestFixture]
public class ArnBuilderTests
{
    [TestCase("us-east-1", "aws")]
    [TestCase("eu-west-1", "aws")]
    [TestCase("ap-southeast-2", "aws")]
    [TestCase("cn-north-1", "aws-cn")]
    [TestCase("cn-northwest-1", "aws-cn")]
    [TestCase("us-gov-west-1", "aws-us-gov")]
    [TestCase("us-gov-east-1", "aws-us-gov")]
    [TestCase("us-iso-east-1", "aws-iso")]
    [TestCase("us-isob-east-1", "aws-iso-b")]
    [TestCase("eu-isoe-west-1", "aws-iso-e")]
    [TestCase("us-isof-south-1", "aws-iso-f")]
    [TestCase(null, "aws")]
    [TestCase("", "aws")]
    [TestCase("(unknown)", "aws")]
    public void Partition_IsDerivedFromRegion_WhenNoPartitionSupplied(string region, string expectedPartition)
    {
        var builder = new ArnBuilder(null, region, "123456789012");

        Assert.That(builder.Partition, Is.EqualTo(expectedPartition));
    }

    [Test]
    public void Partition_UsesSuppliedValue_EvenWhenRegionImpliesADifferentPartition()
    {
        var builder = new ArnBuilder("aws", "us-gov-west-1", "123456789012");

        Assert.That(builder.Partition, Is.EqualTo("aws"));
    }

    [Test]
    public void Build_EmitsDerivedPartition_ForGovCloudRegion()
    {
        var builder = new ArnBuilder(null, "us-gov-west-1", "123456789012");

        Assert.That(builder.Build("kinesis", "stream/MyStream"), Is.EqualTo("arn:aws-us-gov:kinesis:us-gov-west-1:123456789012:stream/MyStream"));
    }

    [Test]
    public void Build_EmitsDerivedPartition_ForChinaRegion()
    {
        var builder = new ArnBuilder(null, "cn-north-1", "123456789012");

        Assert.That(builder.Build("kinesis", "stream/MyStream"), Is.EqualTo("arn:aws-cn:kinesis:cn-north-1:123456789012:stream/MyStream"));
    }

    [Test]
    public void Build_EmitsDerivedPartition_ForStandardRegion()
    {
        var builder = new ArnBuilder(null, "us-east-1", "123456789012");

        Assert.That(builder.Build("kinesis", "stream/MyStream"), Is.EqualTo("arn:aws:kinesis:us-east-1:123456789012:stream/MyStream"));
    }
}
