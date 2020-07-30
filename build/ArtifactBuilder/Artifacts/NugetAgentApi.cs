/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
namespace ArtifactBuilder.Artifacts
{
    public class NugetAgentApi : Artifact
    {
        public NugetAgentApi(string configuration)
            : base(nameof(NugetAgentApi))
        {
            Configuration = configuration;
        }

        public string Configuration { get; }

        protected override void InternalBuild()
        {
            var frameworkAgentComponents = AgentComponents.GetAgentComponents(AgentType.Framework, Configuration, "x64", RepoRootDirectory, HomeRootDirectory);
            frameworkAgentComponents.ValidateComponents();

            var package = new NugetPackage(StagingDirectory, OutputDirectory);
            package.CopyAll(PackageDirectory);
            package.CopyToLib(frameworkAgentComponents.AgentApiDll, "net35");
            package.SetVersion(frameworkAgentComponents.Version);
            package.Pack();
        }
    }
}
