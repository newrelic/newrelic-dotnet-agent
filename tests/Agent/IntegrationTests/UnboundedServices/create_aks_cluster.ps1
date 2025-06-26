# Copyright 2020 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0

# PowerShell script to create an Azure Kubernetes Service (AKS) cluster with a managed identity and public IP

param(
    [Parameter(Mandatory = $true)]
    [string]$resourceGroup,
    [Parameter(Mandatory = $true)]
    [string]$acrName,
    [Parameter(Mandatory = $true)]
    [string]$managedIdentity,
    [Parameter(Mandatory = $true)]
    [string]$aksName,
    [Parameter(Mandatory = $true)]
    [string]$publicIpName,
    [Parameter(Mandatory = $true)]
    [string]$publicIpDnsName
)

# Login if not already logged in
az account show 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Output "Logging in to Azure..."
    az login
}

# Get the current Azure subscription ID and store it in a variable
$subscriptionId = az account show --query id -o tsv
 
# create a managed identity for working with unbounded services and the acr
az identity create --name $managedIdentity --resource-group $resourceGroup

$managedIdentityClientId = az identity show --name $managedIdentity --resource-group $resourceGroup --query clientId -o tsv
$managedIdentityResourceId = az identity show --name $managedIdentity --resource-group $resourceGroup --query id -o tsv
Write-Output "Managed Identity Client ID: $managedIdentityClientId"
Write-Output "Managed Identity Resource ID: $managedIdentityResourceId"

# Create a federated credential for the managed identity to allow GitHub Actions in the integration-test environment to authenticate
Write-Output "Creating federated credential for integration-test environment..."
az identity federated-credential create --name "github-unbounded-services-integration-test" --identity-name $managedIdentity --resource-group $resourceGroup --issuer "https://token.actions.githubusercontent.com" --subject "repo:newrelic/newrelic-dotnet-agent:environment:integration-test" --audiences "api://AzureADTokenExchange"

# Create the Azure Container Registry
az acr create --resource-group $resourceGroup --name $acrName --sku Standard --admin-enabled true

# assign ACR roles to the managed identity
$registryId = az acr show --name $acrName --resource-group $resourceGroup --query id --output tsv
Write-Output "Assigning ACR roles to managed identity..."

# AcrPush role allows pushing images
az role assignment create --assignee $managedIdentityClientId --scope $registryId --role AcrPush

# AcrPull role allows pulling images
az role assignment create --assignee $managedIdentityClientId --scope $registryId --role AcrPull

# Reader role allows reading registry properties
az role assignment create --assignee $managedIdentityClientId --scope $registryId --role Reader

$acrLoginServer = az acr show --name $acrName --resource-group $resourceGroup --query loginServer --output tsv
Write-Output "ACR Login Server: $acrLoginServer"

# make sure the subscription has access to the Microsft.ContainerService resource provider
az provider register --namespace Microsoft.ContainerService

# Create the AKS cluster - Standard_A4m_v2 is 4 vCPUs and 32 GB RAM, which is sufficient for running the unbounded services
az aks create --resource-group $resourceGroup --name $aksName --node-count 1 --enable-managed-identity --assign-identity $managedIdentityResourceId --generate-ssh-keys --node-vm-size Standard_A4m_v2

# give the cluster permission to access the ACR
az aks update -n $aksName -g $resourceGroup --attach-acr $acrName

# Get AKS resource ID
$aksResourceId = az aks show --name $aksName --resource-group $resourceGroup --query id --output tsv
Write-Output "AKS Resource ID: $aksResourceId"

# Assign required AKS roles
Write-Output "Assigning AKS cluster roles to managed identity..."

# Azure Kubernetes Service Cluster User Role - allows listing cluster user credentials
Write-Output "Assigning Azure Kubernetes Service Cluster User Role..."
az role assignment create --assignee $managedIdentityClientId --scope $aksResourceId --role "Azure Kubernetes Service Cluster User Role"

# Azure Kubernetes Service Cluster Admin Role - allows full access to the cluster
Write-Output "Assigning Azure Kubernetes Service Cluster Admin Role..."
az role assignment create --assignee $managedIdentityClientId --scope $aksResourceId --role "Azure Kubernetes Service Cluster Admin Role"

# Reader role on the AKS resource
Write-Output "Assigning Reader role on the AKS resource..."
az role assignment create --assignee $managedIdentityClientId --scope $aksResourceId --role "Reader"

# create a public IP
az network public-ip create --resource-group $resourceGroup --name $publicIpName --sku Standard
# get the public IP address
$publicIpAddress = az network public-ip show --resource-group $resourceGroup --name $publicIpName --query ipAddress -o json
Write-Output "Public IP address: $publicIpAddress"

# assign a dns label to the public IP
az network public-ip update --resource-group $resourceGroup --name $publicIpName --dns-name $publicIpDnsName
# get the fqdn for the public IP
$publicIpFqdn = az network public-ip show --resource-group $resourceGroup --name $publicIpName --query dnsSettings.fqdn -o tsv
Write-Output "Public IP FQDN: $publicIpFqdn"

# authorize the managed identity to use the public IP, which enables the cluster to use the public IP in load balancers
az role assignment create --assignee $managedIdentityClientId --role "Network Contributor" --scope /subscriptions/$subscriptionId/resourceGroups/$resourceGroup

Write-Output "AKS cluster $aksName created successfully with managed identity $managedIdentity and hostname $publicIpFqdn ($publicIpAddress)."