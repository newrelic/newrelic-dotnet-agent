namespace ArtifactBuilder.Artifacts
{
    public class NugetAwsLambdaOpenTracer : Artifact
    {
        public NugetAwsLambdaOpenTracer(string configuration)
            : base(nameof(NugetAwsLambdaOpenTracer))
        {
            Configuration = configuration;
        }

        public string Configuration { get; }

        protected override void InternalBuild()
        {
            var targetFrameworkMoniker = "netstandard2.0";
            var component = $@"{RepoRootDirectory}\src\AwsLambda\AwsLambdaOpenTracer\bin\{Configuration}\{targetFrameworkMoniker}-ILRepacked\NewRelic.OpenTracing.AmazonLambda.Tracer.dll";
            var package = new NugetPackage(StagingDirectory, OutputDirectory);
            package.CopyAll($@"{PackageDirectory}");
            package.CopyToLib(component, "netstandard2.0");
            package.SetVersionFromDll(component);
            package.Pack();
        }

        protected override string Unpack()
        {
            throw new System.NotImplementedException();
        }

        protected override void ValidateContent()
        {
            throw new System.NotImplementedException();
        }
    }
}
