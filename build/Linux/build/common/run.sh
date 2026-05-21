#!/bin/bash

# This script can be used to run a dotnet application with New Relic monitoring.

CORECLR_NEWRELIC_HOME="${CORECLR_NEWRELIC_HOME:-${CORECLR_NEW_RELIC_HOME:-/usr/local/newrelic-dotnet-agent}}"

case "$(uname -m)" in
    x86_64)  __nr_arch=x64 ;;
    aarch64) __nr_arch=arm64 ;;
    *)       __nr_arch="" ;;
esac
if ldd /bin/ls 2>/dev/null | grep -q musl; then
    __nr_libc=musl-
else
    __nr_libc=
fi
__nr_rid="linux-${__nr_libc}${__nr_arch}"

if [ -n "$__nr_arch" ] && [ -d "$CORECLR_NEWRELIC_HOME/$__nr_rid" ]; then
    __nr_target="$__nr_rid/libNewRelicProfiler.so"
    __nr_link="$CORECLR_NEWRELIC_HOME/libNewRelicProfiler.so"
    if [ -L "$__nr_link" ]; then
        if [ "$(readlink "$__nr_link")" != "$__nr_target" ]; then
            ln -sf "$__nr_target" "$__nr_link" 2>/dev/null || true
        fi
    elif [ ! -e "$__nr_link" ]; then
        ln -sf "$__nr_target" "$__nr_link" 2>/dev/null || true
    fi
fi

CORECLR_NEWRELIC_HOME="$CORECLR_NEWRELIC_HOME" \
CORECLR_ENABLE_PROFILING=1 \
CORECLR_PROFILER={36032161-FFC0-4B61-B559-F6C5D41BAE5A} \
CORECLR_PROFILER_PATH="$CORECLR_NEWRELIC_HOME/libNewRelicProfiler.so" \
    "$@"
