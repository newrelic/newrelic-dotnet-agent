$ErrorActionPreference = "Stop"

###############
# NuGet Restore #
###############

$nugetPath = (Resolve-Path ".\build\Tools\nuget.exe").Path
#$applicationsFull = @("Agent\FullAgent.sln", "IntegrationTests\IntegrationTests.sln", "IntegrationTests\UnboundedIntegrationTests.sln")
$applicationsFull = @("src\Agent\FullAgent.sln")

Write-Host "Restoring NuGet packages"
foreach ($application in $applicationsFull) {
    & $nugetPath restore $application -NoCache -Source "https://www.nuget.org/api/v2"
}

#######
# Build #
#######

$msBuildPath = "C:\Program Files (x86)\Microsoft Visual Studio\2017\BuildTools\MSBuild\15.0\Bin\MSBuild.exe"

$applicationsFull = [Ordered]@{"src\Agent\FullAgent.sln" = "Configuration=Release;Platform=x86;AllowUnsafeBlocks=true";
#    "IntegrationTests\IntegrationTests.sln"          = "Configuration=Release;DeployOnBuild=true;PublishProfile=LocalDeploy";
#    "IntegrationTests\UnboundedIntegrationTests.sln" = "Configuration=Release;DeployOnBuild=true;PublishProfile=LocalDeploy"
}

Write-Host "Building for full build"
foreach ($applicationFull in $applicationsFull.Keys) {
    Write-Host "-- Building $applicationFull"
    Write-Host "-- Executing '. $msBuildPath /m /p:$($applicationsFull.Item($applicationFull)) $applicationFull'"
    . $msBuildPath /m /p:$($applicationsFull.Item($applicationFull)) $applicationFull

    if ($LastExitCode -ne 0) {
        exit $LastExitCode
    }

    if ($applicationFull -eq "src\Agent\FullAgent.sln") {
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

Push-Location "build"
Invoke-Expression "& .\package.ps1 -configuration Release -IncludeDownloadSite"
if ($LastExitCode -ne 0) {
   exit $LastExitCode
}
Pop-Location


exit $LastExitCode
