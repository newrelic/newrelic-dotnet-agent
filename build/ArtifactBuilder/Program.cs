using System;
using ArtifactBuilder.Artifacts;

namespace ArtifactBuilder
{
    class Program
    {
        static int Main(string[] args)
        {
            try
            {
                var package = args[0].ToLower();

                switch (package)
                {
                    case "ziparchives":
                        BuildZipArchives(args);
                        break;
                    case "nugetazurewebsites":
                        BuildNugetAzureWebsites(args);
                        break;
                    case "azuresiteextension":
                        BuildAzureSiteExtension();
                        break;
                    case "nugetagent":
                        BuildNugetAgent(args);
                        break;
                    case "nugetagentapi":
                        BuildNugetAgentApi(args);
                        break;
                    case "nugetagentextensions":
                        BuildNugetAgentExtensions(args);
                        break;
                    case "nugetazurecloudservices":
                        BuildNugetAzureCloudServices(args);
                        break;
                    case "msiinstaller":
                        BuildMsiInstaller(args);
                        break;
                    case "downloadsite":
                        BuildDownloadSite(args);
                        break;
                    case "linuxpackages":
                        BuildLinuxPackages();
                        break;
                    default:
                        throw new Exception($"Unknown package type: {args[0]}");
                }
            }
            catch (PackagingException e)
            {
                Console.Error.WriteLine(e.Message);
                return 1;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                return 1;
            }

            return 0;
        }

        private static void BuildLinuxPackages()
        {
            new LinuxPackage("LinuxDeb", "_amd64", "deb").Build();
            new LinuxPackage("LinuxDeb", "_arm64", "deb").Build(clearOutput: false);
            new LinuxPackage("LinuxRpm", ".x86_64", "rpm").Build();
            new LinuxPackage("LinuxTar", "_amd64", "tar.gz").Build();
            new LinuxPackage("LinuxTar", "_arm64", "tar.gz").Build(clearOutput: false);
        }

        private static void BuildDownloadSite(string[] args)
        {
            var configuration = args[1];
            new DownloadSiteArtifact(configuration).Build();
        }

        private static void BuildMsiInstaller(string[] args)
        {
            var configuration = args[1];
            new MsiInstaller("x86", configuration).Build();
            new MsiInstaller("x64", configuration).Build();
        }

        private static void BuildNugetAgent(string[] args)
        {
            var configuration = args[1];
            var c = new NugetAgent(configuration);
            c.Build();
        }

        private static void BuildNugetAzureWebsites(string[] args)
        {
            var configuration = args[1];
            var platform = args[2];
            var c = new NugetAzureWebSites(platform, configuration);
            c.Build();
        }

        private static void BuildNugetAgentApi(string[] args)
        {
            var configuration = args[1];
            var c = new NugetAgentApi(configuration);
            c.Build();
        }

        private static void BuildNugetAgentExtensions(string[] args)
        {
            var configuration = args[1];
            var c = new NugetAgentExtensions(configuration);
            c.Build();
        }
        private static void BuildNugetAzureCloudServices(string[] args)
        {
            var configuration = args[1];
            var c = new NugetAzureCloudServices(configuration);
            c.Build();
        }

        private static void BuildZipArchives(string[] args)
        {
            var configuration = args[1];
            new ZipArchive("x64", configuration).Build();
            new ZipArchive("x86", configuration).Build();
        }

        private static void BuildAzureSiteExtension()
        {
            var c = new AzureSiteExtension();
            c.Build();
        }
    }
}
