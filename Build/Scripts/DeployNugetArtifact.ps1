Param(
	[string] $rootDirectory,
	[string] $deployingPackage
)

$ErrorActionPreference = "Stop"
. $rootDirectory\Build\Scripts\chatRoomAPI.ps1

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

$chatMessage = ""

if($deployingPackage -eq "NewRelic.Azure.WebSites.x64")
{
	$packageName = Get-ChildItem $rootDirectory\Build\BuildArtifacts\NugetAzureWebSites-x64\NewRelic.Azure.WebSites.*.nupkg -Name
	$packagePath = Convert-Path $rootDirectory\Build\BuildArtifacts\NugetAzureWebSites-x64\$packageName
	$version = $packageName.TrimStart('NewRelic.Azure.WebSites.x').TrimStart('{64,86}').TrimStart('.').TrimEnd('.nupkg')
	$chatMessage = "NuGet package '$packageName' has been updated with agent version $version."

}
elseif($deployingPackage -eq "NewRelic.Azure.WebSites")
{
	$packageName = Get-ChildItem $rootDirectory\Build\BuildArtifacts\NugetAzureWebSites-x86\NewRelic.Azure.WebSites.*.nupkg -Name
	$packagePath = Convert-Path $rootDirectory\Build\BuildArtifacts\NugetAzureWebSites-x86\$packageName
	$version = $packageName.TrimStart('NewRelic.Azure.WebSites.x').TrimStart('{64,86}').TrimStart('.').TrimEnd('.nupkg')
	$chatMessage = "NuGet package '$packageName' has been updated with agent version $version."

}
elseif($deployingPackage -eq "NewRelic.Agent")
{
	$packageName = Get-ChildItem $rootDirectory\Build\BuildArtifacts\NugetAgent\NewRelic.Agent.*.nupkg -Name
	$packagePath = Convert-Path $rootDirectory\Build\BuildArtifacts\NugetAgent\$packageName
	$version = $packageName.TrimStart('NewRelic.Agent').TrimStart('.').TrimEnd('.nupkg')
	$chatMessage = "NuGet package '$packageName' has been updated with agent version $version."
}
elseif($deployingPackage -eq "NewRelic.Agent.Api")
{
	$packageName = Get-ChildItem $rootDirectory\Build\BuildArtifacts\NugetAgentApi\NewRelic.Agent.Api.*.nupkg -Name
	$packagePath = Convert-Path $rootDirectory\Build\BuildArtifacts\NugetAgentApi\$packageName
	$version = $packageName.TrimStart('NewRelic.Agent.Api').TrimStart('.').TrimEnd('.nupkg')
	$chatMessage = "NuGet package '$packageName' has been updated with agent version $version."
}
elseif($deployingPackage -eq "NewRelicWindowsAzure")
{
	$packageName = Get-ChildItem $rootDirectory\Build\BuildArtifacts\NugetAzureCloudServices\NewRelicWindowsAzure.*.nupkg -Name
	$packagePath = Convert-Path $rootDirectory\Build\BuildArtifacts\NugetAzureCloudServices\$packageName
	$version = $packageName.TrimStart('NewRelicWindowsAzure').TrimStart('.').TrimEnd('.nupkg')
	$chatMessage = "NuGet package '$packageName' has been updated with agent version $version."
}
elseif($deployingPackage -eq "NewRelic.Azure.WebSites.Extension")
{
	$packageName = Get-ChildItem $rootDirectory\Build\BuildArtifacts\AzureSiteExtension\NewRelic.Azure.WebSites.Extension.*.nupkg -Name
	$packagePath = Convert-Path $rootDirectory\Build\BuildArtifacts\AzureSiteExtension\$packageName
	$version = $packageName.TrimStart('NewRelic.Azure.WebSites.Extension').TrimStart('.').TrimEnd('.nupkg')
	$chatMessage = "NuGet package '$packageName' has been updated with version $version."

}
else
{
	Write-Host "Input value for deployingPackage parameter not recognized."
	Exit 1
}

Write-Host "Package name: " $packageName

Write-Host "Package path: " $packagePath

Write-Host "Version: "$version

$source = 'https://www.nuget.org'

$apiKey = $env:NuGetAPIKey

Deploy $packagePath $source $apiKey

# Update the Chat room
PostMessageToChatRoom "dotnet-agent" $chatMessage "html" 1 "green"



