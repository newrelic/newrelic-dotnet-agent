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

    public class LambdaContextOnlyParameterFixtureNet8 : LambdaContextOnlyParameterFixtureBase
    {
        public LambdaContextOnlyParameterFixtureNet8() : base("net8.0") { }
    }

    public class LambdaContextOnlyParameterFixtureNet9 : LambdaContextOnlyParameterFixtureBase
    {
        public LambdaContextOnlyParameterFixtureNet9() : base("net9.0") { }
    }
}
