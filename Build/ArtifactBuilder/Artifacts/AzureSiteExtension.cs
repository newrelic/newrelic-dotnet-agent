namespace ArtifactBuilder.Artifacts
{
	public class AzureSiteExtension : Artifact
	{
		public AzureSiteExtension(string sourceDirectory): base(sourceDirectory, nameof(AzureSiteExtension))
		{
		}

		protected override void InternalBuild()
		{
			var version = ReadVersionFromFile();
			var package = new NugetPackage(StagingDirectory, OutputDirectory);
			package.CopyAll($@"{PackageDirectory}");
			package.CopyToContent($@"{SourceDirectory}\Build\NewRelic.NuGetHelper\bin\NewRelic.NuGetHelper.dll");
			package.CopyToContent($@"{SourceDirectory}\Build\NewRelic.NuGetHelper\bin\NuGet.Core.dll");
			package.CopyToContent($@"{SourceDirectory}\Build\NewRelic.NuGetHelper\bin\Microsoft.Web.XmlTransform.dll");
			package.SetVersion(version);
			package.Pack();
		}

		private string ReadVersionFromFile()
		{
			var versionFile  = $@"{SourceDirectory}\Build\BuildArtifacts\_buildProperties\version_azuresiteextension.txt";

			if (!System.IO.File.Exists(versionFile))
			{
				throw new PackagingException($"Version file does not exist: {versionFile}");
			}

			try
			{
				return System.IO.File.ReadAllLines(versionFile)[0];
			}
			catch
			{
				throw new PackagingException($"Failed to read version file from: {versionFile}");
			}
		}
	}
}
