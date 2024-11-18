// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures.AwsLambda
{
    public abstract class LambdaCustomParametersFixtureBase : LambdaSelfExecutingAssemblyFixture
    {
        protected LambdaCustomParametersFixtureBase(string targetFramework, bool isAsync) :
            base(targetFramework,
                null,
                "LambdaSelfExecutingAssembly::LambdaSelfExecutingAssembly.Program::StringInputAndOutputHandler" + (isAsync? "Async" : ""),
                "StringInputAndOutput" + (isAsync? "Async" : ""),
                null)
        {
        }

        public void EnqueueTrigger()
        {
            var json = "\"Foo\"";
            EnqueueLambdaEvent(json);
        }
    }

    public class LambdaCustomParametersFixtureCoreOldest : LambdaCustomParametersFixtureBase
    {
        public LambdaCustomParametersFixtureCoreOldest() : base(CoreOldestTFM, false) { }
    }

    public class LambdaCustomParametersAsyncFixtureCoreOldest : LambdaCustomParametersFixtureBase
    {
        public LambdaCustomParametersAsyncFixtureCoreOldest() : base(CoreOldestTFM, true) { }
    }

    public class LambdaCustomParametersFixtureCoreLatest : LambdaCustomParametersFixtureBase
    {
        public LambdaCustomParametersFixtureCoreLatest() : base(CoreLatestTFM, false) { }
    }

    public class LambdaCustomParametersAsyncFixtureCoreLatest : LambdaCustomParametersFixtureBase
    {
        public LambdaCustomParametersAsyncFixtureCoreLatest() : base(CoreLatestTFM, true) { }
    }
}
