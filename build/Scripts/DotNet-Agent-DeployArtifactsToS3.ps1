############################################################
# Copyright 2020 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0
############################################################

Param(
    [Parameter(Mandatory=$true)][string] $version,
    [Parameter(Mandatory=$true)][string] $bucketName,
    [Parameter(Mandatory=$true)][string] $profileName
)

$workingDir = "working_dir"
$latestReleaseDir= "latest_release"
$previousReleaseDir= "previous_releases"

###
### Copy files to latest_release and push to S3
###

New-Item -ItemType directory -Path .\$latestReleaseDir -Force
Copy-Item .\$workingDir\* .\$latestReleaseDir\ -Force -Recurse

Push-Location .\$latestReleaseDir
aws s3 sync . s3://$bucketName/dot_net_agent/$latestReleaseDir/ --include "*" --exclude ".DS_Store" --delete --profile $profileName
Pop-Location

###
### Create a versioned directory in /previous_versions/ and copy release files and push to S3
###

New-Item -Force -ItemType directory -Path .\$previousReleaseDir\$version
Copy-Item .\$workingDir\* ".\$previousReleaseDir\$version\" -Force -Recurse

Push-Location .\$previousReleaseDir\$version
aws s3 sync . s3://$bucketName/dot_net_agent/$previousReleaseDir/$version/ --include "*" --exclude ".DS_Store" --delete --profile $profileName
Pop-Location