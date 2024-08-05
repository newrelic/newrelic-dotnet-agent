############################################################
# Copyright 2020 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0
############################################################

Write-Host ""
Write-Host "********"
Write-Host "Build Linux profiler shared object (.so)"
Write-Host "********"

$baseProfilerPath = (Get-Item (Split-Path $script:MyInvocation.MyCommand.Path)).parent.parent.FullName
Push-Location "$baseProfilerPath"

Write-Host "docker compose build build"
docker compose build build

Write-Host "docker compose run build"
docker compose run build

if ($LastExitCode -ne 0) {
    exit $LastExitCode
}

Write-Host ""
Write-Host "********"
Write-Host "Clean up old containers"
Write-Host "********"

Write-Host "Cleaning up old containers"
Write-Host 'Running command: docker container prune --force --filter "until=60m"'
docker container prune --force --filter "until=60m"

Pop-Location

exit $LastExitCode