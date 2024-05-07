// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures.AwsLambda
{
    public abstract class LambdaSelfExecutingAssemblyFixture : LambdaTestToolFixture
    {
        public LambdaSelfExecutingAssemblyFixture(string targetFramework, string newRelicLambdaHandler, string lambdaHandler, string lambdaName, string lambdaVersion) :
            base(new RemoteService("LambdaSelfExecutingAssembly", "LambdaSelfExecutingAssembly.exe", targetFramework, ApplicationType.Bounded, createsPidFile: true, isCoreApp: true, publishApp: true),
                newRelicLambdaHandler,
                lambdaHandler,
                lambdaName,
                lambdaVersion,
                "self executing assembly")
        {
        }

        public string GetTestTraceStateHeaderValue(string spanId)
        {
            return $"{TestConfiguration.NewRelicAccountId}@nr=0-0-{TestConfiguration.NewRelicAccountId}-12345-{spanId}--1-0.789-1563574856827";
        }

        public string GetTestTraceParentHeaderValue(string traceId, string spanId)
        {
            return $"00-{traceId}-{spanId}-01";
        }
    }
}
