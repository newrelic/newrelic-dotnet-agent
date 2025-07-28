#!/bin/bash

# Copyright 2020 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0
# Bash script to build and push Docker images to Azure Container Registry (ACR)
# Usage: ./build_and_push_acr.sh <ACR_NAME> <RESOURCE_GROUP> [service1 service2 ...]

set -e

ACR_NAME=$1
RESOURCE_GROUP=$2
shift 2

LOGIN_SERVER=$(az acr show --name "$ACR_NAME" --resource-group "$RESOURCE_GROUP" --query loginServer --output tsv)
az acr login --name "$ACR_NAME"

# Default list of all services
all_services=(rabbitmq mongodb32 mongodb60 redis couchbase postgres mssql mysql elastic9 elastic8 elastic7 oracle)

# If specific services are provided as arguments, use those instead
if [ $# -gt 0 ]; then
    services=("$@")
    echo "Building only specified services: ${services[*]}"
else
    services=("${all_services[@]}")
    echo "Building all services: ${services[*]}"
fi

for service in "${services[@]}"; do
    echo "Building and pushing $service..."
    imageName="$LOGIN_SERVER/$service:latest"
    docker build -t "$imageName" "$(dirname "$0")/$service"
    docker push "$imageName"
done
