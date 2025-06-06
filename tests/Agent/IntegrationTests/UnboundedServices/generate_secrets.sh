#!/usr/bin/env bash

# Set required environment variables (replace the values with your actual secrets)
# export MONGO_INITDB_ROOT_USERNAME="your_mongo_root_username"
# export MONGO_INITDB_ROOT_PASSWORD="your_mongo_root_password"
# export POSTGRES_USER="your_postgres_user"
# export POSTGRES_PASSWORD="your_postgres_password"
# export SA_PASSWORD="your_mssql_sa_password"
# export MYSQL_ROOT_PASSWORD="your_mysql_root_password"
# export COUCHBASE_ADMINISTRATOR_PASSWORD="your_couchbase_admin_password"
# export REDIS_PASSWORD="your_redis_password"
# export RABBITMQ_DEFAULT_USER="your_rabbitmq_user"
# export RABBITMQ_DEFAULT_PASS="your_rabbitmq_pass"
# export ELASTIC_PASSWORD="your_elastic_password"
# export ORACLE_PWD="your_oracle_pwd"

# Generate the Kubernetes secrets manifest
cat <<EOF > k8s/unboundedservices-secrets.yaml
# Shared Kubernetes Secret for all service credentials
apiVersion: v1
kind: Secret
metadata:
  name: unboundedservices-secrets
  namespace: unbounded-services
type: Opaque
data:
  MONGO_INITDB_ROOT_USERNAME: $(echo -n "\$MONGO_INITDB_ROOT_USERNAME" | base64)
  MONGO_INITDB_ROOT_PASSWORD: $(echo -n "\$MONGO_INITDB_ROOT_PASSWORD" | base64)
  POSTGRES_USER: $(echo -n "\$POSTGRES_USER" | base64)
  POSTGRES_PASSWORD: $(echo -n "\$POSTGRES_PASSWORD" | base64)
  SA_PASSWORD: $(echo -n "\$SA_PASSWORD" | base64)
  MYSQL_ROOT_PASSWORD: $(echo -n "\$MYSQL_ROOT_PASSWORD" | base64)
  COUCHBASE_ADMINISTRATOR_PASSWORD: $(echo -n "\$COUCHBASE_ADMINISTRATOR_PASSWORD" | base64)
  REDIS_PASSWORD: $(echo -n "\$REDIS_PASSWORD" | base64)
  RABBITMQ_DEFAULT_USER: $(echo -n "\$RABBITMQ_DEFAULT_USER" | base64)
  RABBITMQ_DEFAULT_PASS: $(echo -n "\$RABBITMQ_DEFAULT_PASS" | base64)
  ELASTIC_PASSWORD: $(echo -n "\$ELASTIC_PASSWORD" | base64)
  ORACLE_PWD: $(echo -n "\$ORACLE_PWD" | base64)
EOF