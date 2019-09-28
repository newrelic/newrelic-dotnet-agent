$ErrorActionPreference = "Stop"

$nugetPath = (Resolve-Path ".\Build\Tools\nuget.exe").Path
$msBuildPath = "C:\Program Files (x86)\Microsoft Visual Studio\2017\BuildTools\MSBuild\15.0\Bin\MSBuild.exe"
$msBuildPathx64 = "C:\Program Files (x86)\Microsoft Visual Studio\2017\BuildTools\MSBuild\15.0\Bin\amd64\MSBuild.exe"

function ExitIfFailLastExitCode {
    if ($LastExitCode -ne 0) {
        exit $LastExitCode
    }
}

##################
# Profiler Build #
##################

$profilerBuildScript = "Agent\NewRelic\Profiler\build\build.ps1"
& $profilerBuildScript

#######################
# Managed Agent Build #
#######################

$solutions = [Ordered]@{
    "Agent\FullAgent.sln"                               = @("Configuration=Release;AllowUnsafeBlocks=true");
    "Agent\MsiInstaller.sln"                            = @("Configuration=Release;Platform=x86;AllowUnsafeBlocks=true","Configuration=Release;Platform=x64;AllowUnsafeBlocks=true");
    "IntegrationTests\IntegrationTests.sln"             = @("Configuration=Release;DeployOnBuild=true;PublishProfile=LocalDeploy");
    "IntegrationTests\UnboundedIntegrationTests.sln"    = @("Configuration=Release;DeployOnBuild=true;PublishProfile=LocalDeploy");
    "Tests\Agent\PlatformTests\PlatformTests.sln"       = @("Configuration=Release");
}

###############
# NuGet Restore #
###############

Write-Host "Restoring NuGet packages"
foreach ($sln in $solutions.Keys) {
    & $nugetPath restore $sln -NoCache -Source "https://www.nuget.org/api/v2"
}

#######
# Build #
#######

Write-Host "Building solutions"
foreach ($sln in $solutions.Keys) {
    foreach ($config in $solutions.Item($sln)) {
        Write-Host "-- Building $sln : '. $msBuildPath /m /p:$($config) $sln'"
        . $msBuildPath /m /p:$($config) $sln
        if ($LastExitCode -ne 0) {
            exit $LastExitCode
        }
    }
}

$agentVersion = [Reflection.AssemblyName]::GetAssemblyName("$env:WORKSPACE\Agent\_build\AnyCPU-Release\NewRelic.Agent.Core\net45\NewRelic.Agent.Core.dll").Version.ToString()

###############
# Linux build #
###############

Write-Host "===================================="
Write-Host "Executing Linux builds in Docker for Agent Version: $agentVersion"
Push-Location .\Build\Linux
docker-compose build
docker-compose run -e AGENT_VERSION=$agentVersion build_deb
New-Item keys -Type Directory
Copy-Item $env:GPG_KEYS .\keys\gpg.tar.bz2
docker-compose run -e AGENT_VERSION=$agentVersion -e GPG_KEYS=/keys/gpg.tar.bz2 build_rpm
Pop-Location
Write-Host "===================================="

if ($LastExitCode -ne 0) {
    exit $LastExitCode
}

##########################
# Create Build Artifacts #
##########################

Push-Location "Build"
Invoke-Expression "& .\package.ps1 -configuration Release -IncludeDownloadSite"
if ($LastExitCode -ne 0) {
    exit $LastExitCode
}
Pop-Location

###############
# Clean up old containers #
###############

Write-Host "Cleaning up old containers"
Write-Host 'Running command: docker container prune --force --filter "until=60m"'
docker container prune --force --filter "until=60m"

exit $LastExitCode
