// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Threading;
using PlatformTests.Applications;
using Xunit;

namespace PlatformTests.Fixtures
{
    public class AwsLambdaApplicationFixture : BaseFixture
    {
        public const string TestSettingCategory = "AwsLambdaSmokeTests";

        public string LogGroupName => ((AwsLambdaApplication)Application).LogGroupName;

        public AwsLambdaApplicationFixture() : base(new AwsLambdaApplication("AwsLambdaTestApplication", TestSettingCategory))
        {
        }

        public void ExerciseFunction()
        {
            ((AwsLambdaApplication)Application).ExerciseFunction();
            Thread.Sleep(5000);
        }
    }
}
