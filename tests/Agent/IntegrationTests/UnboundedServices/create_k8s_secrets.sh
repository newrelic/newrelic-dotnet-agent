#!/bin/bash

# Copyright 2020 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0

# Script to extract database credentials from JSON configuration and create Kubernetes secret

set -e

# Function to check if a variable is empty or null
check_variable() {
    if [ -z "$1" ] || [ "$1" == "null" ]; then
        echo "Error: $2 is empty or null. Check your TEST_SECRETS JSON."
        exit 1
    fi
}

# Check if TEST_SECRETS environment variable exists
if [ -z "$TEST_SECRETS" ]; then
    echo "Error: TEST_SECRETS environment variable is not set"
    exit 1
fi

# Extract values from JSON in TEST_SECRETS
echo "$TEST_SECRETS" > test_secrets.json

# Extract database credentials from the JSON file
MONGO_USER=$(jq -r '.IntegrationTestConfiguration.TestSettingOverrides.MongoDBTests.CustomSettings.ConnectionString' test_secrets.json | sed -E 's/mongodb:\/\/([^:]+):.+/\1/')
check_variable "$MONGO_USER" "MONGO_USER"
MONGO_PASSWORD=$(jq -r '.IntegrationTestConfiguration.TestSettingOverrides.MongoDBTests.CustomSettings.ConnectionString' test_secrets.json | sed -E 's/mongodb:\/\/[^:]+:([^@]+)@.+/\1/')
check_variable "$MONGO_PASSWORD" "MONGO_PASSWORD"

POSTGRES_USER=$(jq -r '.IntegrationTestConfiguration.TestSettingOverrides.PostgresTests.CustomSettings.ConnectionString' test_secrets.json | grep -o 'User Id=[^;]*' | sed 's/User Id=//')
check_variable "$POSTGRES_USER" "POSTGRES_USER"
POSTGRES_PASSWORD=$(jq -r '.IntegrationTestConfiguration.TestSettingOverrides.PostgresTests.CustomSettings.ConnectionString' test_secrets.json | grep -o 'Password=[^;]*' | sed 's/Password=//')
check_variable "$POSTGRES_PASSWORD" "POSTGRES_PASSWORD"

MSSQL_SA_PASSWORD=$(jq -r '.IntegrationTestConfiguration.TestSettingOverrides.MSSQLTests.CustomSettings.ConnectionString' test_secrets.json | grep -o 'Password=[^;]*' | sed 's/Password=//')
check_variable "$MSSQL_SA_PASSWORD" "MSSQL_SA_PASSWORD"

MYSQL_ROOT_PASSWORD=$(jq -r '.IntegrationTestConfiguration.TestSettingOverrides.MySQLTests.CustomSettings.ConnectionString' test_secrets.json | grep -o 'Password=[^;]*' | sed 's/Password=//')
check_variable "$MYSQL_ROOT_PASSWORD" "MYSQL_ROOT_PASSWORD"

COUCHBASE_ADMINISTRATOR_PASSWORD=$(jq -r '.IntegrationTestConfiguration.TestSettingOverrides.CouchbaseTests.CustomSettings.Password' test_secrets.json)
check_variable "$COUCHBASE_ADMINISTRATOR_PASSWORD" "COUCHBASE_ADMINISTRATOR_PASSWORD"

REDIS_PASSWORD=$(jq -r '.IntegrationTestConfiguration.TestSettingOverrides.StackExchangeRedisTests.CustomSettings.Password' test_secrets.json)
check_variable "$REDIS_PASSWORD" "REDIS_PASSWORD"

RABBITMQ_DEFAULT_USER=$(jq -r '.IntegrationTestConfiguration.TestSettingOverrides.RabbitMqTests.CustomSettings.Username' test_secrets.json)
check_variable "$RABBITMQ_DEFAULT_USER" "RABBITMQ_DEFAULT_USER"
RABBITMQ_DEFAULT_PASS=$(jq -r '.IntegrationTestConfiguration.TestSettingOverrides.RabbitMqTests.CustomSettings.Password' test_secrets.json)
check_variable "$RABBITMQ_DEFAULT_PASS" "RABBITMQ_DEFAULT_PASS"

ELASTIC_PASSWORD=$(jq -r '.IntegrationTestConfiguration.TestSettingOverrides.ElasticSearch9Tests.CustomSettings.Password' test_secrets.json)
check_variable "$ELASTIC_PASSWORD" "ELASTIC_PASSWORD"

ORACLE_PWD=$(jq -r '.IntegrationTestConfiguration.TestSettingOverrides.OracleTests.CustomSettings.ConnectionString' test_secrets.json | grep -o 'Password=[^;]*' | sed 's/Password=//')
check_variable "$ORACLE_PWD" "ORACLE_PWD"


# Echo values of extracted credentials (unredacted for testing purposes)
# uncomment for local testing
# echo "--- Extracted Credentials (TESTING - SHOWING ALL VALUES) ---"
# echo "MongoDB User: $MONGO_USER"
# echo "MongoDB Password: $MONGO_PASSWORD"
# echo "Postgres User: $POSTGRES_USER"
# echo "Postgres Password: $POSTGRES_PASSWORD"
# echo "MSSQL SA Password: $MSSQL_SA_PASSWORD"
# echo "MySQL Root Password: $MYSQL_ROOT_PASSWORD"
# echo "Couchbase Admin Password: $COUCHBASE_ADMINISTRATOR_PASSWORD"
# echo "Redis Password: $REDIS_PASSWORD"
# echo "RabbitMQ User: $RABBITMQ_DEFAULT_USER"
# echo "RabbitMQ Password: $RABBITMQ_DEFAULT_PASS"
# echo "Elastic Password: $ELASTIC_PASSWORD"
# echo "Oracle Password: $ORACLE_PWD"
# echo "-------------------------------------------------------"

# Remove the temporary file for security
rm test_secrets.json

# Create the Kubernetes Secret manifest
cat <<EOF > k8s/unboundedservices-secrets.yaml
# Shared Kubernetes Secret for all service credentials
apiVersion: v1
kind: Secret
metadata:
  name: unboundedservices-secrets
  namespace: unbounded-services
type: Opaque
data:
  MONGO_INITDB_ROOT_USERNAME: $(echo -n "$MONGO_USER" | base64)
  MONGO_INITDB_ROOT_PASSWORD: $(echo -n "$MONGO_PASSWORD" | base64)
  POSTGRES_USER: $(echo -n "$POSTGRES_USER" | base64)
  POSTGRES_PASSWORD: $(echo -n "$POSTGRES_PASSWORD" | base64)
  MSSQL_SA_PASSWORD: $(echo -n "$MSSQL_SA_PASSWORD" | base64)
  MYSQL_ROOT_PASSWORD: $(echo -n "$MYSQL_ROOT_PASSWORD" | base64)
  COUCHBASE_ADMINISTRATOR_PASSWORD: $(echo -n "$COUCHBASE_ADMINISTRATOR_PASSWORD" | base64)
  REDIS_PASSWORD: $(echo -n "$REDIS_PASSWORD" | base64)
  RABBITMQ_DEFAULT_USER: $(echo -n "$RABBITMQ_DEFAULT_USER" | base64)
  RABBITMQ_DEFAULT_PASS: $(echo -n "$RABBITMQ_DEFAULT_PASS" | base64)
  ELASTIC_PASSWORD: $(echo -n "$ELASTIC_PASSWORD" | base64)
  ORACLE_PWD: $(echo -n "$ORACLE_PWD" | base64)
EOF

echo "Kubernetes Secret manifest created successfully at k8s/unboundedservices-secrets.yaml"
