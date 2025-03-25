using NuGet;

namespace NewRelic.NuGetHelper
{
    public static class Utils
    {
        public static IPackage FindPackage(string packageId, string maxVersion, string repoUrl)
        {
            var repo = PackageRepositoryFactory.Default.CreateRepository(repoUrl);

            var versionSpec = new VersionSpec();
            if (!string.IsNullOrEmpty(maxVersion))
            {
                versionSpec.MaxVersion = SemanticVersion.Parse(maxVersion);
            }
            var package = repo.FindPackage(packageId, versionSpec, false, false);

            return package;
        }
    }
}
