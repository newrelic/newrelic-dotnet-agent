// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework;
using NewRelic.Agent.Extensions.AwsSdk;

namespace Agent.Extensions.Tests.Helpers
{
    public class AwsSdkArnBuilderTests
    {
        [Test]
        [TestCase("myfunction", "us-west-2", "123456789012", "arn:aws:lambda:us-west-2:123456789012:function:myfunction")]
        [TestCase("myfunction", "us-west-2", "", null)]
        [TestCase("myfunction", "", "123456789012", null)]
        [TestCase("myfunction:alias", "us-west-2", "123456789012", "arn:aws:lambda:us-west-2:123456789012:function:myfunction:alias")]
        [TestCase("myfunction:alias", "us-west-2", "", null)]
        [TestCase("123456789012:function:my-function", "us-west-2", "123456789012", "arn:aws:lambda:us-west-2:123456789012:function:my-function")]
        [TestCase("123456789012:function:my-function:myalias", "us-west-2", "123456789012", "arn:aws:lambda:us-west-2:123456789012:function:my-function:myalias")]
        [TestCase("123456789012:function:my-function:myalias:extra", "us-west-2", "123456789012", null)]
        [TestCase("123456789012:function:my-function:myalias:extra:lots:of:extra:way:too:many", "us-west-2", "123456789012", null)]
        [TestCase("arn:aws:", "us-west-2", "123456789012", "arn:aws:")]
        [TestCase("arn:aws:lambda:us-west-2:123456789012:function:myfunction", "us-west-2", "123456789012", "arn:aws:lambda:us-west-2:123456789012:function:myfunction")]
        [TestCase("arn:aws:lambda:us-west-2:123456789012:function:myfunction", "us-west-2", "", "arn:aws:lambda:us-west-2:123456789012:function:myfunction")]
        [TestCase("arn:aws:lambda:us-west-2:123456789012:function:myfunction", "", "123456789012", "arn:aws:lambda:us-west-2:123456789012:function:myfunction")]
        [TestCase("myfunction", "us-east-1", "987654321098", "arn:aws:lambda:us-east-1:987654321098:function:myfunction")]
        [TestCase("myfunction:prod", "eu-west-1", "111122223333", "arn:aws:lambda:eu-west-1:111122223333:function:myfunction:prod")]
        [TestCase("my-function", "ap-southeast-1", "444455556666", "arn:aws:lambda:ap-southeast-1:444455556666:function:my-function")]
        [TestCase("my-function:beta", "ca-central-1", "777788889999", "arn:aws:lambda:ca-central-1:777788889999:function:my-function:beta")]
        [TestCase("arn:aws:lambda:eu-central-1:222233334444:function:myfunction", "eu-central-1", "222233334444", "arn:aws:lambda:eu-central-1:222233334444:function:myfunction")]
        [TestCase("us-west-2:myfunction", null, "123456789012", "arn:aws:lambda:us-west-2:123456789012:function:myfunction")]
        [TestCase("us-west-2:myfunction", "us-west-2", "123456789012", "arn:aws:lambda:us-west-2:123456789012:function:myfunction")]
        [TestCase("us-west-2:myfunction", "us-west-2", "", null)]
        [TestCase("us-west-2:myfunction:alias", "us-west-2", "123456789012", "arn:aws:lambda:us-west-2:123456789012:function:myfunction:alias")]
        [TestCase("us-west-2:myfunction:alias", "us-west-2", "", null)]
        [TestCase("123456789012:my-function", "us-west-2", "123456789012", "arn:aws:lambda:us-west-2:123456789012:function:my-function")]
        [TestCase("123456789012:my-function:myalias", "us-west-2", "123456789012", "arn:aws:lambda:us-west-2:123456789012:function:my-function:myalias")]
        [TestCase("123456789012:my-function:myalias:extra", "us-west-2", "123456789012", null)]
        [TestCase("123456789012:my-function:myalias:extra:lots:of:extra:way:too:many", "us-west-2", "123456789012", null)]
        [TestCase("eu-west-1:us-west-2", "eu-west-1", "123456789012", "arn:aws:lambda:eu-west-1:123456789012:function:us-west-2")]
        [TestCase("", "us-west-2", "123456789012", null)]
        // Edge cases: functions that look like account IDs or region names
        [TestCase("123456789012:444455556666", "us-west-2", "123456789012", "arn:aws:lambda:us-west-2:123456789012:function:444455556666")]
        [TestCase("444455556666", "us-west-2", "123456789012", "arn:aws:lambda:us-west-2:123456789012:function:444455556666")]
        [TestCase("us-west-2", "us-west-2", "123456789012", "arn:aws:lambda:us-west-2:123456789012:function:us-west-2")]
        public void ConstructLambdaArn(string name, string region, string accountId, string arn)
        {
            var arnBuilder = new ArnBuilder("aws", region, accountId);
            var constructedArn = arnBuilder.BuildFromPartialLambdaArn(name);
            Assert.That(constructedArn, Is.EqualTo(arn), "Did not get expected ARN");
        }

        [Test]
        [TestCase("aws", "s3", "us-west-2", "123456789012", "bucket_name", "arn:aws:s3:us-west-2:123456789012:bucket_name")]
        [TestCase("aws", "dynamodb", "us-east-1", "987654321098", "table_name", "arn:aws:dynamodb:us-east-1:987654321098:table_name")]
        [TestCase("aws", "dynamodb", "us-east-1", "987654321098", "table/tabletName", "arn:aws:dynamodb:us-east-1:987654321098:table/tabletName")]
        [TestCase("aws", "ec2", "eu-west-1", "111122223333", "instance_id", "arn:aws:ec2:eu-west-1:111122223333:instance_id")]
        [TestCase("aws", "sqs", "ap-southeast-1", "444455556666", "queue_name", "arn:aws:sqs:ap-southeast-1:444455556666:queue_name")]
        [TestCase("aws", "sns", "ca-central-1", "777788889999", "topic_name", "arn:aws:sns:ca-central-1:777788889999:topic_name")]
        [TestCase("aws-cn", "lambda", "cn-north-1", "222233334444", "function_name", "arn:aws-cn:lambda:cn-north-1:222233334444:function_name")]
        [TestCase("aws-us-gov", "iam", "us-gov-west-1", "555566667777", "role_name", "arn:aws-us-gov:iam:us-gov-west-1:555566667777:role_name")]
        [TestCase("aws", "rds", "sa-east-1", "888899990000", "db_instance", "arn:aws:rds:sa-east-1:888899990000:db_instance")]
        [TestCase("aws", "s3", "", "123456789012", "bucket_name", null)]
        [TestCase("aws", "s3", "us-west-2", "", "bucket_name", null)]
        public void ConstructGenericArn(string partition, string service, string region, string accountId, string resource, string expectedArn)
        {
            var arnBuilder = new ArnBuilder(partition, region, accountId);
            var constructedArn = arnBuilder.Build(service, resource);
            Assert.That(constructedArn, Is.EqualTo(expectedArn), "Did not get expected ARN");
        }
    }
}
