using System.IO;
using System.Linq;

namespace ArtifactBuilder.Artifacts
{
	public class AzureSiteExtension : Artifact
	{
		public AzureSiteExtension(string version, string sourceDirectory): base(sourceDirectory, nameof(AzureSiteExtension))
		{
			Version = version;
		}
		
		public string Version { get; }

		protected override void InternalBuild()
		{
			var package = new NugetPackage(StagingDirectory, OutputDirectory);
			package.CopyAll($@"{PackageDirectory}");
			package.CopyToContent($@"{SourceDirectory}\Build\NewRelic.NuGetHelper\bin\NewRelic.NuGetHelper.dll");
			package.CopyToContent($@"{SourceDirectory}\Build\NewRelic.NuGetHelper\bin\NuGet.Core.dll");
			package.CopyToContent($@"{SourceDirectory}\Build\NewRelic.NuGetHelper\bin\Microsoft.Web.XmlTransform.dll");
			package.SetVersion(Version);
			package.Pack();
		}
	}
}
