############################################################
# Copyright 2020 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0
############################################################

# Uninstall.ps1
#
# Manually removes the .Net Agent files, environment variables, and registry settings.
# -Clean and -Rollback both currently eliminate logging. Eventually they may diverge.
param(
    [switch]$clean,
    [switch]$rollback
)

$global:logLocation = "$env:ProgramData\New Relic\Logs"
$LogFileDateFormat = "dd-MMM-yyyy_HH.mm.ss.ff" # Filesystem friendly
$LogEntryDateFormat = "MM/dd/yyyy HH:mm:ss.ff"

# Determines if we display information or log info to a file.
$showFeedback = $true
if($rollback) { $showFeedback = $false }

Function HasAdminRights {
	([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")
}

# Check and prep the install log location
Function PrepForLogging
{ 
    try {
        if(-Not (Test-Path -LiteralPath "$global:logLocation"))
        {
            New-Item "$global:logLocation" -Type directory -ErrorAction Stop
        }

        $now = Get-Date -format $LogFileDateFormat
        $global:logLocation = "$global:logLocation\agent-uninstall.ps1_$now.txt"
        LogEntry "INFO: Starting uninstall.ps1"
        return $True
    }
    catch {
        return $False
    }
}

# Log function
Function LogEntry ([string]$logentry, [string]$exceptionMessage = "")
{
    if ($logentry -eq "") { return }
    $now = Get-Date -format $LogEntryDateFormat

    # logfile
    Add-content "$global:logLocation" -value "$now $logentry" -ErrorAction Stop

    $exceptionExists = !([string]::IsNullOrEmpty($exceptionMessage))
    if ($exceptionExists) {
        Add-content "$InstallLogFile" -value "--> $exceptionMessage" -ErrorAction Stop
    }
   
   #console
    if ($showFeedback) {
        Write-Host "$now $logentry"

        if ($exceptionExists) {
            Write-Host $exceptionMessage
        }
    } 
}

# Checks returns ture there is an existing MSI install of the agent.
Function MsiInstalled{
	$keyBases = @(
		"HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
		"HKLM:\SOFTWARE\Classes\Installer\Products"
	)

	$keyPaths= @()
	try {
		foreach ($keyBase in $keyBases)	{
			if (!(Test-Path -Path $keyBase)) { return $false }
			Get-ChildItem -Path $keyBase | ForEach-Object -Process {
				$leaf = $_ | Split-Path -Leaf
				$data = Get-ItemProperty -LiteralPath "$keyBase\$leaf"
				if($data.DisplayName -like "New Relic .Net Agent*" -Or $data.ProductName -like "New Relic .Net Agent*"){
					$keyPaths += "$keyBase\$leaf"
				}
			}
		}
	}
	catch {
		return $false
	}

	if($keyPaths.Count -gt 0){
		return $true
	}
	return $false
}

# Find the install location via regkey
Function GetNewRelicHome
{
    # HKEY_LOCAL_MACHINE\SOFTWARE\Classes\CLSID\{71DA0A04-7777-4EC6-9643-7D28B46A8A41}\InprocServer32
    # HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Wow6432Node\CLSID\{71DA0A04-7777-4EC6-9643-7D28B46A8A41}\InprocServer32
    $filesKey = "HKLM:\SOFTWARE\Classes\CLSID\{71DA0A04-7777-4EC6-9643-7D28B46A8A41}\InprocServer32"
    if (Test-Path -LiteralPath "$filesKey")
    {
        $filesKeyData = Get-ItemProperty -LiteralPath "$filesKey" -Name "(Default)"
        return VerifyNewRelicInstallPath (Split-Path -Path $filesKeyData.'(default)')
    }
    return VerifyNewRelicInstallPath "$env:ProgramFiles\New Relic\.Net Agent\" # default
}

Function VerifyNewRelicInstallPath ([string]$_installPath)
{
    # validate the path
    if (-Not (Test-Path -LiteralPath "$_installPath"))
    {
        LogEntry "ERROR: Not able to find the .Net Agent install path at $_installPath."
    }

    return $_installPath
}

# remove Items (files, folders, whole reg keys)
Function RemoveItems ([array]$itemsToRemove) 
{
    LogEntry "INFO: Removing Items... "
    try {
        foreach ($itemToRemove in $itemsToRemove)
        {
            if (Test-Path "$itemToRemove")
            {
                Remove-Item -Force -Recurse -LiteralPath "$itemToRemove" -ErrorAction SilentlyContinue -ErrorVariable output
                LogEntry "... $itemToRemove"
            }
        }
    }
    catch {
		LogEntry $_.Exception.Message
   }
}

# remove Items (files, folders, whole reg keys)
Function RemoveItemProperties ([hashtable]$itemPropertiesToRemove)
{
    LogEntry "INFO: Removing Item Properties... "
    try {
        foreach ($key in $itemPropertiesToRemove.Keys)
        {
            if (Test-Path "$key")
            {
                $value = $itemPropertiesToRemove[$key]
                Remove-ItemProperty -Force -LiteralPath "$key" -Name "$value" -ErrorAction SilentlyContinue -ErrorVariable output
                LogEntry "... $key $value"
            }
        }
    }
    catch {
	LogEntry $_.Exception.Message
    }
}

# Finds and then removes the reg keys created via the MSI installer.
Function FindAndRemoveMSIKeys ([array]$keyBases)
{
    LogEntry "INFO: Removing Traditional MSI Install/Uninstall Keys, if present... "
    try {
        foreach ($keyBase in $keyBases)
        {
            $keyPaths=@()

            if (Test-Path -Path $keyBase) { 
                Get-ChildItem -Path $keyBase | ForEach-Object -Process {
                    $leaf = $_ | Split-Path -Leaf
                    $data = Get-ItemProperty -LiteralPath "$keyBase\$leaf"
                    if($data.DisplayName -like "New Relic .NET Agent*" -Or $data.ProductName -like "New Relic .NET Agent*")
                    {
                        $keyPaths += "$keyBase\$leaf"
                    }
                }
            }
            if ($keyPaths.Length -gt 0) { RemoveItems $keyPaths }
        }
    }
    catch {
		LogEntry $_.Exception.Message
    }
}

Function RemoveSystemVariables
{
    LogEntry "INFO: Removing System Environment Variables, if present... "
    try {
        [Environment]::SetEnvironmentVariable("COR_ENABLE_PROFILING","", "Machine")
        [Environment]::SetEnvironmentVariable("COR_PROFILER","", "Machine")
        [Environment]::SetEnvironmentVariable("NEWRELIC_INSTALL_PATH","", "Machine")
    }
    catch {
		LogEntry $_.Exception.Message
    }
}

# Main block

$PrepForLoggingStatus = PrepForLogging
if ($PrepForLoggingStatus -eq $false) {
    Write-Output "WARNING: Unable to provide log file, uninstall continues."
}

if (-Not(HasAdminRights)) {
    LogEntry "ERROR: You must have administrator rights to run this uninstaller."
    Start-Sleep 2
    exit 1
}

$msiInstalledStatus = MsiInstalled
if ($msiInstalledStatus -eq $True) {
    LogEntry ("ERROR: There appears to be a version of the agent that was installed with the MSI on this machine. You must use the MSI installer to remove this installation.")
    exit 1
}

LogEntry "INFO: Stopping IIS."
iisreset /stop > $null

RemoveItems @(
    "$(GetNewRelicHome)",
    "${env:ProgramFiles(x86)}\New Relic\.Net Agent\"
    "$env:ProgramData\New Relic\.Net Agent\",
    "HKLM:\SOFTWARE\New Relic",
    "HKLM:\SOFTWARE\Wow6432Node\New Relic",
    "HKLM:\SOFTWARE\Classes\CLSID\{71DA0A04-7777-4EC6-9643-7D28B46A8A41}",
    "HKLM:\SOFTWARE\Classes\Wow6432Node\CLSID\{71DA0A04-7777-4EC6-9643-7D28B46A8A41}"
)

# curly braces are correct, this is a hashmap 
RemoveItemProperties @{
    "HKLM:SYSTEM\CurrentControlSet\Services\W3SVC" = "Environment";
    "HKLM:SYSTEM\CurrentControlSet\Services\WAS" = "Environment"
}

FindAndRemoveMSIKeys @(
    "HKLM:SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
    "HKLM:SOFTWARE\Classes\Installer\Products"
)

RemoveSystemVariables

LogEntry "INFO: Starting IIS."
iisreset /start > $null
LogEntry "INFO: Uninstall Completed."

$env:program