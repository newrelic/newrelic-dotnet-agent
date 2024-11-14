// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures.AwsLambda
{
    public abstract class LambdaOutOfOrderParameterFixtureBase : LambdaSelfExecutingAssemblyFixture
    {
        protected LambdaOutOfOrderParameterFixtureBase(string targetFramework) :
            base(targetFramework,
                null,
                "LambdaSelfExecutingAssembly::LambdaSelfExecutingAssembly.Program::OutOfOrderParametersHandler",
                "OutOfOrderParameters",
                null)
        {
        }

        public void EnqueueTrigger()
        {
            var json = "\"foo\"";
            EnqueueLambdaEvent(json);
        }
    }

    public class LambdaOutOfOrderParameterFixtureNet8 : LambdaOutOfOrderParameterFixtureBase
    {
        public LambdaOutOfOrderParameterFixtureNet8() : base("net8.0") { }
    }

    public class LambdaOutOfOrderParameterFixtureNet9 : LambdaOutOfOrderParameterFixtureBase
    {
        public LambdaOutOfOrderParameterFixtureNet9() : base("net9.0") { }
    }
}
