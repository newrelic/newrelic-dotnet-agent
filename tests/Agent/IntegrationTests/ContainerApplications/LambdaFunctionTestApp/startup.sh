#!/bin/bash

# create the pid file that tells the container application that we're up and running
touch /app/logs/containerizedapp.pid

# launch the "real" entrypoint script
source /lambda-entrypoint.sh "$@"
