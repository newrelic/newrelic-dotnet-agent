# Copyright 2020 New Relic, Inc. All rights reserved.
# SPDX-License-Identifier: Apache-2.0

# This script creates Azure resources for a BaGetter + MySQL + Azure Blob Storage AKS deployment
param(
    [string]$ResourceGroup = "bagetter-rg",
    [string]$Location = "westus2",
    [string]$AksName = "bagetter-aks",
    [string]$StorageAccountName = "bagetterstorage"
)

# Create resource group
az group create --name $ResourceGroup --location $Location

# Create AKS cluster (no ACR attached)
az aks create --resource-group $ResourceGroup --name $AksName --node-count 1 --generate-ssh-keys

# Create Azure Storage Account for BaGet blob storage
az storage account create --name $StorageAccountName --resource-group $ResourceGroup --location $Location --sku Standard_LRS

# Get storage account connection string
$StorageConnectionString = az storage account show-connection-string --name $StorageAccountName --resource-group $ResourceGroup --query connectionString -o tsv

Write-Host "Azure resources created. Storage connection string: $StorageConnectionString"
Write-Host "Deploy your manifests to AKS. Update your secrets or manifests with the storage connection string."