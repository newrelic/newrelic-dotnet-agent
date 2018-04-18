$ErrorActionPreference = "Stop"

###############
# NuGet Restore #
###############

$applicationsFull = @("Agent\FullAgent.sln", "FunctionalTests\FunctionalTests.sln", "IntegrationTests\IntegrationTests.sln", "IntegrationTests\UnboundedIntegrationTests.sln", "Tests\PlatformTests\PlatformTests.sln")

Write-Host "Restoring NuGet packages"
foreach ($application in $applicationsFull) {
    C:\nuget.exe restore $application -NoCache -Source "http://win-nuget-repository.pdx.vm.datanerd.us:81/NuGet/Default"
}

#######
# Build #
#######

$msBuildPath = "C:\Program Files (x86)\Microsoft Visual Studio\2017\BuildTools\MSBuild\15.0\Bin\MSBuild.exe"

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

##########################
# Create Build Artifacts #
##########################

pushd "Build"
Invoke-Expression "& .\package.ps1 -configuration Release"
if ($LastExitCode -ne 0) {
    exit $LastExitCode
}
popd

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

###############
# Annotate the build and the parent CI job #
###############

$Commit = $env:GIT_COMMIT.Substring(0, 10)
$authorization = 'Basic ' + [System.Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes("msneeden:$env:JenkinsAPIToken"))
Invoke-RestMethod -Uri "$($env:BUILD_URL)submitDescription?description=$agentVersion - $env:GIT_BRANCH - $Commit" -Headers @{'Authorization' = $authorization} -Method POST -MaximumRedirection 0 -ErrorVariable invokeErr -ErrorAction SilentlyContinue
if($invokeErr[0].FullyQualifiedErrorId.Contains("MaximumRedirectExceeded")){
    $null
}

if (!$env:sha1 -and $env:BUILD_CAUSE_UPSTREAMTRIGGER) {
    Write-Host "Updating description in UPSTREAM Job - URI: $env:UPSTREAM_BUILD_URL"
    Write-Host "AUTH: $authorization"
    Write-Host "URI:    $($env:UPSTREAM_BUILD_URL)submitDescription?description=$agentVersion - $env:GIT_BRANCH - $Commit"
    Invoke-RestMethod -Uri "$($env:UPSTREAM_BUILD_URL)submitDescription?description=$agentVersion - $env:GIT_BRANCH - $Commit" -Headers @{'Authorization' = $authorization}  -Method POST -MaximumRedirection 0 -ErrorVariable invokeErr -ErrorAction SilentlyContinue
    if($invokeErr[0].FullyQualifiedErrorId.Contains("MaximumRedirectExceeded")){
        $null
    }
}

if ($LastExitCode -ne 0) {
    exit $LastExitCode
}

###############
# Clean up old containers #
###############

Write-Host "Cleaning up old containers"
Write-Host 'Running command: docker container prune --force --filter "until=60m"'
docker container prune --force --filter "until=60m"

exit $LastExitCode
