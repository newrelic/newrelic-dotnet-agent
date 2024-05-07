############################################################
# Copyright 2020 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0
############################################################

Param(
  [Parameter(Mandatory=$True)]
  [string]$outputPath
)

New-Item -ItemType Directory -Force -Path $outputPath

function getVersionFromTag([string] $tagPrefix, [switch] $excludeCommitCount) {
    $GitLatestTagVersion = git describe --tags --match "$tagPrefix[0-9]*" --abbrev=0 HEAD
    $GitGitLatestTagVersionSanitized = $GitLatestTagVersion.Replace($tagPrefix,'')
    $GitCommitCount = git rev-list "$GitLatestTagVersion..$GitBranchName" --count HEAD
    $GitVersion = "$GitGitLatestTagVersionSanitized.$GitCommitCount"

    if ($excludeCommitCount -eq $true) {
      $GitVersion = "$GitGitLatestTagVersionSanitized"
    }

    return $GitVersion
}

$GitCommitHash = git rev-parse HEAD
$GitBranchName = git rev-parse --abbrev-ref HEAD

Set-Content -Path "$outputPath\branchname.txt" -Value $GitBranchName
Set-Content -Path "$outputPath\commithash.txt" -Value $GitCommitHash
Set-Content -Path "$outputPath\version_agent.txt" -Value (getVersionFromTag "v")
Set-Content -Path "$outputPath\version_azuresiteextension.txt" -Value (getVersionFromTag "AzureSiteExtension_v" -excludeCommitCount)
