#!/bin/bash

# Copyright 2020 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0
# Bash script to build and push Docker images to Azure Container Registry (ACR)
# Usage: ./build_and_push_acr.sh <ACR_NAME> <RESOURCE_GROUP>

set -e

ACR_NAME=$1
RESOURCE_GROUP=$2

LOGIN_SERVER=$(az acr show --name "$ACR_NAME" --resource-group "$RESOURCE_GROUP" --query loginServer --output tsv)
az acr login --name "$ACR_NAME"

services=(rabbitmq mongodb32 mongodb60 redis couchbase postgres mssql mysql elastic elastic7 oracle)

for service in "${services[@]}"; do
    imageName="$LOGIN_SERVER/$service:latest"
    docker build -t "$imageName" "$(dirname "$0")/$service"
    docker push "$imageName"
done
