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
        private string NuspecFileName => "NewRelic.Azure.WebSites.nuspec";
        private string NuspecFile => $@"{PackageDirectory}\{NuspecFileName}";
        private string OutputDirectory => $@"{SourceDirectory}\Build\BuildArtifacts\{Name}";

        public void Build()
        {
            CreateStagingDirectory();
            CopyComponents();
            TransformNuspecFile();
            Pack();
        }

        private void CreateStagingDirectory()
        {
            if (System.IO.Directory.Exists(StagingDirectory))
            {
                System.IO.Directory.Delete(StagingDirectory, true);
            }
            System.IO.Directory.CreateDirectory(StagingDirectory);
            System.IO.Directory.CreateDirectory($@"{StagingDirectory}\content");
        }

        private void CopyComponents()
        {
            FileHelpers.CopyAll($@"{PackageDirectory}\content", $@"{StagingDirectory}\content");
            FileHelpers.CopyFile(NuspecFile, StagingDirectory);
        }

        private void Pack()
        {
            if (Directory.Exists(OutputDirectory))
            {
                Directory.Delete(OutputDirectory, true);
            }
            
            var nugetPath = File.Exists(@"C:\Nuget.exe") ? @"C:\Nuget.exe" : "nuget";
            var parameters = $@"Pack -NoPackageAnalysis {StagingDirectory}\{NuspecFileName} -OutputDirectory {OutputDirectory}";
            var process = System.Diagnostics.Process.Start(nugetPath, parameters);
            process.WaitForExit(30000);
            if (!process.HasExited)
            {
                process.Kill();
                throw new Exception($"Nuget pack failed complete in timely fashion.");
            }
            if (process.ExitCode != 0)
            {
                throw new Exception($"Nuget pack failed with exit code {process.ExitCode}.");
            }
        }

        private void TransformNuspecFile()
        {
            var path = $@"{StagingDirectory}\{NuspecFileName}";
            var xml = new System.Xml.XmlDocument();
            xml.Load(path);

            var ns = new System.Xml.XmlNamespaceManager(xml.NameTable);
            ns.AddNamespace("x", "http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd");

            var nodeVersion = (System.Xml.XmlElement) xml.SelectSingleNode("//x:package/x:metadata/x:version", ns);
            nodeVersion.InnerText = Version;
            xml.Save(path);
        }
    }
}
