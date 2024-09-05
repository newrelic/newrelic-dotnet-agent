// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public abstract class AzureFunctionApplicationFixture : RemoteApplicationFixture
    {
        private const string ApplicationDirectoryName = @"AzureFunctionApplication";

        protected AzureFunctionApplicationFixture(string functionName, string targetFramework)
            : base(new AzureFuncTool(ApplicationDirectoryName, targetFramework, ApplicationType.Bounded, true, true, true))
        {
            CommandLineArguments = $"start --no-build --language-worker dotnet-isolated --dotnet-isolated --functions {functionName} ";

#if DEBUG
            // set a long timeout if you're going to debug into the function
            CommandLineArguments += "--timeout 600 ";
#endif
        }


        public string Get(string endpoint)
        {
            var address = $"http://{DestinationServerName}:{Port}/{endpoint}";

            return GetString(address);
        }
    }

    public class AzureFunctionApplicationFixture_Function1_CoreLatest : AzureFunctionApplicationFixture
    {
        public AzureFunctionApplicationFixture_Function1_CoreLatest() : base("function1", "net8.0")
        {
        }
    }
}
