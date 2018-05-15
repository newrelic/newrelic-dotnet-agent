using System;
using System.IO;
using System.Linq;

namespace ArtifactBuilder.Artifacts
{
	public class LinuxPackage : Artifact
	{
		private readonly string _packagePlatform;
		private readonly string _fileExtension;
		private readonly string _buildOutputDirectory;

		public LinuxPackage(string sourceDirectory, string name, string packagePlatform, string fileExtension) : base(sourceDirectory, name)
		{
			_packagePlatform = packagePlatform;
			_fileExtension = fileExtension;
			_buildOutputDirectory = $@"{sourceDirectory}\Agent\_build\CoreArtifacts";
			OutputDirectory = $@"{SourceDirectory}\Build\BuildArtifacts\{Name}";
		}

		protected override void InternalBuild()
		{
			//Note that the wildcard purposely includes the separator and the version number
			var fileNameSearchPattern = $"newrelic-netcore20-agent*{_packagePlatform}.{_fileExtension}";
			var packagePath = Directory.GetFiles(_buildOutputDirectory, fileNameSearchPattern).FirstOrDefault();

			if (string.IsNullOrEmpty(packagePath))
			{
				Console.WriteLine("Warning: The {0} package could not be found.", fileNameSearchPattern);
				return;
			}

			Directory.CreateDirectory(OutputDirectory);

			var fileInfo = new FileInfo(packagePath);
			File.Copy(fileInfo.FullName, $@"{OutputDirectory}\{fileInfo.Name}", true);

			//Generate the checksum file based on the Linux standards for the checksum contents: <checksum_value> <checksum_mode><filename>
			//<checksum_mode> is either a ' ' for text mode or '*' for binary mode.
			File.WriteAllText($@"{OutputDirectory}\checksum.sha256", $"{FileHelpers.GetSha256Checksum(packagePath)} *{fileInfo.Name}");
		}
	}
}
