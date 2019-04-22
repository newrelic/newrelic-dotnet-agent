Param(
  [Parameter(Mandatory=$True)]
  [string]$configuration,
  [switch]$IncludeDownloadSite
)

$packagesToBuild = @(
    "dotnet run --project ArtifactBuilder\ArtifactBuilder.csproj AzureSiteExtension 1.2.0",
    "dotnet run --project ArtifactBuilder\ArtifactBuilder.csproj NugetAzureWebSites $configuration x64",
    "dotnet run --project ArtifactBuilder\ArtifactBuilder.csproj NugetAzureWebSites $configuration x86",
    "dotnet run --project ArtifactBuilder\ArtifactBuilder.csproj NugetAgentApi $configuration",
    "dotnet run --project ArtifactBuilder\ArtifactBuilder.csproj NugetAgentExtensions $configuration",
    "dotnet run --project ArtifactBuilder\ArtifactBuilder.csproj NugetAzureCloudServices $configuration",
    "dotnet run --project ArtifactBuilder\ArtifactBuilder.csproj NugetAgent $configuration",
    "dotnet run --project ArtifactBuilder\ArtifactBuilder.csproj ZipArchives $configuration",
    "dotnet run --project ArtifactBuilder\ArtifactBuilder.csproj CoreInstaller $configuration",
    "dotnet run --project ArtifactBuilder\ArtifactBuilder.csproj ScriptableInstaller $configuration",
    "dotnet run --project ArtifactBuilder\ArtifactBuilder.csproj MsiInstaller $configuration",
    "dotnet run --project ArtifactBuilder\ArtifactBuilder.csproj LinuxPackages $configuration"
)

foreach ($pkg in $packagesToBuild) {
    Write-Output "EXECUTING: $pkg"
    Invoke-Expression $pkg
    if ($LastExitCode -ne 0) {
        exit $LastExitCode
    }
    Write-Output "----------------------"
}

if ($IncludeDownloadSite) {
    #The download site should be built after the other artifacts are built, because it depends on the other artifacts
    dotnet run --project ArtifactBuilder\ArtifactBuilder.csproj DownloadSite $configuration
}
