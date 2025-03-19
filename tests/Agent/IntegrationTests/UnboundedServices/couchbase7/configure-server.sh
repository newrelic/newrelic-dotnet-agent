#!/bin/sh
set -e 
/entrypoint.sh couchbase-server &

if [ ! -f /nr-container-configured ]; then

  apt-get update && apt-get install -y jq

  # wait for the server to be up and running
  # when the file /opt/couchbase/var/lib/couchbase/container-configured exists, the server is ready
  while [ ! -f /opt/couchbase/var/lib/couchbase/container-configured ]; do
    sleep 1
  done

  echo "Waiting for the server to be ready"
  sleep 15s

  # use the couchbase cli to change the administrator password
  echo "Changing administrator password to ${COUCHBASE_ADMINISTRATOR_PASSWORD}"
  /opt/couchbase/bin/couchbase-cli reset-admin-password --new-password ${COUCHBASE_ADMINISTRATOR_PASSWORD} || { echo "Error: Failed to reset administrator password"; exit 1; }

  # Get UUID of travel-sample bucket
  uuid=$(curl -u Administrator:${COUCHBASE_ADMINISTRATOR_PASSWORD} http://127.0.0.1:8091/pools/default/buckets/travel-sample | jq '. | .uuid') || { echo "Error: Failed to retrieve UUID"; exit 1; }

  echo "Creating a full text search index on the hotel collection in the inventory scope"
                      
  curl -XPUT -H "Content-Type: application/json" -u Administrator:${COUCHBASE_ADMINISTRATOR_PASSWORD} \
  http://localhost:8094/api/bucket/travel-sample/scope/inventory/index/index-hotel-description \
  -d '{
  "name": "index-hotel-description",
  "type": "fulltext-index",
  "params": {
    "mapping": {
    "types": {
      "inventory.hotel": {
      "dynamic": true,
      "enabled": true
      }
    },
    "default_mapping": {
      "enabled": false,
      "dynamic": true
    },
    "default_type": "_default",
    "default_analyzer": "standard",
    "default_datetime_parser": "dateTimeOptional",
    "default_field": "_all",
    "store_dynamic": false,
    "index_dynamic": true,
    "docvalues_dynamic": false
    },
    "store": {
    "indexType": "scorch",
    "kvStoreName": ""
    },
    "doc_config": {
    "docid_prefix_delim": "",
    "docid_regexp": "",
    "mode": "scope.collection.type_field",
    "type_field": "type"
    }
  },
  "sourceType": "couchbase",
  "sourceName": "travel-sample",
  "sourceUUID": '${uuid}',
  "sourceParams": {},
  "planParams": {
    "maxPartitionsPerPIndex": 1024,
    "numReplicas": 0,
    "indexPartitions": 1
  },
  "uuid": ""
  }' || { echo "Error: Failed to create full text search index"; exit 1; }

  touch /nr-container-configured
fi

echo "Couchbase server is running"

# required to keep the container running
tail -f /dev/null