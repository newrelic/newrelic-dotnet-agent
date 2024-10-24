#!/bin/bash

if [ -z "$CORECLR_NEWRELIC_HOME" ]; && [ -z "$CORECLR_NEW_RELIC_HOME" ]; then
    echo "CORECLR_NEWRELIC_HOME is undefined"
else
    if [ -n "$CORECLR_NEWRELIC_HOME" ]; then
        NRHOME=$CORECLR_NEWRELIC_HOME
    else
        NRHOME=$CORECLR_NEW_RELIC_HOME
    fi
    
    export CORECLR_ENABLE_PROFILING=1
    export CORECLR_PROFILER={36032161-FFC0-4B61-B559-F6C5D41BAE5A}
    export CORECLR_PROFILER_PATH=$NR_HOME/libNewRelicProfiler.so
fi