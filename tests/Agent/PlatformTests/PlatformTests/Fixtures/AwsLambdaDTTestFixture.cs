// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Net;
using System.Threading;
using PlatformTests.Applications;
using Xunit;

namespace PlatformTests.Fixtures
{
    public class AwsLambdaDTTestFixture : BaseFixture
    {
        public string CalleeApplicationLogs => ((AwsLambdaApplication)base.Application).LogGroupName;
        public string CallerApplicationLogs => CallerApplication.LogGroupName;

        public AwsLambdaApplication CalleeApplication => (AwsLambdaApplication)base.Application;
        public AwsLambdaApplication CallerApplication;

        public AwsLambdaDTTestFixture() : base(new AwsLambdaApplication("AwsLambdaTestApplication", "AwsLambdaDTTestCallee")) //callee
        {
            CallerApplication = new AwsLambdaApplication("AwsLambdaChainCallingApplication", "AwsLambdaDTTestCaller"); //caller
            InitializeCallingApplication();
        }

        public void WarmUpCalleeApp()
        {
            CalleeApplication.ExerciseFunction();
            Thread.Sleep(5000);
        }

        public void ExerciseChainTest()
        {
            var calleeResourceId = CalleeApplication.TestConfiguration["ApplicationGatewayResourceId"];
            var calleeUrl = $"https://{calleeResourceId}.execute-api.us-west-2.amazonaws.com/test/{CalleeApplication.ApplicationName}";

            var queryString = $"calleeUrl={calleeUrl}";

            CallerApplication.ExerciseFunction(null, queryString);
            Thread.Sleep(5000);
        }

        private void InitializeCallingApplication()
        {
            CallerApplication.TestLogger = TestLogger;
            CallerApplication.InstallAgent();
            CallerApplication.BuildAndDeploy();
        }
    }
}
