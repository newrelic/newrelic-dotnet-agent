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

$profilerSolutionPath = "Agent\NewRelic\Profiler\NewRelic.Profiler.sln"

Write-Host "-- Profiler build: Restoring NuGet packages"
& $nugetPath restore $profilerSolutionPath -Source "https://www.nuget.org/api/v2"
ExitIfFailLastExitCode

Write-Host "-- Profiler build: Building x64 profiler"
& $msBuildPathx64 /p:Platform=x64 /p:Configuration=Release $profilerSolutionPath
ExitIfFailLastExitCode

Write-Host "-- Profiler build: Building Win32 profiler"
& "$msBuildPath" /p:Platform=Win32 /p:Configuration=Release $profilerSolutionPath
ExitIfFailLastExitCode

Write-Host "-- Profiler build: Building Linux profiler"
& .\Agent\NewRelic\Profiler\build\scripts\build_linux.ps1
ExitIfFailLastExitCode

Remove-Item -Recurse -Force Agent\ProfilerBuildsForDevMachines

###############
# NuGet Restore #
###############

$applicationsFull = @("Agent\FullAgent.sln", "FunctionalTests\FunctionalTests.sln", "IntegrationTests\IntegrationTests.sln", "IntegrationTests\UnboundedIntegrationTests.sln", "Tests\PlatformTests\PlatformTests.sln")

Write-Host "Restoring NuGet packages"
foreach ($application in $applicationsFull) {
    & $nugetPath restore $application -NoCache -Source "https://www.nuget.org/api/v2"
}

#######
# Build #
#######

$applicationsFull = [Ordered]@{"Agent\FullAgent.sln" = "Configuration=Release;Platform=x86;AllowUnsafeBlocks=true";
    "FunctionalTests\FunctionalTests.sln"            = "Configuration=Release";
    "IntegrationTests\IntegrationTests.sln"          = "Configuration=Release;DeployOnBuild=true;PublishProfile=LocalDeploy";
    "IntegrationTests\UnboundedIntegrationTests.sln" = "Configuration=Release;DeployOnBuild=true;PublishProfile=LocalDeploy";
	"Tests\PlatformTests\PlatformTests.sln"          = "Configuration=Release";
}

Write-Host "Building for full build"
foreach ($applicationFull in $applicationsFull.Keys) {
    Write-Host "-- Building $applicationFull"
    Write-Host "-- Executing '. $msBuildPath /m /p:$($applicationsFull.Item($applicationFull)) $applicationFull'"
    . $msBuildPath /m /p:$($applicationsFull.Item($applicationFull)) $applicationFull

    if ($LastExitCode -ne 0) {
        exit $LastExitCode
    }

    if ($applicationFull -eq "Agent\FullAgent.sln") {
        Write-Host "-- Executing '. $msBuildPath /m /p:$($applicationsFull.Item($applicationFull).Replace("x86", "x64")) $applicationFull'"
        . $msBuildPath /m /p:$($applicationsFull.Item($applicationFull).Replace("x86", "x64")) $applicationFull
        
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
Set-Location .\Agent
docker-compose build
docker-compose run -e AGENT_VERSION=$agentVersion build_deb
copy $env:GPG_KEYS .\gpg.tar.bz2
docker-compose run -e AGENT_VERSION=$agentVersion -e GPG_KEYS=/data/gpg.tar.bz2 build_rpm
Set-Location ..
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
