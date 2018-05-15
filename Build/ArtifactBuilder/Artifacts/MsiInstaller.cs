using System;
using System.IO;
using System.Linq;

namespace ArtifactBuilder.Artifacts
{
	class MsiInstaller : Artifact
	{
		public string Configuration { get; }
		public string Platform { get; }
		public string MsiDirectory { get; }

		public MsiInstaller(string sourceDirectory, string platform, string configuration) : base(sourceDirectory, "MsiInstaller")
		{
			Platform = platform;
			Configuration = configuration;
			MsiDirectory = $@"{sourceDirectory}\Agent\_build\{Platform}-{Configuration}\Installer";
			OutputDirectory = $@"{SourceDirectory}\Build\BuildArtifacts\{Name}-{Platform}";
		}

		protected override void InternalBuild()
		{
			if (!Directory.Exists(MsiDirectory))
			{
				Console.WriteLine("Warning: The {0} directory does not exist.", MsiDirectory);
				return;
			}

			var fileSearchPattern = $@"NewRelicAgent_{Platform}_*.msi";
			var msiPath = Directory.GetFiles(MsiDirectory, fileSearchPattern).FirstOrDefault();

			if (string.IsNullOrEmpty(msiPath))
			{
				Console.WriteLine("Warning: The {0} installer could not be found.", fileSearchPattern);
				return;
			}

			FileHelpers.CopyFile(msiPath, OutputDirectory);
			File.WriteAllText($@"{OutputDirectory}\checksum.sha256" , FileHelpers.GetSha256Checksum(msiPath));
		}
	}
}
