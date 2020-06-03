Param(
  [Parameter(Mandatory=$True)]
  [string]$configuration,
  [switch]$IncludeDownloadSite
)

$rootDirectory = Resolve-Path "$(Split-Path -Parent $PSCommandPath)\.."
& "$rootDirectory\Build\generateBuildProperties.ps1" -outputPath "$rootDirectory\Build\BuildArtifacts\_buildProperties"
$artifactBuilderCsproj = "$rootDirectory\Build\ArtifactBuilder\ArtifactBuilder.csproj"

$packagesToBuild = @(
    "dotnet run --project '$artifactBuilderCsproj' AzureSiteExtension",
    "dotnet run --project '$artifactBuilderCsproj' NugetAzureWebSites $configuration x64",
    "dotnet run --project '$artifactBuilderCsproj' NugetAzureWebSites $configuration x86",
    "dotnet run --project '$artifactBuilderCsproj' NugetAgentApi $configuration",
    "dotnet run --project '$artifactBuilderCsproj' NugetAgentExtensions $configuration",
    "dotnet run --project '$artifactBuilderCsproj' NugetAzureCloudServices $configuration",
    "dotnet run --project '$artifactBuilderCsproj' NugetAgent $configuration",
    "dotnet run --project '$artifactBuilderCsproj' NugetAwsLambdaOpenTracer $configuration",
    "dotnet run --project '$artifactBuilderCsproj' ZipArchives $configuration",
    "dotnet run --project '$artifactBuilderCsproj' CoreInstaller $configuration",
    "dotnet run --project '$artifactBuilderCsproj' ScriptableInstaller $configuration",
    "dotnet run --project '$artifactBuilderCsproj' MsiInstaller $configuration",
    "dotnet run --project '$artifactBuilderCsproj' LinuxPackages $configuration"
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
    dotnet run --project "$artifactBuilderCsproj" DownloadSite $configuration
}
