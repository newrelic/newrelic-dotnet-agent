// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures.AwsLambda
{
    public abstract class LambdaContextOnlyParameterFixtureBase : LambdaSelfExecutingAssemblyFixture
    {
        protected LambdaContextOnlyParameterFixtureBase(string targetFramework) :
            base(targetFramework,
                null,
                "LambdaSelfExecutingAssembly::LambdaSelfExecutingAssembly.Program::LambdaContextOnlyHandler",
                "LambdaContextOnly",
                null)
        {
        }

        public void EnqueueTrigger()
        {
            var json = "{}";
            EnqueueLambdaEvent(json);
        }
    }

    public class LambdaContextOnlyParameterFixtureCoreOldest : LambdaContextOnlyParameterFixtureBase
    {
        public LambdaContextOnlyParameterFixtureCoreOldest() : base(CoreOldestTFM) { }
    }

    public class LambdaContextOnlyParameterFixtureCoreLatest : LambdaContextOnlyParameterFixtureBase
    {
        public LambdaContextOnlyParameterFixtureCoreLatest() : base(CoreLatestTFM) { }
    }
}
