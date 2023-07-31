using CommandLine;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NugetValidator
{
    public class Options
    {
        [Option('n', "name", Required = true, HelpText = "NuGet Package Name")]
        public string Name { get; set; }

        [Option('v', "version", Required = true, HelpText = "Package Version")]
        public string Version { get; set; }
    }

    internal class Program
    {
        private const string RepoUrl = "https://api.nuget.org/v3/index.json";

        static async Task<int> Main(string[] args)
        {
            IPackageSearchMetadata? result = null;

            await Parser.Default.ParseArguments<Options>(args)
                .WithParsedAsync(async options =>
                {
                    Console.WriteLine($"Validating that NuGet package {options.Name} with version {options.Version} exists...");

                    SourceCacheContext cache = new SourceCacheContext();
                    SourceRepository repository = Repository.Factory.GetCoreV3(RepoUrl);
                    PackageMetadataResource resource = await repository.GetResourceAsync<PackageMetadataResource>();

                    List<IPackageSearchMetadata> packages = (await resource.GetMetadataAsync(
                        options.Name,
                        includePrerelease: false,
                        includeUnlisted: false,
                        cache,
                        NullLogger.Instance,
                        CancellationToken.None)).ToList();

                    SemanticVersion semVer = SemanticVersion.Parse(options.Version);
                    
                    result = packages.FirstOrDefault(p => p.Identity.Id.ToLower() == options.Name.ToLower() && p.Identity.Version == semVer);

                    Console.WriteLine($"{(result != null ? "Found" : "Did NOT find")} NuGet package {options.Name} with version {options.Version}");
                });

            // return code of 0 indicates success
            return result == null ? -1 : 0;
        }
    }
}
