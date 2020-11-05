#!/bin/bash
set -e # for initial debugging purposes, halt the build script on any error.  This should be removed before release, probably

if [ -z "$AGENT_VERSION" ]; then
    echo "AGENT_VERSION is not set"
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