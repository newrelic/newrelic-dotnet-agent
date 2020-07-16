$ErrorActionPreference = "Stop"

###############
# NuGet Restore #
###############

$nugetPath = (Resolve-Path ".\build\Tools\nuget.exe").Path
#$solutions = @("Agent\FullAgent.sln", "IntegrationTests\IntegrationTests.sln", "IntegrationTests\UnboundedIntegrationTests.sln")
$solutions = @("src\Agent\FullAgent.sln", "src\Agent\MsiInstaller\MsiInstaller.sln")

Write-Host "Restoring NuGet packages"
foreach ($sln in $solutions) {
    & $nugetPath restore $sln -NoCache -Source "https://www.nuget.org/api/v2"
}

#######
# Build #
#######

$msBuildPath = "C:\Program Files (x86)\Microsoft Visual Studio\2017\BuildTools\MSBuild\15.0\Bin\MSBuild.exe"

$solutions = [Ordered]@{
    "src\Agent\FullAgent.sln"                                    = @("Configuration=Release;AllowUnsafeBlocks=true");
    "src\Agent\MsiInstaller\MsiInstaller.sln"                    = @("Configuration=Release;Platform=x86;AllowUnsafeBlocks=true","Configuration=Release;Platform=x64;AllowUnsafeBlocks=true");
    # "IntegrationTests\IntegrationTests.sln"                      = @("Configuration=Release;DeployOnBuild=true;PublishProfile=LocalDeploy");
    # "IntegrationTests\UnboundedIntegrationTests.sln"             = @("Configuration=Release;DeployOnBuild=true;PublishProfile=LocalDeploy");
}

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

Push-Location "build"
Invoke-Expression "& .\package.ps1 -configuration Release -IncludeDownloadSite"
if ($LastExitCode -ne 0) {
   exit $LastExitCode
}
Pop-Location


exit $LastExitCode
