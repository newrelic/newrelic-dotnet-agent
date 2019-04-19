Param(
  [Parameter(Mandatory=$True)]
  [string]$configuration,
  [switch]$IncludeDownloadSite
)

$packagesToBuild = @(
    "dotnet run --project ArtifactBuilder\ArtifactBuilder.csproj NugetAzureWebSites $configuration x64",
    "dotnet run --project ArtifactBuilder\ArtifactBuilder.csproj NugetAzureWebSites $configuration x86",
    "dotnet run --project ArtifactBuilder\ArtifactBuilder.csproj NugetAgentApi $configuration",
    "dotnet run --project ArtifactBuilder\ArtifactBuilder.csproj NugetAzureCloudServices $configuration",
    "dotnet run --project ArtifactBuilder\ArtifactBuilder.csproj ZipArchives $configuration",
    "dotnet run --project ArtifactBuilder\ArtifactBuilder.csproj ScriptableInstaller $configuration",
    "dotnet run --project ArtifactBuilder\ArtifactBuilder.csproj MsiInstaller $configuration"
)

foreach ($pkg in $packagesToBuild) {
    Invoke-Expression $pkg
    if ($LastExitCode -ne 0) {
        exit $LastExitCode
    }
}

if ($IncludeDownloadSite) {
    #The download site should be built after the other artifacts are built, because it depends on the other artifacts
    dotnet run --project ArtifactBuilder\ArtifactBuilder.csproj DownloadSite $configuration
}
