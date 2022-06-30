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
            var coreAgentComponents = AgentComponents.GetAgentComponents(AgentType.Core, Configuration, "x64", RepoRootDirectory, HomeRootDirectory);
            frameworkAgentComponents.ValidateComponents();
            coreAgentComponents.ValidateComponents();

            var package = new NugetPackage(StagingDirectory, OutputDirectory);
            package.CopyAll(PackageDirectory);
            package.CopyToLib(frameworkAgentComponents.AgentApiDll, "net462");
            package.CopyToLib(coreAgentComponents.AgentApiDll, "netstandard2.0");
            package.CopyToRoot(frameworkAgentComponents.NewRelicLicenseFile);
            package.CopyToRoot(frameworkAgentComponents.NewRelicThirdPartyNoticesFile);
            package.SetVersion(frameworkAgentComponents.Version);
            package.Pack();
        }
    }
}
