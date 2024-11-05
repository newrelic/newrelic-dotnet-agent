#!/bin/sh

set -m
 
/entrypoint.sh couchbase-server &
 
sleep 20
 
echo "Creating node" 
curl http://127.0.0.1:8091/pools/default -d memoryQuota=512 -d indexMemoryQuota=512 -d ftsMemoryQuota=512
 
echo "Setting up services"
curl http://127.0.0.1:8091/node/controller/setupServices -d services=kv%2cn1ql%2Cindex%2cfts
 
echo "Setting admin user and password" 
curl http://127.0.0.1:8091/settings/web -d port=8091 -d username=$COUCHBASE_ADMINISTRATOR_USERNAME -d password=$COUCHBASE_ADMINISTRATOR_PASSWORD
 
echo "Setting index storge mode" 
curl -u $COUCHBASE_ADMINISTRATOR_USERNAME:$COUCHBASE_ADMINISTRATOR_PASSWORD -X POST http://127.0.0.1:8091/settings/indexes -d 'storageMode=memory_optimized'

# Check to see if the travel-sample bucket already exists and if it does, skip the rest of setup

travelSampleBucketNotInstalled=$(curl -u $COUCHBASE_ADMINISTRATOR_USERNAME:$COUCHBASE_ADMINISTRATOR_PASSWORD http://127.0.0.1:8091/pools/default/buckets/travel-sample |grep -c "Requested resource not found")

if [ "$travelSampleBucketNotInstalled" -eq 1 ]; then

    echo "Installing travel-sample bucket" 
    curl -X POST -u $COUCHBASE_ADMINISTRATOR_USERNAME:$COUCHBASE_ADMINISTRATOR_PASSWORD http://127.0.0.1:8091/sampleBuckets/install -d '["travel-sample"]'
 
    sleep 15

    # Get UUID of travel-sample bucket
    uuid=$(curl -u $COUCHBASE_ADMINISTRATOR_USERNAME:$COUCHBASE_ADMINISTRATOR_PASSWORD http://127.0.0.1:8091/pools/default/buckets/travel-sample |jq '. | .uuid ')

    echo "Create a full text search index on travel-sample"
    curl -X PUT -u $COUCHBASE_ADMINISTRATOR_USERNAME:$COUCHBASE_ADMINISTRATOR_PASSWORD -H "Content-Type: application/json" \
    http://localhost:8094/api/index/idx_travel_content \
    -d '{
    "type": "fulltext-index",
    "name": "idx_travel_content",
    "sourceType": "couchbase",
    "sourceName": "travel-sample",
    "sourceUUID": '${uuid}',
    "planParams": {
        "maxPartitionsPerPIndex": 32,
        "numReplicas": 0,
        "hierarchyRules": null,
        "nodePlanParams": null,
        "pindexWeights": null,
        "planFrozen": false
    },
    "params": {
        "mapping": {
        "byte_array_converter": "json",
        "default_analyzer": "standard",
        "default_datetime_parser": "dateTimeOptional",
        "default_field": "_all",
        "default_mapping": {
            "display_order": "0",
            "dynamic": true,
            "enabled": true
        },
        "default_type": "_default",
        "index_dynamic": true,
        "store_dynamic": false,
        "type_field": "type"
        },
        "store": {
        "kvStoreName": "forestdb"
        }
    },
    "sourceParams": {
        "clusterManagerBackoffFactor": 0,
        "clusterManagerSleepInitMS": 0,
        "clusterManagerSleepMaxMS": 2000,
        "dataManagerBackoffFactor": 0,
        "dataManagerSleepInitMS": 0,
        "dataManagerSleepMaxMS": 2000,
        "feedBufferAckThreshold": 0,
        "feedBufferSizeBytes": 0
    }
    }'
else
    echo "travel-sample bucket already installed"
fi 

fg 1
echo "Couchbase server is ready"