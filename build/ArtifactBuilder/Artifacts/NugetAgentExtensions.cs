using System;
using System.IO;
using System.Linq;

namespace ArtifactBuilder.Artifacts
{
    public class NugetAgentExtensions : Artifact
    {
        public NugetAgentExtensions(string configuration)
            : base(nameof(NugetAgentExtensions))
        {
            Configuration = configuration;
            NugetPackagePath = $@"{RepoRootDirectory}\src\Agent\NewRelic\Agent\Extensions\NewRelic.Agent.Extensions\bin\{configuration}";
        }

        public string Configuration { get; }
        private string NugetPackagePath { get; }

        protected override void InternalBuild()
        {
            var fileNameSearchPattern = $"NewRelic.Agent.Extensions*.nupkg";
            var packagePath = Directory.GetFiles(NugetPackagePath, fileNameSearchPattern).FirstOrDefault();

            if (string.IsNullOrEmpty(packagePath))
            {
                Console.WriteLine("Warning: The {0} package could not be found.", fileNameSearchPattern);
                return;
            }

            Directory.CreateDirectory(OutputDirectory);

            var fileInfo = new FileInfo(packagePath);
            File.Copy(fileInfo.FullName, $@"{OutputDirectory}\{fileInfo.Name}", true);
            Console.WriteLine($"Successfully created package {nameof(NugetAgentExtensions)}");
        }

        protected override string Unpack()
        {
            throw new NotImplementedException();
        }

        protected override void ValidateContent()
        {
            throw new NotImplementedException();
        }
    }
}
