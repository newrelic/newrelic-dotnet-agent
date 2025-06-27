# Copyright 2020 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0

# PowerShell script to build and push Docker images to Azure Container Registry (ACR)
# Usage: .\build_and_push_acr.ps1 <ACR_NAME> <RESOURCE_GROUP>

param(
    [Parameter(Mandatory=$true)]
    [string]$AcrName,
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroup,
    [Parameter(Mandatory=$false)]
    [string]$ServicesToBuild
)

$ErrorActionPreference = 'Stop'

# Login if not already logged in
$loginStatus = az account show 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Output "Logging in to Azure..."
    az login
}

# Get the login server for the ACR
$loginServer = az acr show --name $AcrName --resource-group $ResourceGroup --query loginServer --output tsv
az acr login --name $AcrName

$allServices = @('couchbase', 'elastic', 'elastic7', 'mongodb32', 'mongodb60', 'mssql', 'mysql', 'oracle', 'postgres', 'rabbitmq', 'redis')

if ($ServicesToBuild) {
    $services = $ServicesToBuild.Split(",") | ForEach-Object { $_.Trim() }
} else {
    $services = $allServices
}

foreach ($service in $services) {
    Write-Host "$loginServer/${service}:latest"
    $imageName = "$loginServer/${service}:latest"
    $servicePath = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) $service
    Write-Host "Building and pushing image for $service with tag $imageName and build context path $servicePath ..."
    docker build -t $imageName $servicePath
    docker push $imageName
}
