#!/bin/bash
set -e # Exit script on any error

if [ -z "$AGENT_VERSION" ]; then
    echo "AGENT_VERSION is not set"
    exit -1
fi

if [ -z "$S3_BUCKET" ]; then
    echo "S3_BUCKET is not set"
    exit -1
fi

if [ -z "$AWS_ACCESS_KEY_ID" ]; then
    echo "AWS_ACCESS_KEY_ID is not set"
    exit -1
fi

if [ -z "$AWS_SECRET_ACCESS_KEY" ]; then
    echo "AWS_SECRET_ACCESS_KEY is not set"
    exit -1
fi

AWS_DEFAULT_REGION=${AWS_DEFAULT_REGION:-us-west-2}
AWS_DEFAULT_OUTPUT=${AWS_DEFAULT_OUTPUT:-text}


## Actions
# 'release' => release new packages
# 'rollback' => roll back/remove an existing set of packages

ACTION=${ACTION:-release}
S3_BUCKET=${S3_BUCKET:-$}

/deployscripts/deploy-packages.bash -p /data/s3 -i /packages -a "$ACTION" -s "$S3_BUCKET" -v "$AGENT_VERSION"