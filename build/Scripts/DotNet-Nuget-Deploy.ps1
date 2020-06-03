$ErrorActionPreference = "Stop"

function DeployNuGetPackage($packageName, $nugetPackageSource)
{
    $nupkgPath = "$packageName.nupkg"

    # Push the package to NuGet
    . C:\nuget.exe setApiKey $env:NuGetAPIKey
    $deploySuccessful = $false
    $retryCount = 0
    while($deploySuccessful -eq $false -and $retryCount -lt 3)
    {
        . C:\nuget.exe push $nupkgPath $nugetPackageSource
        if ($LASTEXITCODE -eq 0)
        {
            Write-Host "-- Deploy successful for '$packageName'."
            $deploySuccessful = $true
        }
        else
        {
            Write-Host "-- Encountered an error attempting to deploy NuGet package '$packageName'."
            $retryCount++
            Write-Host "Retry count now '$retryCount'."
        }
    }

    if (!$deploySuccessful)
    {
        exit 1
    }	
}
