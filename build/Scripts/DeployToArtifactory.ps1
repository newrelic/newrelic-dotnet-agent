############################################################
# Copyright 2020 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0
############################################################

param(
    [Parameter(Mandatory=$true)][string] $user,
    [Parameter(Mandatory=$true)][string] $password,
    [Parameter(Mandatory=$true)][string] $url,
    [Parameter(Mandatory=$true)][string] $productName,
    [Parameter(Mandatory=$true)][string] $7ZipExe,
    [Parameter(Mandatory=$true)][string] $ArtifactoryExe
)

Import-Module -Name "$(Split-Path -Parent $PSCommandPath)\common" -Force

$product = Get-ProductInfo $productName

Write-Host "Uploading to Artifactory: $product"

$rt_archiveName = (Get-Date -format "yyyyMMdd") +  "_r" + $product.Version
$rt_archiveName_tar = $rt_archiveName + ".tar"
$rt_archiveName_tgz = $rt_archiveName + ".tgz"
$rt_destination = "dotnet-release/$($product.ArtifactoryRootFolder)/r$($product.Version)/"

Invoke-Expression "& $7ZipExe a -ttar $rt_archiveName_tar $($product.PathsToArchive)"
Invoke-Expression "& $7ZipExe a -tgzip $rt_archiveName_tgz $rt_archiveName_tar"

Write-Host "Running $ArtifactoryExe rt upload --url=$url --user=$user --password=$password --flat=false $rt_archiveName_tgz $rt_destination"

& $ArtifactoryExe rt upload --url=$url --user=$user --password=$password --flat=false $rt_archiveName_tgz $rt_destination
