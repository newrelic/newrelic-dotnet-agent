namespace ArtifactBuilder.Artifacts
{
	public abstract class Artifact
	{
		public Artifact(string sourceDirectory, string name)
		{
			SourceDirectory = sourceDirectory;
			Name = name;
		}

		public string SourceDirectory { get; }
		public string Name { get; }

		protected string StagingDirectory => $@"{SourceDirectory}\Build\_staging\{Name}";
		protected string PackageDirectory => $@"{SourceDirectory}\Build\Packaging\{Name}";
		protected string OutputDirectory => $@"{SourceDirectory}\Build\BuildArtifacts\{Name}";

		public void Build()
		{
			FileHelpers.DeleteDirectories(StagingDirectory, OutputDirectory);
			InternalBuild();
		}

		protected abstract void InternalBuild();
	}
}