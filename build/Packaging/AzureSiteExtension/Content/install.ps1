############################################################
# Copyright 2020 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0
############################################################

# Install.ps1
#
# Retrieves the latest NuGet package for the .NET Agent for
# Azure Web Sites and runs it with default parameters.
# If the agent configuration file is found, only an upgrade
# is performed.

function WriteToInstallLog($output)
{
	$logPath = (Split-Path -Parent $PSCommandPath) + "\install.log"
	Write-Output "[$(Get-Date)] -- $output" | Out-File -FilePath $logPath -Append
}

function CheckIfAppIs35
{
	
	try
	{
		$xdoc = new-object System.Xml.XmlDocument
		$file = resolve-path($env:APP_POOL_CONFIG)
		$xdoc.load($file)
	
		$appPools = $xdoc.configuration.'system.applicationHost'.applicationPools.add
	
		Foreach($appPool in $appPools)
		{
			if ($appPool.Attributes["managedRuntimeVersion"].Value -eq "v2.0" )
			{		
				return $TRUE
			}
		}
	
		return $FALSE
	
	}catch
	{
		exit "Unable to detect if CLR 2.0 is being used. Failed to install .NET Agent." 
	}
}

function CopyDirectory ($fromDirectory, $destinationDirectory)
{
	if (Test-Path $destinationDirectory)
	{
		foreach ($item in Get-ChildItem -Path $fromDirectory) 
		{
			if($item -is [System.IO.DirectoryInfo])
			{
				CopyDirectory "$fromDirectory\$item" "$destinationDirectory\$item"
			}
			else
			{
				Copy-Item "$fromDirectory\$item" -Destination "$destinationDirectory" -Force
			}
		}
	}
	else
	{
		Copy-Item -path $fromDirectory -Destination $destinationDirectory -Force -Recurse -ErrorAction Continue
	}
}

function SaveNewRelicConfigAndCustomInstrumentationFiles($newRelicInstallPath)
{
	if (Test-Path -Path "$newRelicInstallPath")
	{
		if(Test-Path -Path "$newRelicInstallPath\saved_items")
		{
			WriteToInstallLog "Rename existing $newRelicInstallPath\saved_items folder to .$newRelicInstallPath\saved_items-$((Get-Date).ToString('yyyyMMddHHmmss'))"
			Rename-Item "$newRelicInstallPath\saved_items" "$newRelicInstallPath\saved_items-$((Get-Date).ToString('yyyyMMddHHmmss'))" -Force
		}
		WriteToInstallLog "Create new $newRelicInstallPath\saved_items folder."
		New-Item "$newRelicInstallPath\saved_items" -ItemType directory

		if (Test-Path -Path "$newRelicInstallPath\newrelic.config")
		{
			WriteToInstallLog "Save existing newrelic.config to the $newRelicInstallPath\saved_items folder."
			Copy-Item -Path "$newRelicInstallPath\newrelic.config" -Destination "$newRelicInstallPath\saved_items"
		}

		WriteToInstallLog "Save the following existing custom instrumemtation files to the $newRelicInstallPath\saved_items folder."
		Get-ChildItem "$newRelicInstallPath\extensions\*.xml" -Exclude "NewRelic.Providers.*" | Out-File -FilePath "$(Split-Path -Parent $PSCommandPath)\install.log" -Append
		Get-ChildItem "$newRelicInstallPath\extensions\*.xml" -Exclude "NewRelic.Providers.*" | Copy-Item -Destination "$newRelicInstallPath\saved_items"
	}
}

function RestoreCustomerRelatedFiles($newRelicInstallPath)
{
	if (Test-Path -Path "$newRelicInstallPath\saved_items")
	{
		WriteToInstallLog "Restore newrelic.config and custom instrumemtation files from the saved_items folder."
		Copy-Item -Path "$newRelicInstallPath\saved_items\newrelic.config" -Destination "$newRelicInstallPath\newrelic.config" -Force
		Get-ChildItem "$newRelicInstallPath\saved_items\*.xml" | Copy-Item -Destination "$newRelicInstallPath\extensions"
	}
}

function RenameExistingFilesAsSaveExtensionFiles($newRelicInstallPath)
{
	if (Test-Path -Path $newRelicInstallPath)
	{
		WriteToInstallLog "Rename existing Agent files to *.save files except for files in the saved_items folder."
		$toBeRenamedItems = Get-ChildItem -Path "$newRelicInstallPath" -Recurse | Where {$_.FullName -notlike "*\saved_items*"}
		foreach ($item in $toBeRenamedItems)
		{
			if(Test-Path $item.fullname -PathType Leaf)
			{
				Rename-Item $item.fullname "$($item.fullname).save" -Force
			}
		}
	}
}

function RemoveExistingSaveExtenstionFiles($newRelicInstallPath)
{
	WriteToInstallLog "Remove existing *.save files from previous upgrade if there are any."
	if(Test-Path $newRelicInstallPath)
	{
		Get-ChildItem $newRelicInstallPath -Include *.save -Recurse | Remove-Item
	}
}

function InstallNewAgent($newRelicNugetContentPath, $newRelicInstallPath)
{
	###Remove existing *.save files from previous upgrade###
	RemoveExistingSaveExtenstionFiles $newRelicInstallPath

	###Preserve existing newrelic.config and custom instrumemtation xml files###
	SaveNewRelicConfigAndCustomInstrumentationFiles $newRelicInstallPath

	###Rename all existing files as *.save files except for files in the saved_items folder.####
	RenameExistingFilesAsSaveExtensionFiles $newRelicInstallPath

	$xdoc = new-object System.Xml.XmlDocument
	$file = resolve-path(".\" + $newRelicNugetContentPath + "\newrelic.config")
	$xdoc.load($file)

	#Set Agent log location
	$xdoc.configuration.log.SetAttribute("directory", "c:\Home\LogFiles\NewRelic")
	$xdoc.Save($file)

	WriteToInstallLog "Copy items from $(Resolve-Path $newRelicNugetContentPath) to $newRelicInstallPath"
	CopyDirectory $newRelicNugetContentPath $newRelicInstallPath

	###Restore saved newrelic.config and custom instrumemtation files###
	RestoreCustomerRelatedFiles $newRelicInstallPath
}

function RemoveXmlElements($file, $xPaths)
{
	$xdoc = new-object System.Xml.XmlDocument
	$xdoc.load($file)
	foreach ($xPath in $xPaths)
	{
		$elementToBeRemoved = $xdoc.SelectSingleNode($xPath)
		if($elementToBeRemoved -ne $null)
		{
			$elementToBeRemoved.ParentNode.RemoveChild($elementToBeRemoved)
		}
	}
	$xdoc.Save($file)
}

function CopyAgentInfo($agentInfoDestination)
{
	try
	{
		$agentInfoDestinationFilePath = $agentInfoDestination + "\agentinfo.json"
		$agentInfoJson = Get-Content "$agentInfoDestinationFilePath" -Raw | ConvertFrom-Json
		$agentInfoJson | Add-Member -NotePropertyName "azure_site_extension" -NotePropertyValue $true -Force
		$agentInfoJson | ConvertTo-Json -depth 32| set-content "$agentInfoDestinationFilePath"
	}
	catch
	{
		WriteToInstallLog "Failed to configure $agentInfoFilePath  to $agentInfoDestination"
	}
}

WriteToInstallLog "Start executing install.ps1"

#Loading helper assemblies.
[Reflection.Assembly]::LoadFile((Get-ChildItem NuGet.Core.dll).FullName)
[Reflection.Assembly]::LoadFile((Get-ChildItem NewRelic.NuGetHelper.dll).FullName)
[Reflection.Assembly]::LoadFile((Get-ChildItem Microsoft.Web.XmlTransform.dll).FullName)

$nugetSource = "https://www.nuget.org/api/v2/"

$nugetPackageForFrameworkApp = "NewRelic.Azure.WebSites"
$nugetPackageForCoreApp = "NewRelic.Agent"

if ($env:PROCESSOR_ARCHITECTURE -ne "x86")
{
	$nugetPackageForFrameworkApp = "NewRelic.Azure.WebSites.x64"
}

$is35App = CheckIfAppIs35
$agentVersion = ""

if ($env:NEWRELIC_AGENT_VERSION_OVERRIDE -ne $null)
{
	try
	{
		$version = [System.Version]$env:NEWRELIC_AGENT_VERSION_OVERRIDE.ToString()
		$agentVersion = $version.ToString()
	}
	catch
	{
		exit "NEWRELIC_AGENT_VERSION_OVERRIDE environment variable has an incorrect Agent version number. Failed to install."
	}
}
elseif ($is35App -eq $TRUE)
{
	$MAX_6X_AGENT_VERSION = "6.999.999"
	$latest6XPackage = [NewRelic.NuGetHelper.Utils]::FindPackage($nugetPackageForFrameworkApp, $MAX_6X_AGENT_VERSION, $nugetSource)
	$agentVersion = $latest6XPackage.Version
}
else
{
	$latestPackage = [NewRelic.NuGetHelper.Utils]::FindPackage($nugetPackageForFrameworkApp,[NullString]::Value, $nugetSource)
	$agentVersion = $latestPackage.Version
}


if ($env:NEWRELIC_LICENSEKEY -eq $null -and $env:NEW_RELIC_LICENSE_KEY -eq $null)
{
	WriteToInstallLog "The environment variable NEWRELIC_LICENSEKEY or NEW_RELIC_LICENSE_KEY must be set. Please make sure to add one."
}

try
{
	if (Test-Path -Path "NewRelicPackage")
	{
		Remove-Item -Recurse NewRelicPackage -ErrorAction Stop
	}
}
catch
{
	exit "Unable to remove 'NewRelicPackage' directory"
}

try
{
	if (Test-Path -Path "NewRelicCorePackage")
	{
		Remove-Item -Recurse NewRelicCorePackage -ErrorAction Stop
	}
}
catch {
	exit "Unable to remove 'NewRelicCorePackage' directory"
}

if ([System.Version]$agentVersion -ge [System.Version]"8.17.438")
{
	$nugetPackageForFrameworkApp = $nugetPackageForCoreApp
}
else
{
	$xPaths = @("/configuration/system.webServer/runtime/environmentVariables/add[@name='COR_PROFILER_PATH_32']",
				"/configuration/system.webServer/runtime/environmentVariables/add[@name='COR_PROFILER_PATH_64']",
				"/configuration/system.webServer/runtime/environmentVariables/add[@name='CORECLR_ENABLE_PROFILING']",
				"/configuration/system.webServer/runtime/environmentVariables/add[@name='CORECLR_PROFILER']",
				"/configuration/system.webServer/runtime/environmentVariables/add[@name='CORECLR_PROFILER_PATH_32']",
				"/configuration/system.webServer/runtime/environmentVariables/add[@name='CORECLR_PROFILER_PATH_64']",
				"/configuration/system.webServer/runtime/environmentVariables/add[@name='CORECLR_NEWRELIC_HOME']")
	$file = resolve-path(".\applicationHost.xdt")
	RemoveXmlElements $file $xPaths
}

$packageNames = @($nugetPackageForFrameworkApp, $nugetPackageForCoreApp)
$stagingFolders = @("NewRelicPackage", "NewRelicCorePackage")
$newRelicInstallPaths = @("$env:WEBROOT_PATH\newrelic", "$env:WEBROOT_PATH\newrelic_core")
$newRelicNugetContentPaths = $(".\content\newrelic", ".\contentFiles\any\netstandard2.0\newrelic")

#Check to see if the old Agent is currently being used

For ($i=0; $i -lt $packageNames.Count; $i++)
{
	$packageName = $packageNames[$i]
	$stagingFolder = $stagingFolders[$i]
	$newRelicInstallPath= $newRelicInstallPaths[$i]
	$newRelicNugetContentPath = $newRelicNugetContentPaths[$i]

	if($packageName -eq "NewRelic.Agent" -and [System.Version]$agentVersion -lt [System.Version]"8.17.438")
	{
		WriteToInstallLog "New Relic Site Extension does not install .NET Core Agent version less than 8.17.438"
		Break
	}

	New-Item $stagingFolder -ItemType directory
	cd $stagingFolder
	WriteToInstallLog "Excecute Command: nuget install $packageName -Version $agentVersion -Source $nugetSource"
	nuget install $packageName -Version $agentVersion -Source $nugetSource 

	cd $packageName*

	InstallNewAgent $newRelicNugetContentPath $newRelicInstallPath

	CopyAgentInfo $newRelicInstallPath

	cd ..\..
}

WriteToInstallLog "End executing install.ps1."
WriteToInstallLog "-----------------------------"
exit $LASTEXITCODE