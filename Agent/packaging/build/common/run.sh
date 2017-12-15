#!/bin/bash

# This script can be used to run a dotnet application with New Relic monitoring.

CORECLR_NEWRELIC_HOME=${CORECLR_NEWRELIC_HOME:-/usr/local/newrelic-netcore20-agent} CORECLR_ENABLE_PROFILING=1 CORECLR_PROFILER={36032161-FFC0-4B61-B559-F6C5D41BAE5A} CORECLR_PROFILER_PATH=$CORECLR_NEWRELIC_HOME/libNewRelicProfiler.so $@
