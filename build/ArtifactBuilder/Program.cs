/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.IO;
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
                    case "scriptableinstaller":
                        BuildScriptableInstaller(args);
                        break;
                    case "nugetazurewebsites":
                        BuildNugetAzureWebsites(args);
                        break;
                    case "nugetagentapi":
                        BuildNugetAgentApi(args);
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

        private static string GetSourceDirectory()
        {
            var dirInfo = new DirectoryInfo(System.Environment.CurrentDirectory);
            return dirInfo.Parent.FullName;
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

        private static void BuildNugetAzureCloudServices(string[] args)
        {
            var configuration = args[1];
            var c = new NugetAzureCloudServices(configuration);
            c.Build();
        }

        private static void BuildZipArchives(string[] args)
        {
            var configuration = args[1];
            new ZipArchive(AgentType.Framework, "x64", configuration).Build();
            new ZipArchive(AgentType.Framework, "x86", configuration).Build();
        }

        private static void BuildScriptableInstaller(string[] args)
        {
            var configuration = args[1];
            new ScriptableInstaller(configuration).Build();
        }

    }
}
