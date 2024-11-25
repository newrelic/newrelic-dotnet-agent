// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures.AwsLambda
{
    public abstract class LambdaStreamParameterFixtureBase : LambdaSelfExecutingAssemblyFixture
    {
        protected LambdaStreamParameterFixtureBase(string targetFramework) :
            base(targetFramework,
                null,
                "LambdaSelfExecutingAssembly::LambdaSelfExecutingAssembly.Program::StreamParameterHandler",
                "StreamParameter",
                null)
        {
        }

        public void EnqueueTrigger()
        {
            var json = "\"fizz\"";
            EnqueueLambdaEvent(json);
        }
    }

    public class LambdaStreamParameterFixtureCoreOldest : LambdaStreamParameterFixtureBase
    {
        public LambdaStreamParameterFixtureCoreOldest() : base(CoreOldestTFM) { }
    }

    public class LambdaStreamParameterFixtureCoreLatest : LambdaStreamParameterFixtureBase
    {
        public LambdaStreamParameterFixtureCoreLatest() : base(CoreLatestTFM) { }
    }
}
