// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using CommandLine;

namespace S3Validator
{
    internal class Program
    {
        private const string VersionToken = @"{version}";

        private static readonly HttpClient _client = new();

        static void Main(string[] args)
        {
            var result = Parser.Default.ParseArguments<Options>(args)
                .WithParsed(ValidateOptions)
                .WithNotParsed(HandleParseError);

            var version = result.Value.Version;
            var configuration = LoadConfiguration(result.Value.ConfigurationPath);
            Validate(version, configuration);
            Console.WriteLine("Valid.");
        }

        private static void Validate(string version, Configuration configuration)
        {
            var tasks = new List<Task<HttpResponseMessage>>();
            foreach (var dir in configuration.DirectoryList!)
            {
                foreach (var fileDetail in configuration.FileList!)
                {
                    var url = $"{configuration.BaseUrl}/{dir.Replace(VersionToken, version)}/{fileDetail.Name.Replace(VersionToken, version)}";
                    var request = new HttpRequestMessage(HttpMethod.Head, url);
                    request.Options.TryAdd("expectedSize", fileDetail.Size);
                    tasks.Add(_client.SendAsync(request));
                }
            }

            if (!tasks.Any())
            {
                ExitWithError(ExitCode.Error, "There was nothing to validate.");
            }

            var taskCompleted = Task.WaitAll(tasks.ToArray(), 10000);
            if (!taskCompleted)
            {
                ExitWithError(ExitCode.Error, "Validation timed out waiting for HttpClient requests to complete.");
            }

            var isValid = true;
            var results = new List<string>();
            foreach (var task in tasks)
            {
                var status = "Valid";

                if (!task.Result.IsSuccessStatusCode)
                {
                    isValid = false;
                    status = task.Result.StatusCode.ToString();
                }
                else if (task.Result.Content.Headers.ContentLength < task.Result.RequestMessage?.Options.GetValue<long>("expectedSize"))
                {
                    isValid = false;
                    status = "FileSize";
                }

                results.Add($"{status,-12}{task.Result.RequestMessage?.RequestUri}");
            }

            if (!isValid)
            {
                ExitWithError(ExitCode.Error, "Validation failed. Results:" + Environment.NewLine + string.Join(Environment.NewLine, results));
            }
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

        private static Configuration LoadConfiguration(string path)
        {
            var input = File.ReadAllText(path);
            var deserializer = new YamlDotNet.Serialization.Deserializer();
            return deserializer.Deserialize<Configuration>(input);
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

    public static class Helpers
    {

        // Simplfy the TryGetValue into something more usable.
        public static T? GetValue<T>(this HttpRequestOptions options, string key)
        {
            options.TryGetValue(new HttpRequestOptionsKey<T>(key), out var value);
            return value;
        }
    }

}
