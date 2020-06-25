############################################################
# Copyright 2020 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0
############################################################

Param(
	[string] $rootDirectory,
	[string] $deployingPackage
)

$ErrorActionPreference = "Stop"

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

if($deployingPackage -eq "NewRelic.Azure.WebSites.x64")
{
	$packageName = Get-ChildItem $rootDirectory\build\BuildArtifacts\\NugetAzureWebSites-x64\NewRelic.Azure.WebSites.*.nupkg -Name
	$packagePath = Convert-Path $rootDirectory\build\BuildArtifacts\\NugetAzureWebSites-x64\$packageName
	$version = $packageName.TrimStart('NewRelic.Azure.WebSites.x').TrimStart('{64,86}').TrimStart('.').TrimEnd('.nupkg')
}
elseif($deployingPackage -eq "NewRelic.Azure.WebSites")
{
	$packageName = Get-ChildItem $rootDirectory\build\BuildArtifacts\\NugetAzureWebSites-x86\NewRelic.Azure.WebSites.*.nupkg -Name
	$packagePath = Convert-Path $rootDirectory\build\BuildArtifacts\\NugetAzureWebSites-x86\$packageName
	$version = $packageName.TrimStart('NewRelic.Azure.WebSites.x').TrimStart('{64,86}').TrimStart('.').TrimEnd('.nupkg')
}
elseif($deployingPackage -eq "NewRelic.Agent")
{
	$packageName = Get-ChildItem $rootDirectory\build\BuildArtifacts\\NugetAgent\NewRelic.Agent.*.nupkg -Name
	$packagePath = Convert-Path $rootDirectory\build\BuildArtifacts\\NugetAgent\$packageName
	$version = $packageName.TrimStart('NewRelic.Agent').TrimStart('.').TrimEnd('.nupkg')
}
elseif($deployingPackage -eq "NewRelic.Agent.Api")
{
	$packageName = Get-ChildItem $rootDirectory\build\BuildArtifacts\\NugetAgentApi\NewRelic.Agent.Api.*.nupkg -Name
	$packagePath = Convert-Path $rootDirectory\build\BuildArtifacts\\NugetAgentApi\$packageName
	$version = $packageName.TrimStart('NewRelic.Agent.Api').TrimStart('.').TrimEnd('.nupkg')
}
elseif($deployingPackage -eq "NewRelicWindowsAzure")
{
	$packageName = Get-ChildItem $rootDirectory\build\BuildArtifacts\\NugetAzureCloudServices\NewRelicWindowsAzure.*.nupkg -Name
	$packagePath = Convert-Path $rootDirectory\build\BuildArtifacts\\NugetAzureCloudServices\$packageName
	$version = $packageName.TrimStart('NewRelicWindowsAzure').TrimStart('.').TrimEnd('.nupkg')
}
elseif($deployingPackage -eq "NewRelic.Azure.WebSites.Extension")
{
	$packageName = Get-ChildItem $rootDirectory\build\BuildArtifacts\AzureSiteExtension\NewRelic.Azure.WebSites.Extension.*.nupkg -Name
	$packagePath = Convert-Path $rootDirectory\build\BuildArtifacts\AzureSiteExtension\$packageName
	$version = $packageName.TrimStart('NewRelic.Azure.WebSites.Extension').TrimStart('.').TrimEnd('.nupkg')
}
elseif($deployingPackage -eq "NewRelic.OpenTracing.AmazonLambda.Tracer")
{
	$packageName = Get-ChildItem $rootDirectory\build\BuildArtifacts\\NugetAwsLambdaOpenTracer\NewRelic.OpenTracing.AmazonLambda.Tracer.*.nupkg -Name
	$packagePath = Convert-Path $rootDirectory\build\BuildArtifacts\\NugetAwsLambdaOpenTracer\$packageName
	$version = $packageName.TrimStart('NewRelic.OpenTracing.AmazonLambda.Tracer').TrimStart('.').TrimEnd('.nupkg')
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
