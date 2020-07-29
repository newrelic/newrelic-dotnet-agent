using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ILRepacking;

namespace NewRelic.Build.RepackAssemblies
{
	public class Program
	{
		public static string SolutionPath { get; set; }
		public static string Configuration { get; set; }
		private static string ExtensionsDirectoryPath => Path.Combine(SolutionPath, "src", "Agent", "NewRelic", "Agent", "Extensions");

        public static void Main(string[] args)
        {
            var userArgs = ConvertArgsToDictionary(args);

            SolutionPath = userArgs["solution"];
            Configuration = userArgs["configuration"];
            var repackTarget = userArgs["repackTarget"];

            switch (repackTarget)
            {
                case "AsyncLocal":
                    RepackAsyncLocal();
                    break;
                default:
                    throw new ArgumentException($"No matching repack targets setup in RepackAssemblies.Program for value: {repackTarget}");
            }
        }

        private static void RepackAsyncLocal()
        {
            var asyncLocalDirectory = Path.Combine(ExtensionsDirectoryPath, "Providers", "Storage", "CallStack.AsyncLocal");
            var binConfigurationPath = Path.Combine(asyncLocalDirectory, "bin", Configuration);
            var frameworkBuildDirectories = Directory.EnumerateDirectories(binConfigurationPath, "*net*", SearchOption.AllDirectories);

            foreach (var directory in frameworkBuildDirectories)
            {
                var asyncLocalDllFilePath = Path.Combine(directory, "NewRelic.Providers.CallStack.AsyncLocal.dll");
                var keyFilePath = Path.Combine(SolutionPath, "build", "keys", "KeyFile.snk");

                var assemblyPathsToRepack = new List<string>
                {
                    asyncLocalDllFilePath,
                    Path.Combine(directory, "System.Collections.Immutable.dll")
                };

                var ilRepackOptions = new RepackOptions(Enumerable.Empty<string>())
                {
                    Parallel = true,
                    Internalize = true,
                    InputAssemblies = assemblyPathsToRepack.ToArray(),
                    TargetKind = ILRepack.Kind.SameAsPrimaryAssembly,
                    OutputFile = asyncLocalDllFilePath,
                    SearchDirectories = new[] { directory },
                    KeyFile = keyFilePath
                };

                var ilRepack = new ILRepack(ilRepackOptions);
                ilRepack.Repack();
            }
        }

        private static Dictionary<string, string> ConvertArgsToDictionary(string[] args)
        {
            var passArgs = new Dictionary<string, string>();
            foreach (var arg in args)
            {
                var pieces = arg.Split('=');
                if (pieces.Length == 2)
                {
                    var key = pieces[0].TrimStart('-');
                    var value = pieces[1];

                    passArgs[key] = value;
                }
            }

            return passArgs;
        }
    }
}
