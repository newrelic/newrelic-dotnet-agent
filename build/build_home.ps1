############################################################
# Copyright 2020 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0
############################################################

####################################################################################
# build_home - Builds a set of home folders to test and run the agent on a dev box #
####################################################################################

Param(
    [ValidateSet("Debug","Release")][string]$Configuration = "Debug",
    [ValidateSet("All", "Framework","Core")][string]$Type = "All",
    [ValidateSet("All","x64","x86")][string]$Architecture = "All",
    [string]$HomePath = "$env:NR_DEV_HOMEROOT",
    [switch]$KeepNewRelicConfig = $false,
    [switch]$SetSystemEnvironment = $false,
    [switch]$SetSessionEnvironment = $false
)

$ErrorActionPreference = "Stop"

##############################
# Setup and resolve pathing  #
##############################

Write-Host "Setting up params"

$rootDirectory = Resolve-Path "$(Split-Path -Parent $PSCommandPath)\.."
$extRootDir = "$rootDirectory\src\Agent\NewRelic\Agent\Extensions"
$wrappersRootDir = "$extRootDir\Providers\Wrapper"
$storageRootDir = "$extRootDir\Providers\Storage"

if ($env:KeepNewRelicConfig) {
    $KeepNewRelicConfig = [System.Convert]::ToBoolean($env:KeepNewRelicConfig)
}

if ($env:SetSystemEnvironment) {
    $SetSystemEnvironment = [System.Convert]::ToBoolean($env:SetSystemEnvironment)
}

if ($env:SetSessionEnvironment) {
    $SetSessionEnvironment = [System.Convert]::ToBoolean($env:SetSessionEnvironment)
}

. "$rootDirectory\build\build_functions.ps1"
$HomePath = Get-HomeRootPath $HomePath

################################
# Gather data for home folders #
################################

$net35WrapperHash = @{}
$netstandard20WrapperHash = @{}

$wrapperDirs = Get-ChildItem -LiteralPath "$wrappersRootDir" -Directory
foreach ($wrapperDir in $wrapperDirs) {
    $wrapperDirPath = $wrapperDir.FullName
    $wrapperName = $wrapperDir.Name
    if ($net35path = Resolve-Path "$wrapperDirPath\bin\$Configuration\net3*") {
        $dllObject = Get-ChildItem -File -Path "$net35path" -Filter NewRelic.Providers.Wrapper.$wrapperName.dll
        $xmlObject = Get-ChildItem -File -Path "$net35path" -Filter Instrumentation.xml
        $net35WrapperHash.Add($dllObject, $xmlObject)
    }

    if ($net4Xpath = Resolve-Path "$wrapperDirPath\bin\$Configuration\net4*") {
        $dllObject = Get-ChildItem -File -Path "$net4Xpath" -Filter NewRelic.Providers.Wrapper.$wrapperName.dll
        $xmlObject = Get-ChildItem -File -Path "$net4Xpath" -Filter Instrumentation.xml
        $net35WrapperHash.Add($dllObject, $xmlObject)
    }

    if ($netstandard20path = Resolve-Path "$wrapperDirPath\bin\$Configuration\netstandard2.*") {
        $dllObject = Get-ChildItem -File -Path "$netstandard20path" -Filter NewRelic.Providers.Wrapper.$wrapperName.dll
        $xmlObject = Get-ChildItem -File -Path "$netstandard20path" -Filter Instrumentation.xml
        $netstandard20WrapperHash.Add($dllObject, $xmlObject)
    }
}

# AspNetCore needs only to be in netstandard2.0
if ($apsNetCorePath = Resolve-Path "$wrappersRootDir\AspNetCore\bin\$Configuration\netstandard2.*") {
    $dllObject = Get-ChildItem -File -Path "$apsNetCorePath" -Filter NewRelic.Providers.Wrapper.AspNetCore.dll
    $xmlObject = Get-ChildItem -File -Path "$apsNetCorePath" -Filter Instrumentation.xml
    $netstandard20WrapperHash.Add($dllObject, $xmlObject)
}

$net35StorageArray = @()
$netstandard20StorageArray = @()

$storageDirs = Get-ChildItem -LiteralPath "$storageRootDir" -Directory
foreach ($storageDir in $storageDirs) {
    $storageDirPath = $storageDir.FullName
    $storageName = $storageDir.Name

    if ($net35path = Resolve-Path "$storageDirPath\bin\$Configuration\net3*") {
        $dllObject = Get-ChildItem -File -Path "$net35path" -Filter NewRelic.Providers.$storageName.dll
        $net35StorageArray += $dllObject
    }

    if ($netstandard20path = Resolve-Path "$storageDirPath\bin\$Configuration\netstandard2.*") {
        $dllObject = Get-ChildItem -File -Path "$netstandard20path" -Filter NewRelic.Providers.$storageName.dll
        $netstandard20StorageArray += $dllObject
    }

    if ($net35path = Resolve-Path "$storageDirPath\bin\$Configuration\net4*") {
        $dllObject = Get-ChildItem -File -Path "$net35path" -Filter NewRelic.Providers.$storageName.dll
        $net35StorageArray += $dllObject
        $netstandard20StorageArray += $dllObject
    }
}


#######################
# Create home folders #
#######################

Write-Host "Creating home folders"

if ($Type -like "All" -or $Type -like "Framework") {
    if ($Architecture -like "All" -or $Architecture -like "x64") {
        New-HomeStructure -Path "$HomePath" -Name "newrelichome_x64"
        Copy-ExtensionsInstrumentation -Destination "$HomePath\newrelichome_x64" -Extensions $net35WrapperHash
        Copy-ExtensionsStorage -Destination "$HomePath\newrelichome_x64" -Extensions $net35StorageArray
        Copy-ExtensionsOther -RootDirectory "$rootDirectory" -Destination "$HomePath\newrelichome_x64" -Configuration "$Configuration" -Type "Framework"
        Copy-AgentRoot  -RootDirectory "$rootDirectory" -Destination "$HomePath\newrelichome_x64" -Configuration "$Configuration" -Type "Framework" -Architecture "x64"

        if (-Not $KeepNewRelicConfig) {
            Copy-NewRelicConfig -RootDirectory "$rootDirectory" -Destination "$HomePath\newrelichome_x64\"
        }
    }

    if ($Architecture -like "All" -or $Architecture -like "x86") {
        New-HomeStructure -Path "$HomePath" -Name "newrelichome_x86"
        Copy-ExtensionsInstrumentation -Destination "$HomePath\newrelichome_x86" -Extensions $net35WrapperHash
        Copy-ExtensionsStorage -Destination "$HomePath\newrelichome_x86" -Extensions $net35StorageArray
        Copy-ExtensionsOther -RootDirectory "$rootDirectory" -Destination "$HomePath\newrelichome_x86" -Configuration "$Configuration" -Type "Framework"
        Copy-AgentRoot  -RootDirectory "$rootDirectory" -Destination "$HomePath\newrelichome_x86" -Configuration "$Configuration" -Type "Framework" -Architecture "x86"

        if (-Not $KeepNewRelicConfig) {
            Copy-NewRelicConfig -RootDirectory "$rootDirectory" -Destination "$HomePath\newrelichome_x86\"
        }
    }
}

if ($Type -like "All" -or $Type -like "Core") {
    if ($Architecture -like "All" -or $Architecture -like "x64") {
        New-HomeStructure -Path "$HomePath" -Name "newrelichome_x64_coreclr"
        Copy-ExtensionsInstrumentation -Destination "$HomePath\newrelichome_x64_coreclr" -Extensions $netstandard20WrapperHash
        Copy-ExtensionsStorage -Destination "$HomePath\newrelichome_x64_coreclr" -Extensions $netstandard20StorageArray
        Copy-ExtensionsOther -RootDirectory "$rootDirectory" -Destination "$HomePath\newrelichome_x64_coreclr" -Configuration "$Configuration" -Type "Core"
        Copy-AgentRoot  -RootDirectory "$rootDirectory" -Destination "$HomePath\newrelichome_x64_coreclr" -Configuration "$Configuration" -Type "Core" -Architecture "x64"
        Copy-AgentApi  -RootDirectory "$rootDirectory" -Destination "$HomePath\newrelichome_x64_coreclr" -Configuration "$Configuration" -Type "Core"

        if (-Not $KeepNewRelicConfig) {
            Copy-NewRelicConfig -RootDirectory "$rootDirectory" -Destination "$HomePath\newrelichome_x64_coreclr\"
        }
    }

    if ($Architecture -like "All" -or $Architecture -like "x86") {
        New-HomeStructure -Path "$HomePath" -Name "newrelichome_x86_coreclr"
        Copy-ExtensionsInstrumentation -Destination "$HomePath\newrelichome_x86_coreclr" -Extensions $netstandard20WrapperHash
        Copy-ExtensionsStorage -Destination "$HomePath\newrelichome_x86_coreclr" -Extensions $netstandard20StorageArray
        Copy-ExtensionsOther -RootDirectory "$rootDirectory" -Destination "$HomePath\newrelichome_x86_coreclr" -Configuration "$Configuration" -Type "Core"
        Copy-AgentRoot  -RootDirectory "$rootDirectory" -Destination "$HomePath\newrelichome_x86_coreclr" -Configuration "$Configuration" -Type "Core" -Architecture "x86"
        Copy-AgentApi  -RootDirectory "$rootDirectory" -Destination "$HomePath\newrelichome_x86_coreclr" -Configuration "$Configuration" -Type "Core"

        if (-Not $KeepNewRelicConfig) {
            Copy-NewRelicConfig -RootDirectory "$rootDirectory" -Destination "$HomePath\newrelichome_x86_coreclr\"
        }
    }
}

##################
# Setup env vars #
##################

if ($SetSystemEnvironment) {
    Set-SystemEnvironment -Type "$Type" -Architecture "$Architecture" -HomePath "$HomePath"
}

if ($SetSessionEnvironment) {
    Set-SessionEnvironment -Type "$Type" -Architecture "$Architecture" -HomePath "$HomePath"
}