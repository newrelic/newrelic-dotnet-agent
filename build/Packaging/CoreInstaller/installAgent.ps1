############################################################
# Copyright 2020 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0
############################################################

# install_agent.ps1
#
# Installs the New Relic .Net Core Agent. Run script with -Help switch to view usage information. Additional
# information can be found at: https://docs.newrelic.com/docs/agents/net-agent?toc=true
#

param (
    [string]$Destination,
    [ValidateSet('local', 'global')][string]$installType,
    [string]$LicenseKey,
    [switch]$Force,
    [switch]$ResetIIS,
    [string]$AppName,
    [string]$LogDir,
    [switch]$X86,
    [switch]$Help
)

$64bitAgentMask = $PSScriptRoot + "\x64"
$32bitAgentMask = $PSScriptRoot + "\x86"

Function AreYouSure([string]$title, [string]$message, [string]$yesDescription, [string]$noDescription) {
    $yes = New-Object System.Management.Automation.Host.ChoiceDescription "&Yes", $yesDescription
    $no = New-Object System.Management.Automation.Host.ChoiceDescription "&No", $noDescription
    $options = [System.Management.Automation.Host.ChoiceDescription[]]($yes, $no)
    $result = $host.ui.PromptForChoice($title, $message, $options, 1)

    switch ($result) {
        1 { $False }
        0 { $True }
    }
}

Function InstallAgent() {
    $x64FilePath = (Get-Item $64bitAgentMask).FullName
    $x86FilePath = (Get-Item $32bitAgentMask).FullName

    $locationExists = Test-Path $Destination
	$locationIsEmpty = $False
	if ($locaitonExists) {
	   $locationIsEmpty = (Get-ChildItem $Destination).Length -eq 0
	}

    if ( ($installType -eq "global") -And ($X86 -eq $True)) {

        if (-Not $Force) {
            $title = "Are you sure?"
            $message = "You have chosen to install the 32bit agent globally. This is not a recommended configuration."
            $yesDescription = "Choosing 'Yes' will result in the 32bit version of the Core agent being installed globally."
            $noDescription = "Choosing 'No' will terminate this script."

            $result = AreYouSure $title $message $yesDescription $noDescription
            
            if ($result -eq $False) {
                exit
            }
        }
    }

    if ($locationExists -And (-Not($locationIsEmpty)) -And (-Not($Force))) {
        $locationExistsMessage = "
        
The installation location ($Destination) already exists. If you would like to
over-install please re-run this script with the -Force parameter.

NOTE: Forcing the install will overwrite any existing custom configuration and
instrumentation files. If you would like to save these customizations, you
must back them up to another location prior to forcing an over-install.
        
        "
        Write-Host $locationExistsMessage;
        exit
    }

    if ( -Not $locationExists) {
        New-Item $Destination -type Directory | Out-Null
        ConfigureDirectoryPermissions $Destination
    }

    $resolvedPath = (Resolve-Path -Path $Destination).Path
    
    if ( $X86 -eq $False ) {
        Get-ChildItem $resolvedPath | Remove-Item -Recurse
        Copy-Item "$x64FilePath\*" $resolvedPath -Recurse
    }
        
    else {
        Get-ChildItem $resolvedPath | Remove-Item -Recurse
        Copy-Item "$x86Filepath\*" $resolvedPath - Recurse
    }

    if ( $installType -eq "global") {
        SetEnvironmentVariables $resolvedPath
    }

    if ($LogDir) {
        $directoryExits = Test-Path $LogDir
        if ( $directoryExits -eq $False) {
            New-Item $LogDir -type Directory | Out-Null
            ConfigureDirectoryPermissions $LogDir
        }
    }

    UpdateNewRelicConfig $resolvedPath

    if ($ResetIIS -eq $True) {
        Write-Host "Performing IIS Reset"
        IISRESET
    }
}

Function CreateLogPath([string]$logPath) {
    New-Item $logPath -Directory | Out-Null
}

Function UpdateNewRelicConfig([string]$resolvedpath) {
    $configPath = $Destination
    $xdoc = New-Object System.Xml.XmlDocument
    $file = Resolve-Path("$configPath\newrelic.config")
    $xdoc.load($file)
    $xdoc.configuration.service.licenseKey = [string]$LicenseKey
    if ($AppName) {
        $xdoc.configuration.application.name = [string]$AppName
    }
    if ($LogDir) {
        $fullPath = (Resolve-Path $LogDir).Path
        ($xdoc.configuration.log).SetAttribute("directory", $fullPath);
    }
    $xdoc.Save($file)
}

Function SetEnvironmentVariables([string]$fullInstallPath) {
    [Environment]::SetEnvironmentVariable("CORECLR_ENABLE_PROFILING", "1", "Machine")
    [Environment]::SetEnvironmentVariable("CORECLR_PROFILER", "{36032161-FFC0-4B61-B559-F6C5D41BAE5A}", "Machine")
    [Environment]::SetEnvironmentVariable("CORECLR_NEWRELIC_HOME", $fullInstallPath, "Machine")
    [Environment]::SetEnvironmentVariable("CORECLR_PROFILER_PATH", (Join-Path $fullInstallPath "NewRelic.Profiler.dll"), "Machine")
}

Function IsAdmin() {
    ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")
}

Function ConfigureDirectoryPermissions($directory) {
    $acl = (Get-Item $directory).GetAccessControl('Access')
    $rule = New-Object System.Security.AccessControl.FileSystemAccessRule("IIS_IUSRS", "FullControl", "ContainerInherit, ObjectInherit", "None", "Allow")
    $acl.AddAccessRule($rule)
    Set-Acl $directory $acl
}

Function ValidateInput() {

    if ($installType) { $installType = $installType.ToLower().Trim()}
    if ($Destination) { $Destination = $Destination.Trim()}
    if ($LicenseKey) { $LicenseKey = $LicenseKey.Trim()}
    if ($AppName) { $AppName = $AppName.Trim() }
    if ($LogDir) {$LogDir = $LogDir.Trim()} 

    If (-Not($Destination)) {
        Write-Host "
    -Destination is a requried parameter
        "
        exit
    }
    
    If (-Not($installType)) {
        Write-Host "
    -InstallType is a requried parameter
        "
        exit
    }
    
    If (-Not($LicenseKey)) {
        Write-Host "
    -LicenseKey is a requried parameter
        "
        exit
    }
}

Function EnforceAdmin() {
    If (-Not(IsAdmin) -And ($installType -eq "global")) {
        Write-Host "
    You must have administrator rights in order to perform a global install of the agent. Please
    run this script from an elevated shell.
        "
        exit
    }
    
    If (-Not(IsAdmin) -And $LogDir) {
        Write-Host "
    You must have administrator rights in order to select a custom log directory. Please run this
    script from an elevated shell.
        "
        exit
    }
    
    If (-Not(IsAdmin) -And $ResetIIS) {
        Write-Host "
    You must have administrator rights in order to perform an IISReset.Please run this script from
    an elevated shell.
        "
        exit
    }
}

# If the -help switch is present we shold short circut to displaying usage info and exiting.
If ($Help) {
    Get-Content "installAgentUsage.txt"
    exit
}

ValidateInput
EnforceAdmin
InstallAgent

Write-Host "

The agent was installed successfully to $Destination
    
"