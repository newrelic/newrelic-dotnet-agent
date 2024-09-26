// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework;
using NewRelic.Agent.Extensions.Helpers;

namespace Agent.Extensions.Tests.Helpers
{
    public class AwsSdkHelperTests
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
        [TestCase("arn:aws:", "us-west-2", "123456789012", null)]
        [TestCase("arn:aws:lambda:us-west-2:123456789012:function:myfunction", "us-west-2", "123456789012", "arn:aws:lambda:us-west-2:123456789012:function:myfunction")]
        [TestCase("arn:aws:lambda:us-west-2:123456789012:function:myfunction", "us-west-2", "", "arn:aws:lambda:us-west-2:123456789012:function:myfunction")]
        [TestCase("arn:aws:lambda:us-west-2:123456789012:function:myfunction", "", "123456789012", "arn:aws:lambda:us-west-2:123456789012:function:myfunction")]
        [TestCase("myfunction", "us-east-1", "987654321098", "arn:aws:lambda:us-east-1:987654321098:function:myfunction")]
        [TestCase("myfunction:prod", "eu-west-1", "111122223333", "arn:aws:lambda:eu-west-1:111122223333:function:myfunction:prod")]
        [TestCase("my-function", "ap-southeast-1", "444455556666", "arn:aws:lambda:ap-southeast-1:444455556666:function:my-function")]
        [TestCase("my-function:beta", "ca-central-1", "777788889999", "arn:aws:lambda:ca-central-1:777788889999:function:my-function:beta")]
        [TestCase("arn:aws:lambda:eu-central-1:222233334444:function:myfunction", "eu-central-1", "222233334444", "arn:aws:lambda:eu-central-1:222233334444:function:myfunction")]
        public void ConstructArn(string name, string region, string accountId, string arn)
        {
            var constructedArn = AwsSdkHelpers.ConstructArn(null, name, region, accountId);
            Assert.That(constructedArn, Is.EqualTo(arn), "Did not get expected ARN");
        }
    }
}
