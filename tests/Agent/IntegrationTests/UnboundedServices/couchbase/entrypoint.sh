#!/bin/bash
# Set open files limit for Couchbase
ulimit -n 200000
# Exec the original Couchbase entrypoint (assumes official image as base)
exec /entrypoint.sh "$@"
