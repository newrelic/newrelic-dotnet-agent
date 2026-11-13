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

$ProgressPreference = 'SilentlyContinue'

function WriteToInstallLog($output)
{
	try
	{
		$logPath = (Split-Path -Parent $PSCommandPath) + "\install.log"
		Write-Output "[$(Get-Date)] -- $output" | Out-File -FilePath $logPath -Append
	}
	catch
	{
	}
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
		WriteToInstallLog "Unable to detect if CLR 2.0 is being used. Failed to install New Relic .NET Agent."
		exit 1
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
				Copy-Item "$fromDirectory\$item" -Destination "$destinationDirectory" -Force -ErrorAction Stop
			}
		}
	}
	else
	{
		Copy-Item -path $fromDirectory -Destination $destinationDirectory -Force -Recurse -ErrorAction Stop
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

		WriteToInstallLog "Save the following existing custom instrumentation files to the $newRelicInstallPath\saved_items folder."
		Get-ChildItem "$newRelicInstallPath\extensions\*.xml" -Exclude "NewRelic.Providers.*" | Out-File -FilePath "$(Split-Path -Parent $PSCommandPath)\install.log" -Append
		Get-ChildItem "$newRelicInstallPath\extensions\*.xml" -Exclude "NewRelic.Providers.*" | Copy-Item -Destination "$newRelicInstallPath\saved_items"
	}
}

function RestoreCustomerRelatedFiles($newRelicInstallPath, $newRelicLegacyInstallPath)
{
	if (Test-Path -Path "$newRelicInstallPath\saved_items")
	{
		WriteToInstallLog "Restore newrelic.config and custom instrumentation files from the saved_items folder."
		Copy-Item -Path "$newRelicInstallPath\saved_items\newrelic.config" -Destination "$newRelicInstallPath\newrelic.config" -Force
		Get-ChildItem "$newRelicInstallPath\saved_items\*.xml" | Copy-Item -Destination "$newRelicInstallPath\extensions"
	}
	# If there aren't any saved_items in the current install path, then this may be an upgrade between versions of the site extension
	# that changed the storage path for where the new relic agent lives. The uninstall of the site extension removes the legacy storage
	# locations, so this migration should only ever happen once.
	elseif (Test-Path -Path $newRelicLegacyInstallPath)
	{
		WriteToInstallLog "Migrating newrelic.config and custom instrumentation files from old site extension storage location."
		if (Test-Path -Path "$newRelicLegacyInstallPath\newrelic.config")
		{
			WriteToInstallLog "Migrating $newRelicLegacyInstallPath\newrelic.config to $newRelicInstallPath\newrelic.config."
			Copy-Item -Path "$newRelicLegacyInstallPath\newrelic.config" -Destination "$newRelicInstallPath\newrelic.config" -Force
		}

		WriteToInstallLog "Migrating the following custom instrumentation files to the $newRelicInstallPath."
		Get-ChildItem "$newRelicLegacyInstallPath\extensions\*.xml" -Exclude "NewRelic.Providers.*" | Out-File -FilePath "$(Split-Path -Parent $PSCommandPath)\install.log" -Append
		Get-ChildItem "$newRelicLegacyInstallPath\extensions\*.xml" -Exclude "NewRelic.Providers.*" | Copy-Item -Destination "$newRelicInstallPath\extensions"
	}
}

function RenameExistingFilesAsSaveExtensionFiles($newRelicInstallPath, $target)
{
	if (Test-Path -Path $newRelicInstallPath)
	{
		WriteToInstallLog "Rename existing Agent files to *.save files except for files in the saved_items folder."
		$toBeRenamedItems = Get-ChildItem -Path "$newRelicInstallPath" -Recurse | Where-Object {$_.FullName -notlike "*\saved_items*" -and $_.Name -notlike '*.save' -and $_.Name -notlike '*.save.stale-*'}
		foreach ($item in $toBeRenamedItems)
		{
			if(Test-Path -LiteralPath $item.FullName -PathType Leaf)
			{
				$saveName = "$($item.Name).save"
				# A stale .save file that could not be deleted is renamed out of the way so the restore does not lose the previous file.
				if (Test-Path -LiteralPath "$($item.FullName).save")
				{
					Rename-Item -LiteralPath "$($item.FullName).save" -NewName "$saveName.stale-$((Get-Date).ToString('yyyyMMddHHmmss'))" -Force -ErrorAction Stop
				}
				Rename-Item -LiteralPath $item.FullName -NewName $saveName -Force -ErrorAction Stop
				$target.Renamed.Add($item.FullName)
			}
		}
	}
}

function RemoveExistingSaveExtensionFiles($newRelicInstallPath)
{
	WriteToInstallLog "Remove existing *.save files from previous upgrade if there are any."
	if(Test-Path $newRelicInstallPath)
	{
		Get-ChildItem $newRelicInstallPath -Include *.save -Recurse | Remove-Item
		Get-ChildItem $newRelicInstallPath -Include *.save.stale-* -Recurse | Remove-Item -ErrorAction SilentlyContinue
	}
}

function RestoreSavedFiles($target)
{
	$installPath = $target.InstallPath
	$restored = $true

	if ($target.ExistedBefore -ne $true)
	{
		try
		{
			if (Test-Path -LiteralPath $installPath)
			{
				Remove-Item -LiteralPath $installPath -Recurse -Force -ErrorAction Stop
			}
			WriteToInstallLog "Removed the partial first install in $installPath."
			return $true
		}
		catch
		{
			$err = $_.Exception.Message
			WriteToInstallLog "Unable to remove ${installPath}: $err"
			WriteToInstallLog "The previous agent files in $installPath could not be fully restored. Restart the app and reinstall the site extension."
			return $false
		}
	}

	$renamed = New-Object 'System.Collections.Generic.HashSet[string]' -ArgumentList ([System.StringComparer]::OrdinalIgnoreCase)
	foreach ($original in $target.Renamed)
	{
		$null = $renamed.Add($original)
	}

	# Delete the files that the new version added. The files this run renamed are replaced in the next loop.
	if ($target.CopyStarted -eq $true)
	{
		$candidates = @()
		try
		{
			$candidates = @(Get-ChildItem -LiteralPath $installPath -Recurse -File -ErrorAction Stop | Where-Object { $_.FullName -notlike "*\saved_items*" -and -not $renamed.Contains($_.FullName) })
		}
		catch
		{
			$err = $_.Exception.Message
			WriteToInstallLog "Unable to list the files in ${installPath}: $err"
			$restored = $false
		}

		foreach ($file in $candidates)
		{
			$relativePath = $file.FullName.Substring($installPath.Length).TrimStart('\')
			if (Test-Path -LiteralPath (Join-Path $target.ContentFullPath $relativePath) -PathType Leaf)
			{
				try
				{
					Remove-Item -LiteralPath $file.FullName -Force -ErrorAction Stop
				}
				catch
				{
					$err = $_.Exception.Message
					WriteToInstallLog "Unable to delete $($file.FullName): $err"
					$restored = $false
				}
			}
		}
	}

	foreach ($original in $target.Renamed)
	{
		try
		{
			Move-Item -LiteralPath "$original.save" -Destination $original -Force -ErrorAction Stop
		}
		catch
		{
			$err = $_.Exception.Message
			WriteToInstallLog "Unable to restore ${original}: $err"
			$restored = $false
		}
	}

	if ($restored)
	{
		WriteToInstallLog "Restored the previous agent files in $installPath."
	}
	else
	{
		WriteToInstallLog "The previous agent files in $installPath could not be fully restored. Restart the app and reinstall the site extension."
	}
	return $restored
}

function InstallNewAgent($newRelicNugetContentPath, $newRelicInstallPath, $newRelicLegacyInstallPath, $target)
{
	###Remove existing *.save files from previous upgrade###
	RemoveExistingSaveExtensionFiles $newRelicInstallPath

	###Preserve existing newrelic.config and custom instrumemtation xml files###
	SaveNewRelicConfigAndCustomInstrumentationFiles $newRelicInstallPath

	###Rename all existing files as *.save files except for files in the saved_items folder.####
	RenameExistingFilesAsSaveExtensionFiles $newRelicInstallPath $target

	$xdoc = new-object System.Xml.XmlDocument
	$file = resolve-path(".\" + $newRelicNugetContentPath + "\newrelic.config")
	$xdoc.load($file)

	#Set Agent log location
	$xdoc.configuration.log.SetAttribute("directory", "$env:HOME\LogFiles\NewRelic")
	$xdoc.Save($file)

	WriteToInstallLog "Copy items from $(Resolve-Path $newRelicNugetContentPath) to $newRelicInstallPath"
	$target.CopyStarted = $true
	CopyDirectory $newRelicNugetContentPath $newRelicInstallPath

	###Restore saved newrelic.config and custom instrumemtation files###
	RestoreCustomerRelatedFiles $newRelicInstallPath $newRelicLegacyInstallPath

	###Remove Linux profiler since it won't be used.
	$linuxProfiler = "$newRelicInstallPath\libNewRelicProfiler.so"
	if(Test-Path $linuxProfiler)
	{
		WriteToInstallLog "Remove Linux profiler since it won't be used"
		Remove-Item $linuxProfiler
	}
}

function RemoveXmlElements($file, $xPaths)
{
    $xdoc = new-object System.Xml.XmlDocument
    $xdoc.load($file)
    $namespaceManager = new-object System.Xml.XmlNamespaceManager($xdoc.NameTable)
    $namespaceManager.AddNamespace("xdt", "http://schemas.microsoft.com/XML-Document-Transform")

    foreach ($xPath in $xPaths)
    {
        $node = $xdoc.SelectSingleNode($xPath, $namespaceManager)
        if ($node -ne $null)
        {
            $node.ParentNode.RemoveChild($node)
        }
    }
    $xdoc.Save($file)
}

function AddXmlElements($file, $xPaths)
{
    $xdoc = new-object System.Xml.XmlDocument
    $xdoc.load($file)
    $namespaceManager = new-object System.Xml.XmlNamespaceManager($xdoc.NameTable)
    $namespaceManager.AddNamespace("xdt", "http://schemas.microsoft.com/XML-Document-Transform")

    foreach ($xPath in $xPaths)
    {
        $newElement = $xdoc.CreateElement($xPath.Name)
        foreach ($attribute in $xPath.Attributes.GetEnumerator())
        {
            if ($attribute.Key -like "xdt:*")
            {
                $newAttribute = $xdoc.CreateAttribute($attribute.Key, $namespaceManager.LookupNamespace("xdt"))
            }
            else
            {
                $newAttribute = $xdoc.CreateAttribute($attribute.Key)
            }
            $newAttribute.Value = $attribute.Value
            $newElement.Attributes.Append($newAttribute)
        }
        $parentNode = $xdoc.SelectSingleNode($xPath.ParentPath, $namespaceManager)
        if ($parentNode -ne $null)
        {
            $parentNode.AppendChild($newElement)
        }
    }
    $xdoc.Save($file)
}

function CopyAgentInfo($agentInfoDestination)
{
	try
	{
		$agentInfoDestinationFilePath = $agentInfoDestination + "\agentinfo.json"

		if(-Not(Test-Path $agentInfoDestinationFilePath -PathType Leaf))
		{
			return
		}

		$agentInfoJson = Get-Content "$agentInfoDestinationFilePath" -Raw | ConvertFrom-Json
		$agentInfoJson | Add-Member -NotePropertyName "azure_site_extension" -NotePropertyValue $true -Force
		$agentInfoJson | ConvertTo-Json -depth 32| set-content "$agentInfoDestinationFilePath"
	}
	catch
	{
		WriteToInstallLog "Failed to configure $agentInfoFilePath  to $agentInfoDestination"
	}
}

function RemoveNewRelicInstallArtifacts($fromDirectory)
{
	WriteToInstallLog "Removing New Relic .NET Agent install artifacts"

	try
	{
		if (Test-Path -Path "$fromDirectory\NewRelicPackage")
		{
			Remove-Item -Recurse "$fromDirectory\NewRelicPackage" -ErrorAction Stop
		}
	}
	catch
	{
		WriteToInstallLog "Unable to remove 'NewRelicPackage' directory"
		exit 1
	}

	try
	{
		if (Test-Path -Path "$fromDirectory\NewRelicCorePackage")
		{
			Remove-Item -Recurse "$fromDirectory\NewRelicCorePackage" -ErrorAction Stop
		}
	}
	catch
    {
		WriteToInstallLog "Unable to remove 'NewRelicCorePackage' directory"
		exit 1
	}
}

function RemoveDeprecatedInstrumentationFiles($newRelicInstallPath)
{
	# Logging files
	Remove-Item "$newRelicInstallPath\extensions\NewRelic.Providers.Wrapper.Logging.Instrumentation.xml" -ErrorAction Ignore
	Remove-Item "$newRelicInstallPath\extensions\NewRelic.Providers.Wrapper.Logging.dll" -ErrorAction Ignore

	# Asp35 was renamed to AspNet
	Remove-Item "$newRelicInstallPath\extensions\NewRelic.Providers.Wrapper.Asp35.Instrumentation.xml" -ErrorAction Ignore
	Remove-Item "$newRelicInstallPath\extensions\NewRelic.Providers.Wrapper.Asp35.dll" -ErrorAction Ignore
}

function GetLatestPackageVersion($packageId, $maxVersion, $repoUrl)
{
	# FindPackagesById() paginates at 100 entries per page (oldest-first, not sorted by version),
	# so the newest versions of a long-lived package are often on a later page. Follow every
	# rel="next" link and collect all versions before picking the max -- using Invoke-WebRequest
	# here (rather than Invoke-RestMethod) so the feed-level <link> elements survive; RestMethod's
	# Atom auto-deserialization keeps only the <entry> nodes and discards them.
	$uri = "$($repoUrl)FindPackagesById()?id='$packageId'"
	$allVersions = @()

	while ($uri)
	{
		$response = Invoke-WebRequest -Uri $uri -UseBasicParsing
		[xml]$feed = $response.Content

		$allVersions += $feed.feed.entry.properties.Version

		$nextLink = $feed.feed.link | Where-Object { $_.rel -eq "next" } | Select-Object -First 1
		$uri = if ($nextLink) { $nextLink.href } else { $null }
	}

	$versions = $allVersions |
		Where-Object { $_ -notmatch '-' } |
		Where-Object { [string]::IsNullOrEmpty($maxVersion) -or [System.Version]$_ -le [System.Version]$maxVersion }

	return ($versions | Sort-Object { [System.Version]$_ } | Select-Object -Last 1)
}

function InvokeNuget($arguments, $logFolder)
{
	$stdoutFile = "$logFolder\nuget.stdout.log"
	$stderrFile = "$logFolder\nuget.stderr.log"

	# Start-Process with -NoNewWindow because running executables within Kudu can often fail with "Window title cannot be longer than 1023 characters". See: https://github.com/projectkudu/kudu/issues/2635.
	$proc = Start-Process nuget -ArgumentList $arguments -NoNewWindow -PassThru -RedirectStandardOutput $stdoutFile -RedirectStandardError $stderrFile
	$null = $proc.Handle

	# Periodic output so a host idle timeout sees activity.
	while (-not $proc.WaitForExit(15000))
	{
		[Console]::Out.WriteLine("Waiting for nuget...")
	}

	$logPath = (Split-Path -Parent $PSCommandPath) + "\install.log"
	foreach ($file in @($stdoutFile, $stderrFile))
	{
		$lines = Get-Content -Path $file -ErrorAction SilentlyContinue
		if ($lines)
		{
			foreach ($line in $lines)
			{
				[Console]::Out.WriteLine([string]$line)
			}
			try { $lines | Out-File -FilePath $logPath -Append } catch { }
		}
	}

	if ($null -eq $proc.ExitCode)
	{
		return -1
	}

	return [int]$proc.ExitCode
}

function InvokeNugetInstall($packageName, $version, $source, $outputDirectory, $configPath)
{
	# nuget install skips signature validation and exits 0 when the package folder already exists.
	if (Test-Path -Path "$outputDirectory\$packageName.$version")
	{
		StopInstall "$outputDirectory\$packageName.$version already exists before nuget install, so nuget would skip signature validation. The previous agent was not changed."
	}

	$arguments = "install $packageName -Version $version -Source $source -OutputDirectory `"$outputDirectory`" -NonInteractive"
	if ($null -ne $configPath)
	{
		$arguments += " -ConfigFile `"$configPath`""
	}

	WriteToInstallLog "Execute Command: nuget $arguments"
	$exitCode = InvokeNuget $arguments $outputDirectory
	return [int]$exitCode
}

function NewSignatureValidationConfig($configPath)
{
	$seed = '<?xml version="1.0" encoding="utf-8"?><configuration><config><add key="signatureValidationMode" value="require" /></config></configuration>'

	$logFolder = Split-Path -Parent $configPath

	for ($attempt = 1; $attempt -le 2; $attempt++)
	{
		try
		{
			[System.IO.File]::WriteAllText($configPath, $seed)

			$arguments = "trusted-signers add -Name nuget.org -ServiceIndex https://api.nuget.org/v3/index.json -Owners newrelic -ConfigFile `"$configPath`" -NonInteractive"
			WriteToInstallLog "Execute Command (attempt $attempt): nuget $arguments"
			$exitCode = InvokeNuget $arguments $logFolder

			if ($exitCode -eq 0)
			{
				$xdoc = New-Object System.Xml.XmlDocument
				$xdoc.Load($configPath)
				$mode = $xdoc.SelectSingleNode("/configuration/config/add[@key='signatureValidationMode']")
				$certificates = $xdoc.SelectNodes("/configuration/trustedSigners/repository/certificate")
				$owners = $xdoc.SelectSingleNode("/configuration/trustedSigners/repository/owners")

				$failedChecks = @()
				if ($null -eq $mode -or $mode.GetAttribute("value") -ne "require")
				{
					$failedChecks += "signatureValidationMode is not require"
				}
				if ($certificates.Count -lt 1)
				{
					$failedChecks += "certificate count is $($certificates.Count)"
				}
				if ($null -eq $owners -or $owners.InnerText -cne "newrelic")
				{
					$failedChecks += "owners value is '$(if ($null -ne $owners) { $owners.InnerText })', expected 'newrelic'"
				}

				if ($failedChecks.Count -eq 0)
				{
					return $true
				}
				WriteToInstallLog "The nuget signature validation config is not valid (attempt ${attempt}): $($failedChecks -join '; ')."
			}
		}
		catch
		{
			$err = $_.Exception.Message
			WriteToInstallLog "Failed to create the nuget signature validation config (attempt ${attempt}): $err"
		}
	}

	$warning = "WARNING: Unable to get the nuget.org repository certificates. The agent package signatures will not be validated."
	WriteToInstallLog $warning
	[Console]::Out.WriteLine($warning)
	Remove-Item -Path $configPath -Force -ErrorAction Ignore
	return $false
}

function StopInstall($message)
{
	WriteToInstallLog $message
	cd $scriptDir
	RemoveNewRelicInstallArtifacts $scriptDir
	exit 1
}

try
{
	$scriptDir = Split-Path -Parent $PSCommandPath

	WriteToInstallLog "Start executing install.ps1"

	#Using Tls12
	[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bOR [Net.SecurityProtocolType]::Tls12

	$nugetSource = "https://www.nuget.org/api/v2/"

	$nugetPackageForFrameworkApp = "NewRelic.Azure.WebSites"
	$nugetPackageForCoreApp = "NewRelic.Agent"

	if ($env:PROCESSOR_ARCHITECTURE -ne "x86")
	{
		$nugetPackageForFrameworkApp = "NewRelic.Azure.WebSites.x64"
	}

	$is35App = CheckIfAppIs35
	$agentVersion = ""

	if ($env:NEW_RELIC_AGENT_VERSION_OVERRIDE -ne $null)
	{
		try
		{
			$version = [System.Version]$env:NEW_RELIC_AGENT_VERSION_OVERRIDE.ToString()
			$agentVersion = $version.ToString()
		}
		catch
		{
			WriteToInstallLog "NEW_RELIC_AGENT_VERSION_OVERRIDE environment variable has an incorrect Agent version number. Failed to install."
			exit 1
		}
	}
	elseif ($env:NEWRELIC_AGENT_VERSION_OVERRIDE -ne $null) # check for deprecated environment variable
	{
		try
		{
			$version = [System.Version]$env:NEWRELIC_AGENT_VERSION_OVERRIDE.ToString()
			$agentVersion = $version.ToString()
			WriteToInstallLog "NEWRELIC_AGENT_VERSION_OVERRIDE environment variable is deprecated and may be removed in a future release. Please use NEW_RELIC_AGENT_VERSION_OVERRIDE instead."
		}
		catch
		{
			WriteToInstallLog "NEWRELIC_AGENT_VERSION_OVERRIDE environment variable has an incorrect Agent version number. Failed to install."
			exit 1
		}
	}
	elseif ($is35App -eq $TRUE)
	{
		$MAX_6X_AGENT_VERSION = "6.999.999"
		$agentVersion = GetLatestPackageVersion $nugetPackageForFrameworkApp $MAX_6X_AGENT_VERSION $nugetSource
	}
	else
	{
		$agentVersion = GetLatestPackageVersion $nugetPackageForFrameworkApp $null $nugetSource
	}


	if ($env:NEWRELIC_LICENSEKEY -eq $null -and $env:NEW_RELIC_LICENSE_KEY -eq $null)
	{
		WriteToInstallLog "The environment variable NEWRELIC_LICENSE_KEY must be set. Please make sure to add it."
	}

	RemoveNewRelicInstallArtifacts $scriptDir

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

    if ($env:FUNCTIONS_WORKER_RUNTIME -ne $null) # for Azure Functions, enable azure function mode and remove <system.applicationHost>
    {
        $xPaths = @(
            @{ Name = "add"; Attributes = @{ name = "NEW_RELIC_AZURE_FUNCTION_MODE_ENABLED"; value = "1"; "xdt:Locator" = "Match(name)"; "xdt:Transform" = "InsertIfMissing" }; ParentPath = "/configuration/system.webServer/runtime/environmentVariables" },
            @{ Name = "add"; Attributes = @{ name = "NEW_RELIC_LOG_DIRECTORY"; value = "%HOME%\LogFiles\NewRelic"; "xdt:Locator" = "Match(name)"; "xdt:Transform" = "InsertIfMissing" }; ParentPath = "/configuration/system.webServer/runtime/environmentVariables" }
        )
        $file = resolve-path ".\applicationHost.xdt"
        WriteToInstallLog "Adding Azure Functions environment variables to applicationHost.xdt"
        AddXmlElements $file $xPaths
    }

	# For 8.17.438 and later, one package holds both the Framework and the Core content.
	$packageName = $nugetPackageForFrameworkApp
	$newRelicInstallPaths = @("$env:HOME\NewRelicAgent\Framework", "$env:HOME\NewRelicAgent\Core")
	$newRelicLegacyInstallPaths = @("$env:WEBROOT_PATH\newrelic", "$env:WEBROOT_PATH\newrelic_core")
	$newRelicNugetContentPaths = @(".\content\newrelic", ".\contentFiles\any\netstandard2.0\newrelic")

	$stagingFolder = "$scriptDir\NewRelicPackage"
	$packageFolder = "$stagingFolder\$packageName.$agentVersion"

	$targetCount = 2
	if ([System.Version]$agentVersion -lt [System.Version]"8.17.438")
	{
		WriteToInstallLog "New Relic .NET Agent Site Extension does not install .NET Core Agent version less than 8.17.438"
		$targetCount = 1
	}

	$installTargets = New-Object 'System.Collections.Generic.List[hashtable]'
	for ($i = 0; $i -lt $targetCount; $i++)
	{
		$installTargets.Add(@{
			InstallPath = [System.IO.Path]::GetFullPath($newRelicInstallPaths[$i])
			LegacyInstallPath = $newRelicLegacyInstallPaths[$i]
			ContentPath = $newRelicNugetContentPaths[$i]
			ContentFullPath = [System.IO.Path]::GetFullPath((Join-Path $packageFolder $newRelicNugetContentPaths[$i]))
			ExistedBefore = $false
			CopyStarted = $false
			Renamed = New-Object 'System.Collections.Generic.List[string]'
		})
	}

	# Phase 1: download and validate the package. Nothing under the install paths changes here.
	$null = New-Item $stagingFolder -ItemType directory

	$signingConfigPath = "$stagingFolder\nuget.signing.config"
	if ((NewSignatureValidationConfig $signingConfigPath) -ne $true)
	{
		$signingConfigPath = $null
	}

	$exitCode = InvokeNugetInstall $packageName $agentVersion $nugetSource $stagingFolder $signingConfigPath
	if ($exitCode -ne 0)
	{
		StopInstall "nuget install $packageName $agentVersion failed with exit code $exitCode. The previous agent was not changed."
	}

	if (-Not (Test-Path -Path $packageFolder))
	{
		StopInstall "$packageName.$agentVersion folder does not exist. The previous agent was not changed."
	}

	foreach ($target in $installTargets)
	{
		if (-Not (Test-Path -Path "$($target.ContentFullPath)\newrelic.config"))
		{
			StopInstall "The package $packageName $agentVersion does not contain $($target.ContentPath)\newrelic.config. The previous agent was not changed."
		}
	}

	# Phase 2: install into the agent folders.
	cd $packageFolder

	$startedTargets = New-Object 'System.Collections.Generic.List[hashtable]'
	$failedTarget = $null

	try
	{
		foreach ($target in $installTargets)
		{
			$failedTarget = $target
			$target.ExistedBefore = Test-Path $target.InstallPath
			$startedTargets.Add($target)

			InstallNewAgent $target.ContentPath $target.InstallPath $target.LegacyInstallPath $target

			CopyAgentInfo $target.InstallPath

			RemoveDeprecatedInstrumentationFiles $target.InstallPath
		}
	}
	catch
	{
		$err = $_.Exception.Message
		cd $scriptDir

		$allRestored = $true
		foreach ($startedTarget in $startedTargets)
		{
			try
			{
				if ((RestoreSavedFiles $startedTarget) -ne $true)
				{
					$allRestored = $false
				}
			}
			catch
			{
				$restoreErr = $_.Exception.Message
				WriteToInstallLog "Restore of $($startedTarget.InstallPath) failed: $restoreErr"
				$allRestored = $false
			}
		}

		$outcome = "The previous agent was restored."
		if (-not $allRestored)
		{
			$outcome = "The previous agent could not be fully restored. Restart the app and reinstall the site extension."
		}
		elseif (@($startedTargets | Where-Object { $_.ExistedBefore -eq $true }).Count -eq 0)
		{
			$outcome = "The partial install was removed."
		}
		StopInstall "Failed to install the agent to $($failedTarget.InstallPath): $($err.TrimEnd('.')). $outcome"
	}

	cd $scriptDir

	RemoveNewRelicInstallArtifacts $scriptDir

	WriteToInstallLog "End executing install.ps1."
	WriteToInstallLog "-----------------------------"
	exit 0
}
catch
{
	$err = $_.Exception.Message
	StopInstall $err
}
