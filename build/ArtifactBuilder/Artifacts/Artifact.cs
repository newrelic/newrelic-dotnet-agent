using System;

namespace ArtifactBuilder.Artifacts
{
    public abstract class Artifact
    {
        public Artifact(string name)
        {
            Name = name;

            RepoRootDirectory = FileHelpers.GetRepoRootDirectory();
            StagingDirectory = $@"{RepoRootDirectory}\Build\_staging\{Name}";
            PackageDirectory = $@"{RepoRootDirectory}\Build\Packaging\{Name}";
            OutputDirectory = $@"{RepoRootDirectory}\Build\BuildArtifacts\{Name}";

            HomeRootDirectory = FileHelpers.GetHomeRootDirectory();
        }

        public string RepoRootDirectory { get; }
        public string HomeRootDirectory { get; }
        public string Name { get; }

        protected string StagingDirectory;
        protected string PackageDirectory;
        protected string OutputDirectory;
        protected Action ValidateContentAction;

        public void Build(bool clearOutput = true)
        {
            FileHelpers.DeleteDirectories(StagingDirectory);
            if (clearOutput)
                FileHelpers.DeleteDirectories(OutputDirectory);

            InternalBuild();
            ValidateContentAction?.Invoke();
        }

        protected abstract void InternalBuild();
    }
}
