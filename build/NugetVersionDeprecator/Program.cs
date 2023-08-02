using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NugetVersionDeprecator
{
    internal class Program
    {
        private const string RepoUrl = "https://api.nuget.org/v3/index.json";

        // Need: A list of package names to query
        // NewRelic.Agent
        // NewRelic.Agent.API
        // NewRelicWindowsAzure (?)
        // NewRelic.Azure.Websites
        // NewRelic.Azure.Websites.x64

        static async Task Main(string[] args)

        {
            Console.WriteLine("Hello, World!");

            // query the docs API to get a current list of .NET Agent versions

            // query NuGet for a current list of non-deprecated versions of all .NET Agent packages
            SourceCacheContext cache = new SourceCacheContext();
            SourceRepository repository = Repository.Factory.GetCoreV3(RepoUrl);
            PackageMetadataResource resource = await repository.GetResourceAsync<PackageMetadataResource>();
            List<IPackageSearchMetadata> packages = (await resource.GetMetadataAsync(
                "NewRelic.",
                includePrerelease: false,
                includeUnlisted: false,
                cache,
                NullLogger.Instance,
                CancellationToken.None)).ToList();
                    

            // intersect the two lists and build a list of NuGet versions that should be deprecated

            // iterate the list of versions to deprecate and call the NuGet API to deprecate them
        }
    }
}
