using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ArtifactBuilder
{
	public class AzureSiteExtension
	{
		public AzureSiteExtension(string version, string sourceDirectory)
		{
			Version = version;
			SourceDirectory = sourceDirectory;
		}

		public string SourceDirectory { get; }
		public string Version { get; }
		public string Name => "AzureSiteExtension";
		public string StagingDirectory => $@"{SourceDirectory}\Build\_staging\{Name}";
		public string PackageDirectory => $@"{SourceDirectory}\Build\Packaging\{Name}";
		private string OutputDirectory => $@"{SourceDirectory}\Build\BuildArtifacts\{Name}";

		public void Build()
		{
			FileHelpers.DeleteDirectories(StagingDirectory, OutputDirectory);
			CopyComponents();
			TransformNuspecFile();
			NuGetHelpers.Pack(Directory.GetFiles(StagingDirectory, "*.nuspec").First(), OutputDirectory);
		}

		private void CopyComponents()
		{
			FileHelpers.CopyAll($@"{PackageDirectory}", $@"{StagingDirectory}");
			FileHelpers.CopyFile($@"{SourceDirectory}\Build\NewRelic.NuGetHelper\bin\NewRelic.NuGetHelper.dll", $@"{StagingDirectory}\content");
			FileHelpers.CopyFile($@"{SourceDirectory}\Build\NewRelic.NuGetHelper\bin\NuGet.Core.dll", $@"{StagingDirectory}\content");
			FileHelpers.CopyFile($@"{SourceDirectory}\Build\NewRelic.NuGetHelper\bin\Microsoft.Web.XmlTransform.dll", $@"{StagingDirectory}\content");
		}

		private void TransformNuspecFile()
		{
			var path = Directory.GetFiles(StagingDirectory, "*.nuspec").First();
			var xml = new System.Xml.XmlDocument();
			xml.Load(path);

			var nodeVersion = (System.Xml.XmlElement) xml.SelectSingleNode("//package/metadata/version");
			nodeVersion.InnerText = Version;
			xml.Save(path);
		}
	}
}
