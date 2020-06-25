############################################################
# Copyright 2020 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0
############################################################

# This script needs to be run on a machine that can access the supplied symbol store path parameter

Param(
    [Parameter(Mandatory=$true)][string] $symbolStorePath
)

$ErrorActionPreference = "Stop"

$msi= Get-ChildItem -Path "$ENV:WORKSPACE\src\_build\x64-Release\Installer\*.msi"
$version = $msi.Name.TrimStart('NewRelicAgent_x64_').TrimEnd('.msi')
Set-Location "C:\Program Files (x86)\Windows Kits\10\Debuggers\x64"
Write-Host "Uploading $version"
.\symstore add /r /s "$symbolStorePath" /f "$ENV:WORKSPACE\*.pdb" /t "New Relic .NET Agent" /v "$version" /o
.\symstore add /r /s "$symbolStorePath" /f "$ENV:WORKSPACE\*.dll" /t "New Relic .NET Agent" /v "$version" /o
