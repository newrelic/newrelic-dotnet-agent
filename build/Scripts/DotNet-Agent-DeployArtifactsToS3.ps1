$workingDir = "working_dir"
$latestReleaseDir= "latest_release"
$previousReleaseDir= "previous_releases"
$versionDir = "$env:Version"
$bucketName = "fake-downloads-main"
$profileName = "test-download-site"

$target = $env:Target
if ( $target -eq "Production" ) {
    $bucketName = "nr-downloads-main"
    $profileName = "default"
}

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

New-Item -Force -ItemType directory -Path .\$previousReleaseDir\$versionDir
Copy-Item .\$workingDir\* ".\$previousReleaseDir\$versionDir\" -Force -Recurse

Push-Location .\$previousReleaseDir\$versionDir
aws s3 sync . s3://$bucketName/dot_net_agent/$previousReleaseDir/$versionDir/ --include "*" --exclude ".DS_Store" --delete --profile $profileName
Pop-Location