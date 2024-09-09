// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public abstract class AzureFunctionApplicationFixture : RemoteApplicationFixture
    {
        private const string ApplicationDirectoryName = @"AzureFunctionApplication";

        protected AzureFunctionApplicationFixture(string functionNames, string targetFramework, bool enableAzureFunctionMode)
            : base(new AzureFuncTool(ApplicationDirectoryName, targetFramework, ApplicationType.Bounded, true, true, true, enableAzureFunctionMode))
        {
            CommandLineArguments = $"start --no-build --language-worker dotnet-isolated --dotnet-isolated --functions {functionNames} ";

#if DEBUG
            // set a long timeout if you're going to debug into the function
            CommandLineArguments += "--timeout 600 ";
#endif

            AzureFunctionModeEnabled = enableAzureFunctionMode;
        }


        public string Get(string endpoint)
        {
            var address = $"http://{DestinationServerName}:{Port}/{endpoint}";

            return GetString(address);
        }

        public void PostToAzureFuncTool(string triggerName, string payload)
        {
            var address = $"http://{DestinationServerName}:{Port}/admin/functions/{triggerName}";

            var inputPayload = $$"""{"input":"{{payload}}"}""";
            PostJson(address, inputPayload);
        }

        public bool AzureFunctionModeEnabled { get; }
    }

    public class AzureFunctionApplicationFixtureHttpTriggerCoreOldest : AzureFunctionApplicationFixture
    {
        public AzureFunctionApplicationFixtureHttpTriggerCoreOldest() : base("httpTriggerFunctionUsingAspNetCorePipeline httpTriggerFunctionUsingSimpleInvocation", "net6.0", true)
        {
        }
    }
    public class AzureFunctionApplicationFixtureHttpTriggerCoreLatest : AzureFunctionApplicationFixture
    {
        public AzureFunctionApplicationFixtureHttpTriggerCoreLatest() : base("httpTriggerFunctionUsingAspNetCorePipeline httpTriggerFunctionUsingSimpleInvocation", "net8.0", true)
        {
        }
    }
    public class AzureFunctionApplicationFixtureInstrumentationDisabledCoreLatest : AzureFunctionApplicationFixture
    {
        public AzureFunctionApplicationFixtureInstrumentationDisabledCoreLatest() : base("httpTriggerFunctionUsingAspNetCorePipeline httpTriggerFunctionUsingSimpleInvocation", "net8.0", false)
        {
        }
    }

    public class AzureFunctionApplicationFixtureQueueTriggerCoreOldest : AzureFunctionApplicationFixture
    {
        public AzureFunctionApplicationFixtureQueueTriggerCoreOldest() : base("queueTriggerFunction", "net6.0", true)
        {
        }
    }
    public class AzureFunctionApplicationFixtureQueueTriggerCoreLatest : AzureFunctionApplicationFixture
    {
        public AzureFunctionApplicationFixtureQueueTriggerCoreLatest() : base("queueTriggerFunction", "net8.0", true)
        {
        }
    }
}
