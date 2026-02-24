# Copyright 2020 New Relic, Inc. All rights reserved.
# SPDX-License-Identifier: Apache-2.0

# Launches the test app with the Rust profiler attached.
# Usage: .\run-with-profiler.ps1 [-Release] [-Build]
#
# Prerequisites:
#   - cargo build (or cargo build --release) in the rust-profiler-poc directory
#   - dotnet build in the test-app/ProfilerTestApp directory

param(
    [switch]$Release,
    [switch]$Build
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path "$PSScriptRoot/.."
$testAppDir = "$PSScriptRoot/ProfilerTestApp"

# Determine profiler DLL path
$buildProfile = if ($Release) { "release" } else { "debug" }
$profilerDll = "$repoRoot/target/$buildProfile/newrelic_profiler_poc.dll"

if (!(Test-Path $profilerDll)) {
    Write-Host "Profiler DLL not found at: $profilerDll" -ForegroundColor Red
    Write-Host "Run 'cargo build' first from: $repoRoot" -ForegroundColor Yellow
    exit 1
}

$profilerDllFull = Resolve-Path $profilerDll

# Build test app if requested
if ($Build) {
    Write-Host "Building test app..." -ForegroundColor Cyan
    dotnet build $testAppDir -c Debug
    if ($LASTEXITCODE -ne 0) { exit 1 }
}

# Build profiler if requested
if ($Build) {
    Write-Host "Building profiler..." -ForegroundColor Cyan
    Push-Location $repoRoot
    if ($Release) { cargo build --release } else { cargo build }
    if ($LASTEXITCODE -ne 0) { Pop-Location; exit 1 }
    Pop-Location
}

Write-Host ""
Write-Host "=== Launching test app with Rust profiler ===" -ForegroundColor Green
Write-Host "Profiler DLL: $profilerDllFull" -ForegroundColor Gray
Write-Host ""

# Set .NET Core profiler environment variables
# CLSID must match CLSID_PROFILER_CORECLR in profiler_callback.rs
$env:CORECLR_ENABLE_PROFILING = "1"
$env:CORECLR_PROFILER = "{36032161-FFC0-4B61-B559-F6C5D41BAE5A}"
$env:CORECLR_PROFILER_PATH = $profilerDllFull

# Enable env_logger output so we can see profiler log messages
$env:RUST_LOG = "info"

try {
    dotnet run --project $testAppDir --no-build
}
finally {
    # Clean up environment variables
    Remove-Item Env:CORECLR_ENABLE_PROFILING -ErrorAction SilentlyContinue
    Remove-Item Env:CORECLR_PROFILER -ErrorAction SilentlyContinue
    Remove-Item Env:CORECLR_PROFILER_PATH -ErrorAction SilentlyContinue
    Remove-Item Env:RUST_LOG -ErrorAction SilentlyContinue
}
