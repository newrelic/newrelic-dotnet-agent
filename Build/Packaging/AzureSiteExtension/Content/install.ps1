# Install.ps1
#
# Retrieves the latest NuGet package for the .NET Agent for
# Azure Web Sites and runs it with default parameters.
# If the agent configuration file is found, only an upgrade
# is performed.


# RemoveAndRename
# 
# Remove any previously-renamed files and rename current set of DLLs.
#
function RemoveAndRename
{	
	$filesToDelete = $NEW_RELIC_FOLDER + "\*.dll.save*"
	$filesToRename = $NEW_RELIC_FOLDER + "\*.dll"
	
	# Remove - silently continuing because we don't care if we cannot delete it this time as the
	# this script may have been run twice before the app pool recycled.
	Get-ChildItem $filesToDelete -Recurse | Out-File "deleteFiles.txt"
	Get-ChildItem $filesToDelete -Recurse | remove-item -ErrorAction SilentlyContinue
	
	# Rename
	Get-ChildItem $filesToRename -Recurse | Out-File "renameFiles.txt"
	Get-ChildItem $filesToRename -Recurse | rename-item -NewName { $_.Name -replace '\.dll', '.dll.save' }
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



#set agent version to default value
$agentVersion = "newest" 

$is35App = CheckIfAppIs35

$LATEST_6X_AGENT = "6.999.999"

if ($is35App -eq $TRUE){
	$agentVersion = $LATEST_6X_AGENT
}

if ($env:NEWRELIC_AGENT_VERSION_OVERRIDE -ne $null){
	try{
		$version = [System.Version]$env:NEWRELIC_AGENT_VERSION_OVERRIDE.ToString()
		$agentVersion = $version.ToString()
	}
	catch{
		exit "NEWRELIC_AGENT_VERSION_OVERRIDE environment variable has an incorrect Agent version number. Failed to install."
	}
} 


if ($env:NEWRELIC_LICENSEKEY -eq $null) {
	Write-Output "The environment variable NEWRELIC_LICENSEKEY must be set. Please make sure to add one."
}

try {
	if (Test-Path -Path "NewRelicPackage") {
		Remove-Item -Recurse NewRelicPackage
	}
}
catch {
	exit "Unable to remove 'NewRelicPackage' directory"
}

New-Item NewRelicPackage -ItemType directory
cd NewRelicPackage
New-Item logs -ItemType directory

$nugetSource = "https://www.nuget.org/api/v2/"

$packageName = "NewRelic.Azure.WebSites"

if ($env:PROCESSOR_ARCHITECTURE -ne "x86"){
	$packageName = "NewRelic.Azure.WebSites.x64"
}

if($agentVersion -eq "newest"){
	Install-Package -Name $packageName -Source $nugetSource -Provider NuGet -Force -Destination .
}
elseif ($agentVersion -eq $LATEST_6X_AGENT){
	Install-Package -Name $packageName -MaximumVersion $agentVersion -Source $nugetSource -Provider NuGet -Force -Destination .
}
else{
	Install-Package -Name $packageName -RequiredVersion $agentVersion -Source $nugetSource -Provider NuGet -Force -Destination .
}

$NEW_RELIC_FOLDER=$env:WEBROOT_PATH  + "\newrelic"
$NEW_RELIC_CONFIG_FILE = $NEW_RELIC_FOLDER + "\newrelic.config"

$IsUpgrade = Test-Path -Path $NEW_RELIC_CONFIG_FILE

cd NewRelic.Azure.Web*

if ($IsUpgrade -eq $true) {
	RemoveAndRename
	Copy-Item -path content\newrelic\*.dll -Destination $NEW_RELIC_FOLDER -Force -Recurse -ErrorAction Continue
	Copy-Item -path content\newrelic\newrelic.xsd -Destination $NEW_RELIC_FOLDER -Force -ErrorAction Continue
	Copy-Item -Path content\newrelic\extensions -Destination $NEW_RELIC_FOLDER -Force -Recurse -ErrorAction Continue
}
else {
	# Set license key from environment.
	cd content\newrelic
	$xdoc = new-object System.Xml.XmlDocument
	$file = resolve-path(".\newrelic.config")
	$xdoc.load($file)
	
	if ($env:NEWRELIC_LICENSEKEY -ne $null) {
		$xdoc.configuration.service.licenseKey = $env:NEWRELIC_LICENSEKEY.ToString()
	}
	
	$xdoc.Save($file)
	cd ..\..
	
	Copy-Item -path content\newrelic -Destination $NEW_RELIC_FOLDER -Force -Recurse -ErrorAction Continue
}

cd ..

exit $LASTEXITCODE