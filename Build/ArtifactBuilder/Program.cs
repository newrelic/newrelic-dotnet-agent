using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArtifactBuilder.Artifacts;

namespace ArtifactBuilder
{
    class Program
    {
        static int Main(string[] args)
        {            
            try
            {
                var sourceDirectory = GetSourceDirectory();
                var package = args[0].ToLower();

                switch (package)
                {
                    case "nugetazurewebsites":
                        BuildNugetAzureWebsites(sourceDirectory, args);
                        break;
                    case "azuresiteextension":
                        BuildAzureSiteExtension(sourceDirectory, args);
                        break;
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

        private static NugetPushInfo NugetPushInfo = new NugetPushInfo("http://win-nuget-repository.pdx.vm.datanerd.us:81/nuget/Default", "C7B30E3332814310896ADB3DEC35F491");

        private static string GetSourceDirectory()
        {
            var dirInfo = new DirectoryInfo(System.Environment.CurrentDirectory);
            return dirInfo.Parent.FullName;
        }

        private static void BuildNugetAzureWebsites(string sourceDirectory, string[] args)
        {
            var configuration = args[1];
            var platform = args[2];
            var pushNugetPackage = args.Length == 4;
            var c = new NugetAzureWebSites(platform, configuration, sourceDirectory, pushNugetPackage ? NugetPushInfo : null);
            c.Build();
        }

        private static void BuildAzureSiteExtension(string sourceDirectory, string[] args)
        {
            var version = args[1];
            var c = new AzureSiteExtension(version, sourceDirectory);
            c.Build();
        }
    }
}
