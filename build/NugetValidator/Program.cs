// Copyright 2023 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using CommandLine;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NugetValidator;

internal class Program
{
    private const string RepoUrl = "https://api.nuget.org/v3/index.json";

    static async Task<int> Main(string[] args)
    {
        var options = Parser.Default.ParseArguments<Options>(args)
            .WithParsed(ValidateOptions)
            .WithNotParsed(HandleParseError)
            .Value;

        var configuration = LoadConfiguration(options.ConfigurationPath);

        var validationFailed = await ValidatePackagesAsync(options, configuration);

        // return code of 0 indicates success
        var exitCode = validationFailed ? 1 : 0;
        Console.WriteLine($"Exit code: {exitCode}");

        return exitCode;
    }

    static async Task<bool> ValidatePackagesAsync(Options options, Configuration configuration)
    {
        var validationFailed = false;

        try
        {
            SourceCacheContext cache = new SourceCacheContext();
            SourceRepository repository = Repository.Factory.GetCoreV3(RepoUrl);

            PackageMetadataResource resource = await repository.GetResourceAsync<PackageMetadataResource>();
            var nugetVersion = NuGetVersion.Parse(options.Version);

            foreach (var packageName in configuration.Packages)
            {
                Console.WriteLine($"Validating that NuGet package {packageName} with version {options.Version} exists...");

                List<IPackageSearchMetadata> packages = (await resource.GetMetadataAsync(
                    packageName,
                    includePrerelease: false,
                    includeUnlisted: false,
                    cache,
                    new ConsoleNugetLogger(),
                    CancellationToken.None)).ToList();

                var result = packages.FirstOrDefault(p =>
                    string.Equals(p.Identity.Id, packageName, StringComparison.CurrentCultureIgnoreCase) &&
                    p.Identity.Version == nugetVersion);

                Console.WriteLine($"{(result != null ? "Found" : "Did NOT find")} NuGet package {packageName} with version {options.Version}");

                validationFailed |= (result == null);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Unexpected exception: {e}");
            throw;
        }

        return validationFailed;
    }

    private static Configuration LoadConfiguration(string path)
    {
        var input = File.ReadAllText(path);
        var deserializer = new YamlDotNet.Serialization.Deserializer();
        return deserializer.Deserialize<Configuration>(input);
    }


    private static void ValidateOptions(Options opts)
    {
        if (string.IsNullOrWhiteSpace(opts.Version)
            || string.IsNullOrWhiteSpace(opts.ConfigurationPath))
        {
            ExitWithError(ExitCode.BadArguments, "Arguments were empty or whitespace.");
        }

        if (!Version.TryParse(opts.Version, out _))
        {
            ExitWithError(ExitCode.Error, $"Version provided, '{opts.Version}', was not a valid version.");
        }

        if (!File.Exists(opts.ConfigurationPath))
        {
            ExitWithError(ExitCode.FileNotFound, $"Configuration file did not exist at {opts.ConfigurationPath}.");
        }
    }

    private static void HandleParseError(IEnumerable<Error> errs)
    {
        ExitWithError(ExitCode.BadArguments, "Error occurred while parsing command line arguments.");
    }

    public static void ExitWithError(ExitCode exitCode, string message)
    {
        Console.WriteLine(message);
        Environment.Exit((int)exitCode);
    }

}
