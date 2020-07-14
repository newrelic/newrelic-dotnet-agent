namespace ArtifactBuilder.Artifacts
{
	public abstract class Artifact
	{
		public Artifact(string sourceDirectory, string name)
		{
			SourceDirectory = sourceDirectory;
			Name = name;
			StagingDirectory = $@"{SourceDirectory}\build\_staging\{Name}";
			PackageDirectory = $@"{SourceDirectory}\build\Packaging\{Name}";
			OutputDirectory = $@"{SourceDirectory}\build\BuildArtifacts\{Name}";
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