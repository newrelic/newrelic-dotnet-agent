#!/bin/bash

if [ -z "$CORECLR_NEWRELIC_HOME" ] && [ -z "$CORECLR_NEW_RELIC_HOME" ]; then
    echo "CORECLR_NEWRELIC_HOME is undefined"
else
    NRHOME=${CORECLR_NEWRELIC_HOME:-${CORECLR_NEW_RELIC_HOME}}

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

    # Refresh the libc-aware compat symlink at the home root. If a customer
    # carried a tarball-pre-baked symlink into a runtime container with a
    # different libc, this re-points it.
    if [ -n "$__nr_arch" ] && [ -d "$NRHOME/$__nr_rid" ]; then
        __nr_target="$__nr_rid/libNewRelicProfiler.so"
        __nr_link="$NRHOME/libNewRelicProfiler.so"
        if [ -L "$__nr_link" ]; then
            if [ "$(readlink "$__nr_link")" != "$__nr_target" ]; then
                ln -sf "$__nr_target" "$__nr_link" 2>/dev/null || true
            fi
        elif [ ! -e "$__nr_link" ]; then
            ln -sf "$__nr_target" "$__nr_link" 2>/dev/null || true
        fi
        unset __nr_target __nr_link
    fi
    unset __nr_arch __nr_libc __nr_rid

    export CORECLR_ENABLE_PROFILING=1
    export CORECLR_PROFILER={36032161-FFC0-4B61-B559-F6C5D41BAE5A}
    export CORECLR_PROFILER_PATH=$NRHOME/libNewRelicProfiler.so
fi
