params (
    [string] $RootDirectory,
    [string] $DeployingPackage
)

$ErrorActionPreference = "Stop"
. $env:WORKSPACE\agent-build-scripts\chatRoomAPI.ps1

function Deploy($packagePath, $source, $apiKey)
{
	if(-not ([string]::IsNullOrEmpty($apiKey) ))
	{
		C:\nuget.exe setApiKey $apiKey -Source $source
	}
	
	$deploySuccessful = $false
	$retryCount = 0
	while($deploySuccessful -eq $false -and $retryCount -lt 3)
	{
		. C:\nuget.exe push $packagePath -Source $source
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


# Update the Chat room
function ChatAboutNuGetPackage($packageName)
{
    $link = "http://www.nuget.org/packages/$packageName/"
    $chatMessage = "NuGet package '$packageName' has been updated with agent version $version.  See $link for more information."
    PostMessageToChatRoom "dotnet-agent" $chatMessage "html" 1 "green"
}



if($DeployingPackage -eq "NewRelic.Azure.WebSites.x64")
{
	$packageName = Get-ChildItem $RootDirectory\Build\BuildArtifacts\NugetAzureWebSites-x64\NewRelic.Azure.WebSites.*.nupkg -Name
	$packagePath = Convert-Path $RootDirectory\Build\BuildArtifacts\NugetAzureWebSites-x64\$packageName
	$version = $packageName.TrimStart('NewRelic.Azure.WebSites.x').TrimStart('{64,86}').TrimStart('.').TrimEnd('.nupkg')

}
elseif(DeployingPackage -eq "NewRelic.Azure.WebSites")
{
	$packageName = Get-ChildItem $RootDirectory\Build\BuildArtifacts\NugetAzureWebSites-x86\NewRelic.Azure.WebSites.*.nupkg -Name
	$packagePath = Convert-Path $RootDirectory\Build\BuildArtifacts\NugetAzureWebSites-x86\$packageName
	$version = $packageName.TrimStart('NewRelic.Azure.WebSites.x').TrimStart('{64,86}').TrimStart('.').TrimEnd('.nupkg')
}
else
{
	Write-Host "Input value for DeployingPackage parameter not recognized."
	Exit 1
}

Write-Host "Package name: " $packageName

Write-Host "Package path: " $packagePath

Write-Host "Version: "$version

$source = 'https://www.nuget.org'

$apiKey = $env:NuGetAPIKey

Deploy $packagePath $source $apiKey

ChatAboutNuGetPackage $packageName.TrimEnd('.nupkg')



