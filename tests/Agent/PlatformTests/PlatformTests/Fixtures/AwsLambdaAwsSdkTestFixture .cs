/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System.Net;
using System.Threading;
using PlatformTests.Applications;
using Xunit;

namespace PlatformTests.Fixtures
{
    public class AwsLambdaAwsSdkTestFixture : BaseFixture
    {
        public const string TestSettingCategory = "AwsLambdaAwsSdkTests";

        public string LogGroupName => ((AwsLambdaApplication)Application).LogGroupName;

        public AwsLambdaAwsSdkTestFixture() : base(new AwsLambdaApplication("AwsLambdaAwsSdkTestFunction", TestSettingCategory))
        {
        }

        public void ExerciseFunction()
        {
            var accessKeyId = Application.TestConfiguration.DefaultSetting.AwsAccessKeyId;
            var secretKeyId = Application.TestConfiguration.DefaultSetting.AwsSecretAccessKey;
            var accountNumber = Application.TestConfiguration.DefaultSetting.AwsAccountNumber;
            var queryString = $"AwsAccessKeyId={accessKeyId}&AwsSecretAccessKey={secretKeyId}&AwsAccountNumber={accountNumber}";

            ((AwsLambdaApplication)Application).ExerciseFunction(queryStringParameters: queryString);
            Thread.Sleep(5000);
        }
    }
}
