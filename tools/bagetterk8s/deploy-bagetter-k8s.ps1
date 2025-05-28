# Copyright 2020 New Relic, Inc. All rights reserved.
# SPDX-License-Identifier: Apache-2.0

# This script deploys the BaGet + MySQL manifests to AKS
param(
    [string]$ResourceGroup = "bagetter-rg",
    [string]$AksName = "bagetter-aks"
)

# Get AKS credentials
az aks get-credentials --resource-group $ResourceGroup --name $AksName

# Deploy manifests
kubectl apply -f namespace-bagetter.yaml
kubectl apply -f bagetter-secrets.yaml
kubectl apply -f mysql.yaml
kubectl apply -f bagetter.yaml

Write-Host "BaGetter + MySQL deployed to AKS."
