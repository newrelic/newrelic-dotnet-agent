# Standard License Header
# Bash script to build and push Docker images to Azure Container Registry (ACR)
# Usage: ./build_and_push_acr.sh <ACR_NAME> <RESOURCE_GROUP> <AZURE_SUBSCRIPTION_ID>

set -e

ACR_NAME=$1
RESOURCE_GROUP=$2
SUBSCRIPTION_ID=$3

az account set --subscription "$SUBSCRIPTION_ID"
LOGIN_SERVER=$(az acr show --name "$ACR_NAME" --resource-group "$RESOURCE_GROUP" --query loginServer --output tsv)
az acr login --name "$ACR_NAME"

services=(rabbitmq mongodb32 mongodb60 redis couchbase postgres mssql mysql)

for service in "${services[@]}"; do
    imageName="$LOGIN_SERVER/unbounded-$service:latest"
    docker build -t "$imageName" "./$service"
    docker push "$imageName"
done
