/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System.Net;
using System.Threading;
using PlatformTests.Applications;

namespace PlatformTests.Fixtures
{
    public class AwsLambdaErrorTestFixture : BaseFixture
    {
        public const string TestSettingCategory = "AwsLambdaErrorTests";

        public string LogGroupName => ((AwsLambdaApplication)Application).LogGroupName;

        public AwsLambdaErrorTestFixture() : base(new AwsLambdaApplication("AwsLambdaErrorTestFunction", TestSettingCategory))
        {
        }

        public void ExerciseFunction()
        {
            try
            {
                ((AwsLambdaApplication)Application).ExerciseFunction();
                Thread.Sleep(5000);
            }
            catch (WebException ex)
            {
                if (ex.Message != "The remote server returned an error: (502) Bad Gateway.")
                {
                    throw;
                }
            }
        }
    }
}
