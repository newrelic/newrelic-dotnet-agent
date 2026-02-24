#!/bin/bash
# Copyright 2020 New Relic, Inc. All rights reserved.
# SPDX-License-Identifier: Apache-2.0

# Launches the test app with the Rust profiler attached (Linux).
# Usage: ./run-with-profiler.sh [--release]

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
TEST_APP_DIR="$SCRIPT_DIR/ProfilerTestApp"

# Determine profiler .so path
BUILD_PROFILE="debug"
if [ "$1" = "--release" ]; then
    BUILD_PROFILE="release"
fi

PROFILER_SO="$REPO_ROOT/target/$BUILD_PROFILE/libnewrelic_profiler_poc.so"

if [ ! -f "$PROFILER_SO" ]; then
    echo "Profiler .so not found at: $PROFILER_SO"
    echo "Run 'cargo build' first from: $REPO_ROOT"
    exit 1
fi

PROFILER_SO_FULL="$(realpath "$PROFILER_SO")"

echo ""
echo "=== Launching test app with Rust profiler ==="
echo "Profiler SO: $PROFILER_SO_FULL"
echo ""

# Set .NET Core profiler environment variables
# CLSID must match CLSID_PROFILER_CORECLR in profiler_callback.rs
export CORECLR_ENABLE_PROFILING=1
export CORECLR_PROFILER="{36032161-FFC0-4B61-B559-F6C5D41BAE5A}"
export CORECLR_PROFILER_PATH="$PROFILER_SO_FULL"

# Enable env_logger output
export RUST_LOG=info

dotnet run --project "$TEST_APP_DIR"
