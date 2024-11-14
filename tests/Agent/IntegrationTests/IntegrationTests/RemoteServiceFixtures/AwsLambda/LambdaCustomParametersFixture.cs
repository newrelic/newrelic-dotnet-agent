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

    public class LambdaCustomParametersFixtureNet8 : LambdaCustomParametersFixtureBase
    {
        public LambdaCustomParametersFixtureNet8() : base("net8.0", false) { }
    }

    public class LambdaCustomParametersAsyncFixtureNet8 : LambdaCustomParametersFixtureBase
    {
        public LambdaCustomParametersAsyncFixtureNet8() : base("net8.0", true) { }
    }

    public class LambdaCustomParametersFixtureNet9 : LambdaCustomParametersFixtureBase
    {
        public LambdaCustomParametersFixtureNet9() : base("net9.0", false) { }
    }

    public class LambdaCustomParametersAsyncFixtureNet9 : LambdaCustomParametersFixtureBase
    {
        public LambdaCustomParametersAsyncFixtureNet9() : base("net9.0", true) { }
    }
}
