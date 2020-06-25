############################################################
# Copyright 2020 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0
############################################################

Function Expand-Zip($file, $destination)
{
    Write-Host "Expanding $file to $destination"
    $shellApplication = New-Object -com shell.application
    $zipPackage = $shellApplication.NameSpace($file)
    $destinationFolder = $shellApplication.NameSpace($destination)
    $destinationFolder.Copyhere($zipPackage.Items(),20)
}

$dir = "$env:WORKSPACE"
$file = "NewRelic.Agent.Installer.*.zip"
get-childitem -Path $dir | where-object { $_.Name -like $file } | %{ rename-item -LiteralPath $_.FullName -NewName "NewRelic.Agent.Installer.zip" }

$Destination = "$env:WORKSPACE\"
$Source = "$env:WORKSPACE\NewRelic.Agent.Installer.zip"

Expand-Zip $Source $Destination