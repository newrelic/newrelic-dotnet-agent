using System.IO;
using System.Linq;

namespace ArtifactBuilder
{
	public class NugetPackage
	{
		public NugetPackage(string stagingDirectory, string outputDirectory, NugetPushInfo nugetPushInfo)
		{
			StagingDirectory = stagingDirectory;
			OutputDirectory = outputDirectory;
			NugetPushInfo = nugetPushInfo;
		}

		public string StagingDirectory { get; }
		public string OutputDirectory { get; }
		public NugetPushInfo NugetPushInfo { get; }

		private string NuspecFilePath => Directory.GetFiles(StagingDirectory, "*.nuspec").First();

		public void SetVersion(string version)
		{
			var xml = new System.Xml.XmlDocument();
			xml.Load(NuspecFilePath);
			string xmlns = xml.DocumentElement.NamespaceURI;
			var ns = new System.Xml.XmlNamespaceManager(xml.NameTable);
			ns.AddNamespace("x", xmlns);
			var nodeVersion = (System.Xml.XmlElement) xml.SelectSingleNode("//x:package/x:metadata/x:version", ns);
			nodeVersion.InnerText = version;
			xml.Save(NuspecFilePath);
		}

		public void CopyAll(string sourceDirectory)
		{
			FileHelpers.CopyAll(sourceDirectory, StagingDirectory);
		}

		public void CopyToContent(string filePath)
		{
			FileHelpers.CopyFile(filePath, $@"{StagingDirectory}\content");
		}

		public void CopyToLib(string filePath, string targetFrameworkMoniker)
		{
			FileHelpers.CopyFile(filePath, $@"{StagingDirectory}\lib\{targetFrameworkMoniker}");
		}

		public void Pack()
		{
			NuGetHelpers.Pack(NuspecFilePath, OutputDirectory);
		}

		public void Push()
		{
			NuGetHelpers.Push(NugetPushInfo, OutputDirectory);
		}
	}
}