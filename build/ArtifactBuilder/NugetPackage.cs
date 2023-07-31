using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ArtifactBuilder
{
    public class NugetPackage
    {
        public NugetPackage(string stagingDirectory, string outputDirectory)
        {
            StagingDirectory = stagingDirectory;
            OutputDirectory = outputDirectory;
        }

        public string StagingDirectory { get; }
        public string OutputDirectory { get; }
        public string ContentDirectory => $@"{StagingDirectory}\content";

        private string NuspecFilePath => Directory.GetFiles(StagingDirectory, "*.nuspec").First();

        public void SetVersion(string version)
        {
            var xml = new System.Xml.XmlDocument();
            xml.Load(NuspecFilePath);
            string xmlns = xml.DocumentElement.NamespaceURI;
            var ns = new System.Xml.XmlNamespaceManager(xml.NameTable);
            ns.AddNamespace("x", xmlns);
            var nodeVersion = (System.Xml.XmlElement)xml.SelectSingleNode("//x:package/x:metadata/x:version", ns);
            nodeVersion.InnerText = version;
            xml.Save(NuspecFilePath);
        }

        public void SetVersionFromDll(string dllPath, string preReleaseSuffix = null)
        {
            var version = System.Diagnostics.FileVersionInfo.GetVersionInfo(dllPath).FileVersion;
            if (!string.IsNullOrEmpty(preReleaseSuffix)) version += $"-{preReleaseSuffix}";
            SetVersion($"{version}");
        }

        public void CopyAll(string sourceDirectory, string subDirectory = null)
        {
            FileHelpers.CopyAll(sourceDirectory, $@"{StagingDirectory}\{subDirectory}");
        }

        public void CopyToContent(string filePath, string subDirectory = null)
        {
            FileHelpers.CopyFile(filePath, $@"{StagingDirectory}\content\{subDirectory}");
        }

        public void CopyToContent(IEnumerable<string> filePaths, string subDirectory = null)
        {
            FileHelpers.CopyFile(filePaths, $@"{StagingDirectory}\content\{subDirectory}");
        }

        public string GetContentFilesDirectory(string language, string targetFrameworkMoniker)
        {
            return $@"{StagingDirectory}\contentFiles\{language}\{targetFrameworkMoniker}";
        }

        public void CopyToLib(string filePath, string targetFrameworkMoniker = null)
        {
            FileHelpers.CopyFile(filePath, $@"{StagingDirectory}\lib\{targetFrameworkMoniker}");
        }

        public void CopyToLib(IEnumerable<string> filePaths, string targetFrameworkMoniker)
        {
            FileHelpers.CopyFile(filePaths, $@"{StagingDirectory}\lib\{targetFrameworkMoniker}");
        }

        public void CopyToContentFiles(string filePath, string subDirectory = null)
        {
            FileHelpers.CopyFile(filePath, $@"{StagingDirectory}\contentFiles\{subDirectory}");
        }

        public void CopyToContentFiles(IEnumerable<string> filePaths, string subDirectory = null)
        {
            FileHelpers.CopyFile(filePaths, $@"{StagingDirectory}\contentFiles\{subDirectory}");
        }

        public void CopyToTools(string filePath, string subDirectory = null)
        {
            FileHelpers.CopyFile(filePath, $@"{StagingDirectory}\tools\{subDirectory}");
        }

        public void CopyToTools(IEnumerable<string> filePaths, string subDirectory = null)
        {
            FileHelpers.CopyFile(filePaths, $@"{StagingDirectory}\tools\{subDirectory}");
        }

        public void CopyToRoot(string filePath, string subDirectory = null)
        {
            FileHelpers.CopyFile(filePath, $@"{StagingDirectory}\{subDirectory}");
        }

        public void CopyToRoot(IEnumerable<string> filePaths, string subDirectory = null)
        {
            FileHelpers.CopyFile(filePaths, $@"{StagingDirectory}\{subDirectory}");
        }

        public string Pack()
        {
            return NuGetHelpers.Pack(NuspecFilePath, OutputDirectory);
        }
    }
}
