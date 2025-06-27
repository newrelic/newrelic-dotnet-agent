#!/bin/bash

# Copyright 2020 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0

# Script to deploy Kubernetes manifests to AKS with environment variable substitution
# Deploys all manifests, even if nothing has changed.

set -e

# Ensure PUBLIC_IP environment variable exists
if [ -z "$PUBLIC_IP" ]; then
    echo "Error: PUBLIC_IP environment variable is not set"
    exit 1
fi

echo "Deploying Kubernetes resources with PUBLIC_IP: $PUBLIC_IP"

# Apply namespace and secrets directly (these don't need variable substitution)
echo "Applying namespace..."
kubectl apply -f k8s/namespace.yaml

echo "Applying secrets..."
kubectl apply -f k8s/unboundedservices-secrets.yaml

# Apply other manifests with envsubst for variable substitution
echo "Applying service manifests with environment variable substitution..."

for manifest in k8s/*.yaml; do
    # Skip namespace and secrets manifests which were already applied
    if [[ "$manifest" == "k8s/namespace.yaml" || "$manifest" == "k8s/unboundedservices-secrets.yaml" ]]; then
        continue
    fi
    
    echo "Applying $manifest..."
    envsubst < $manifest | kubectl apply -f -
done

echo "Deployment to AKS completed successfully!"
