Param(
    [ValidateSet("Debug","Release")][string]$Configuration = "Release",
    [string]$gpgKeyPath = ""
)

$ErrorActionPreference = "Stop"

$rootDirectory = Resolve-Path "$(Split-Path -Parent $PSCommandPath)\.."
$nugetPath = (Resolve-Path "$rootDirectory\Build\Tools\nuget.exe").Path
$vsWhere = (Resolve-Path "$rootDirectory\Build\Tools\vswhere.exe").Path
$msBuildPath = & "$vsWhere" -products 'Microsoft.VisualStudio.Product.BuildTools' -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | select-object -first 1
if (!$msBuildPath) {
    $msBuildPath = & "$vsWhere" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | select-object -first 1
}

##################
# Profiler Build #
##################

$profilerBuildScript = "$rootDirectory\src\Agent\NewRelic\Profiler\build\build.ps1"
& $profilerBuildScript
if ($LastExitCode -ne 0) {
    Write-Host "Error in Profiler build script. Exiting with code: $LastExitCode.."
    exit $LastExitCode
}

#######################
# Managed Agent Build #
#######################

$solutions = [Ordered]@{
    "$rootDirectory\FullAgent.sln"                                              = @("Configuration=$Configuration;AllowUnsafeBlocks=true");
    "$rootDirectory\src\Agent\MsiInstaller\MsiInstaller.sln"                    = @("Configuration=$Configuration;Platform=x86;AllowUnsafeBlocks=true","Configuration=$Configuration;Platform=x64;AllowUnsafeBlocks=true");
    "$rootDirectory\tests\Agent\IntegrationTests\IntegrationTests.sln"          = @("Configuration=$Configuration;DeployOnBuild=true;PublishProfile=LocalDeploy");
    "$rootDirectory\tests\Agent\IntegrationTests\UnboundedIntegrationTests.sln" = @("Configuration=$Configuration;DeployOnBuild=true;PublishProfile=LocalDeploy");
    "$rootDirectory\tests\Agent\PlatformTests\PlatformTests.sln"                = @("Configuration=$Configuration");
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

Write-Host "Building solutions"
foreach ($sln in $solutions.Keys) {
    foreach ($config in $solutions.Item($sln)) {
        Write-Host "-- Building $sln : '. $msBuildPath /m /p:$($config) $sln'"
        . $msBuildPath /m /p:$($config) $sln
        Write-Host "MSBuild Exit code: $LastExitCode"
        if ($LastExitCode -ne 0) {
            Write-Host "Error building solution $sln. Exiting with code: $LastExitCode.."
            exit $LastExitCode
        }
    }
}

$agentVersion = (Get-Item "$rootDirectory\src\_build\AnyCPU-$Configuration\NewRelic.Agent.Core\net45\NewRelic.Agent.Core.dll").VersionInfo.FileVersion

###############
# Linux build #
###############

if (!($gpgKeyPath -eq "")) {
    Write-Host "===================================="
    Write-Host "Executing Linux builds in Docker for Agent Version: $agentVersion"
    Push-Location "$rootDirectory\build\Linux"
    docker-compose build
    docker-compose run -e AGENT_VERSION=$agentVersion build_deb
    New-Item keys -Type Directory -Force
    Copy-Item $gpgKeyPath .\keys\gpg.tar.bz2 -Force
    docker-compose run -e AGENT_VERSION=$agentVersion -e GPG_KEYS=/keys/gpg.tar.bz2 build_rpm
    Pop-Location
    Write-Host "===================================="

    if ($LastExitCode -ne 0) {
        Write-Host "Error building Linux agent. Exiting with code: $LastExitCode."
        exit $LastExitCode
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

###############
# Clean up old containers #
###############

Write-Host "Cleaning up old containers"
Write-Host 'Running command: docker container prune --force --filter "until=60m"'
docker container prune --force --filter "until=60m"

exit $LastExitCode
