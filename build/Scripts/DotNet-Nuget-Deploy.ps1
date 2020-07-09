$ErrorActionPreference = "Stop"
. $env:WORKSPACE\Build\Scripts\chatRoomAPI.ps1

function DeployNuGetPackage($packageName, $nugetPackageSource)
{
    $nuspecPath = "$packageName.nuspec"
    $nupkgPath = "$packageName.nupkg"

    # Annotate the build
    Set-Location $env:WORKSPACE
    [Xml]$xml = Get-Content $nuspecPath
    $version = $xml.package.metadata.version
    #Invoke-RestMethod -Uri "$($env:BUILD_URL)submitDescription?description=$version" -MaximumRedirection 0 -ErrorVariable invokeErr -ErrorAction SilentlyContinue
    #if($invokeErr[0].FullyQualifiedErrorId.Contains("MaximumRedirectExceeded")){
    #    $null
    #}

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
	
	ChatAboutNuGetPackage $packageName
}

# Update the Chat room
function ChatAboutNuGetPackage($packageName)
{
    $link = "http://www.nuget.org/packages/$packageName/"
    $chatMessage = "NuGet package '$packageName' has been updated with agent version $version.  See $link for more information."
    PostMessageToChatRoom "dotnet-agent" $chatMessage "html" 1 "green"
}