############################################################
# Copyright 2020 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0
############################################################

Param(
    [ValidateSet("Debug","Release")][string]$Configuration = "Release",
    [ValidateSet("All", "Windows", "Linux","Framework", "CoreAll","CoreWindows","CoreLinux")][string]$Type = "All",
    [ValidateSet("All","x64","x86")][string]$Architecture = "All",
    [string]$HomePath = "$env:NR_DEV_HOMEROOT",
    [switch]$KeepNewRelicConfig = $false,
    [switch]$SetSystemEnvironment = $false,
    [switch]$SetSessionEnvironment = $false
)

$ErrorActionPreference = "Stop"


##############################
# Validation  and param setup#
##############################

Write-Host "Performing validation"
$rootDirectory = Resolve-Path "$(Split-Path -Parent $PSCommandPath)\.."
$nugetPath = (Resolve-Path "$rootDirectory\build\Tools\nuget.exe").Path
$vsWhere = (Resolve-Path "$rootDirectory\build\Tools\vswhere.exe").Path
$msBuildPath = & "$vsWhere" -products 'Microsoft.VisualStudio.Product.BuildTools' -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | select-object -first 1
if (!$msBuildPath) {
    $msBuildPath = & "$vsWhere" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | select-object -first 1
}

. "$rootDirectory\build\build_functions.ps1"
$HomePath = Get-HomeRootPath $HomePath

#######################
# Managed Agent Build #
#######################

$solutions = [Ordered]@{
    "$rootDirectory\FullAgent.sln"                                              = @("Configuration=$Configuration;AllowUnsafeBlocks=true");
    "$rootDirectory\src\Agent\MsiInstaller\MsiInstaller.sln"                    = @("Configuration=$Configuration;Platform=x86;AllowUnsafeBlocks=true","Configuration=$Configuration;Platform=x64;AllowUnsafeBlocks=true");
    "$rootDirectory\tests\Agent\IntegrationTests\IntegrationTests.sln"          = @("Configuration=$Configuration;DeployOnBuild=true;PublishProfile=LocalDeploy");
    "$rootDirectory\tests\Agent\IntegrationTests\UnboundedIntegrationTests.sln" = @("Configuration=$Configuration;DeployOnBuild=true;PublishProfile=LocalDeploy");
}

#################
# NuGet Restore #
#################

Write-Host "Restoring NuGet packages"
foreach ($sln in $solutions.Keys) {
    & $nugetPath restore $sln -NoCache -Source "https://www.nuget.org/api/v2"
    if ($LastExitCode -ne 0) {
        Write-Host "Error during NuGet restore. Exiting with code: $LastExitCode.."
        exit $LastExitCode
    }
}

#########
# Build #
#########

$env:NR_DEV_BUILD_HOME = "true"
$env:KeepNewRelicConfig = $KeepNewRelicConfig
$env:SetSystemEnvironment = $SetSystemEnvironment
$env:SetSessionEnvironment = $SetSessionEnvironment
Write-Host "Building solutions"
foreach ($sln in $solutions.Keys) {
    foreach ($config in $solutions.Item($sln)) {
        Write-Host "-- Building $sln : '. $msBuildPath -m -p:$($config) $sln'"
        . $msBuildPath -nologo -m -p:$($config) $sln
        Write-Host "MSBuild Exit code: $LastExitCode"
        if ($LastExitCode -ne 0) {
            Write-Host "Error building solution $sln. Exiting with code: $LastExitCode.."
            exit $LastExitCode
        }
    }
}

##########################
# Create Build Artifacts #
##########################

& "$rootDirectory\build\package.ps1" -configuration $Configuration -IncludeDownloadSite
if ($LastExitCode -ne 0) {
    Write-Host "Error building packages. Exiting with code: $LastExitCode.."
    exit $LastExitCode
}

Write-Host "Completed build.ps1"
exit $LastExitCode
