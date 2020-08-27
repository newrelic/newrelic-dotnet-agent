############################################################
# Copyright 2020 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0
############################################################

Param(
    [Parameter(Mandatory=$true,
    ParameterSetName="Start")]
    [Switch]
    $Start,

    [Parameter(Mandatory=$true,
    ParameterSetName="Stop")]
    [Switch]
    $Stop
)

Function StartUnboundedServices([string] $scriptPath) {
    Push-Location "$scriptPath"
    Write-Host "Launching docker services"
    docker-compose up -d
    Write-Host "Waiting for services to be ready"
    sleep 30 #TODO: something smarter than this
    Pop-Location
}

Function StopUnboundedServices([string] $scriptPath) {
    Push-Location "$scriptPath"
    docker-compose down
    Pop-Location
}

$scriptPath = Resolve-Path "$(Split-Path -Parent $PSCommandPath)"

if ($Start) {
    StartUnboundedServices($scriptPath)
} elseif ($Stop) {
    StopUnboundedServices($scriptPath)
}
