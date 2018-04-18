Param(
  [Parameter(Mandatory=$True)]
  [string]$configuration
)

$packagesToBuild = @(
    "dotnet run --project ArtifactBuilder\ArtifactBuilder.csproj AzureSiteExtension 1.2.0",
    "dotnet run --project ArtifactBuilder\ArtifactBuilder.csproj NugetAzureWebSites $configuration x64 -pushNugetPackage",
    "dotnet run --project ArtifactBuilder\ArtifactBuilder.csproj NugetAzureWebSites $configuration x86 -pushNugetPackage",
    "dotnet run --project ArtifactBuilder\ArtifactBuilder.csproj NugetAgentApi $configuration",
    "dotnet run --project ArtifactBuilder\ArtifactBuilder.csproj NugetAzureCloudServices $configuration",
    "dotnet run --project ArtifactBuilder\ArtifactBuilder.csproj NugetAzureServiceFabric $configuration",
    "dotnet run --project ArtifactBuilder\ArtifactBuilder.csproj ZipArchives $configuration",
    "dotnet run --project ArtifactBuilder\ArtifactBuilder.csproj CoreInstaller $configuration",
    "dotnet run --project ArtifactBuilder\ArtifactBuilder.csproj ScriptableInstaller $configuration"
)

foreach ($pkg in $packagesToBuild) {
    Invoke-Expression $pkg
    if ($LastExitCode -ne 0) {
        exit $LastExitCode
    }
}
