############################################################
# Copyright 2020 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0
############################################################

Param(
  [Parameter(Mandatory=$True)]
  [string]$configuration,
  [switch]$IncludeDownloadSite
)

$rootDirectory = Resolve-Path "$(Split-Path -Parent $PSCommandPath)\.."
& "$rootDirectory\build\generateBuildProperties.ps1" -outputPath "$rootDirectory\build\BuildArtifacts\_buildProperties"
$artifactBuilderCsproj = "$rootDirectory\build\ArtifactBuilder\ArtifactBuilder.csproj"

$packagesToBuild = @(
    "dotnet run --project '$artifactBuilderCsproj' AzureSiteExtension",
    "dotnet run --project '$artifactBuilderCsproj' NugetAzureWebSites $configuration x64",
    "dotnet run --project '$artifactBuilderCsproj' NugetAzureWebSites $configuration x86",
    "dotnet run --project '$artifactBuilderCsproj' NugetAgentApi $configuration",
    "dotnet run --project '$artifactBuilderCsproj' NugetAgentExtensions $configuration",
    "dotnet run --project '$artifactBuilderCsproj' NugetAzureCloudServices $configuration",
    "dotnet run --project '$artifactBuilderCsproj' NugetAgent $configuration",
    "dotnet run --project '$artifactBuilderCsproj' ZipArchives $configuration",
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
