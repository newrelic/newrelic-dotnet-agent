// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures.AwsLambda
{
    public abstract class LambdaCustomEventsTriggerFixtureBase : LambdaSelfExecutingAssemblyFixture
    {
        protected LambdaCustomEventsTriggerFixtureBase(string targetFramework) :
            base(targetFramework,
                null,
                "LambdaSelfExecutingAssembly::LambdaSelfExecutingAssembly.Program::CustomEventHandler",
                "CustomEvent",
                null)
        {
        }

        public void EnqueueTrigger()
        {
            var json = "{}";
            EnqueueLambdaEvent(json);
        }
    }

    public class LambdaCustomEventsTriggerFixtureNet8 : LambdaCustomEventsTriggerFixtureBase
    {
        public LambdaCustomEventsTriggerFixtureNet8() : base("net8.0") { }
    }

    public class LambdaCustomEventsTriggerFixtureNet9 : LambdaCustomEventsTriggerFixtureBase
    {
        public LambdaCustomEventsTriggerFixtureNet9() : base("net9.0") { }
    }
}
