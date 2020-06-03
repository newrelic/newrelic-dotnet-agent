namespace ArtifactBuilder.Artifacts
{
    public abstract class Artifact
    {
        public Artifact(string name)
        {
            SourceDirectory = FileHelpers.GetSourceDirectory();
            Name = name;
            StagingDirectory = $@"{SourceDirectory}\Build\_staging\{Name}";
            PackageDirectory = $@"{SourceDirectory}\Build\Packaging\{Name}";
            OutputDirectory = $@"{SourceDirectory}\Build\BuildArtifacts\{Name}";
        }

        public string SourceDirectory { get; }
        public string Name { get; }

        protected string StagingDirectory;
        protected string PackageDirectory;
        protected string OutputDirectory;

        public void Build()
        {
            FileHelpers.DeleteDirectories(StagingDirectory, OutputDirectory);
            InternalBuild();
        }

        protected abstract void InternalBuild();
    }
}
