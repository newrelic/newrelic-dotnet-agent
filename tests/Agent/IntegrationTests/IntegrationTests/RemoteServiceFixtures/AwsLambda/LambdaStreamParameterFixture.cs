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

    public class LambdaStreamParameterFixtureNet8 : LambdaStreamParameterFixtureBase
    {
        public LambdaStreamParameterFixtureNet8() : base("net8.0") { }
    }

    public class LambdaStreamParameterFixtureNet9 : LambdaStreamParameterFixtureBase
    {
        public LambdaStreamParameterFixtureNet9() : base("net9.0") { }
    }
}
