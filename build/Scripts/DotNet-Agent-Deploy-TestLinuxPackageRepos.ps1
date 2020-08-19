############################################################
# Copyright 2020 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0
############################################################

Param(
    [Parameter(Mandatory=$true)][string] $version,
    [Parameter(Mandatory=$true)][string] $aptRepoUrl,
    [Parameter(Mandatory=$true)][string] $yumRepoUrl
)

$ErrorActionPreference = "Stop"

Write-Host "version=$version"
Write-Host "aptRepoUrl=$aptRepoUrl"
Write-Host "yumRepoUrl=$yumRepoUrl"

docker-compose build
docker-compose run test_debian bash -c "/test/repo_tests/test_apt_repo_install.sh $aptRepoUrl $version"
docker-compose run test_centos bash -c "/test/repo_tests/test_yum_repo_install.sh $yumRepoUrl $version"
