#!/usr/bin/env bash

# Deploy a service to Azure Container Instance using a generated YAML file
# Usage: deploy-aci.sh <service> <tag> <env> <command> <ports> <memory> <cpu> <registry> <registry_username> <registry_password> <resource_group> <location> <log_analytics_workspace_id> <log_analytics_workspace_key>

SERVICE="$1"
TAG="$2"
ENV_INPUT="$3"
COMMAND_INPUT="$4"
PORTS_INPUT="$5"
MEMORY="$6"
CPU="$7"
REGISTRY="$8"
REGISTRY_USERNAME="$9"
REGISTRY_PASSWORD="${10}"
RESOURCE_GROUP="${11}"
LOCATION="${12}"
LOG_ANALYTICS_WORKSPACE_ID="${13}"
LOG_ANALYTICS_WORKSPACE_KEY="${14}"

# Parse environment variables (space separated, quoted values)
ENV_YAML=""
if [ -n "$ENV_INPUT" ]; then
  while IFS= read -r envvar; do
    key="$(echo "$envvar" | cut -d'=' -f1)"
    value="$(echo "$envvar" | cut -d'=' -f2-)"
    ENV_YAML+="            - name: $key\n              value: $value\n"
  done < <(echo "$ENV_INPUT" | tr ' ' '\n')
fi

# Parse command-line if present
CMD_YAML=""
if [ -n "$COMMAND_INPUT" ]; then
  CMD_YAML="        command:\n          - /bin/sh\n          - -c\n          - $COMMAND_INPUT\n"
fi

# Ports
PORT_YAML=""
IP_PORTS_YAML=""
for port in $(echo "$PORTS_INPUT" | tr ',' ' '); do
  PORT_YAML+="            - port: $port\n"
  IP_PORTS_YAML+="      - protocol: tcp\n        port: $port\n"
done

# Generate DNS name label in required format
DNS_NAME_LABEL="dotnet-unboundedservices-${SERVICE}-server"

# Write YAML file with correct indentation and public IP
cat > aci-$SERVICE.yaml <<EOF
apiVersion: '2021-09-01'
location: $LOCATION
name: $SERVICE-server
properties:
  containers:
    - name: $SERVICE-server
      properties:
        image: $REGISTRY/$TAG
        resources:
          requests:
            cpu: $CPU
            memoryInGb: $MEMORY
        environmentVariables:
$(echo -e "$ENV_YAML")
$(echo -e "$CMD_YAML")
        ports:
$(echo -e "$PORT_YAML")
  osType: Linux
  imageRegistryCredentials:
    - server: $REGISTRY
      username: $REGISTRY_USERNAME
      password: $REGISTRY_PASSWORD
  restartPolicy: Always
  ipAddress:
    type: Public
    dnsNameLabel: $DNS_NAME_LABEL
    ports:
$(echo -e "$IP_PORTS_YAML")
  diagnostics:
    logAnalytics:
      workspaceId: $LOG_ANALYTICS_WORKSPACE_ID
      workspaceKey: $LOG_ANALYTICS_WORKSPACE_KEY
EOF

echo "Generated YAML for $SERVICE:"
echo "----------------------"
cat aci-$SERVICE.yaml
echo "----------------------"
az container create --resource-group "$RESOURCE_GROUP" --file aci-$SERVICE.yaml

rm -f aci-$SERVICE.yaml
