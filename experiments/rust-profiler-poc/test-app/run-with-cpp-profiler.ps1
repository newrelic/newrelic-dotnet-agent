# Copyright 2020 New Relic, Inc. All rights reserved.
# SPDX-License-Identifier: Apache-2.0

# Launches the test app with the C++ profiler attached for IL dump comparison.
# Usage: .\run-with-cpp-profiler.ps1
#
# Prerequisites:
#   - Build the C++ profiler: MSBuild.exe -restore -m -p:Platform=x64 -p:Configuration=Debug NewRelic.Profiler.sln
#   - dotnet build in test-app/ProfilerTestApp

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path "$PSScriptRoot/../../.."
$testAppDir = "$PSScriptRoot/ProfilerTestApp"
$profilerDll = "$repoRoot/src/Agent/_profilerBuild/x64-Debug/NewRelic.Profiler.dll"
$profilerHome = "$PSScriptRoot/cpp-profiler-home"

if (!(Test-Path $profilerDll)) {
    Write-Host "C++ Profiler DLL not found at: $profilerDll" -ForegroundColor Red
    Write-Host "Build the profiler first:" -ForegroundColor Yellow
    Write-Host '  MSBuild.exe -restore -m -p:Platform=x64 -p:Configuration=Debug src\Agent\NewRelic\Profiler\NewRelic.Profiler.sln' -ForegroundColor Yellow
    exit 1
}

$profilerDllFull = Resolve-Path $profilerDll
$profilerHomeFull = Resolve-Path $profilerHome

Write-Host ""
Write-Host "=== Launching test app with C++ profiler ===" -ForegroundColor Green
Write-Host "Profiler DLL: $profilerDllFull" -ForegroundColor Gray
Write-Host "Profiler Home: $profilerHomeFull" -ForegroundColor Gray
Write-Host ""

$env:CORECLR_ENABLE_PROFILING = "1"
$env:CORECLR_PROFILER = "{36032161-FFC0-4B61-B559-F6C5D41BAE5A}"
$env:CORECLR_PROFILER_PATH = $profilerDllFull
$env:CORECLR_NEWRELIC_HOME = $profilerHomeFull
$env:CORECLR_NEW_RELIC_HOME = $profilerHomeFull
$env:NEW_RELIC_PROFILER_DUMP_IL = "1"

try {
    dotnet run --project $testAppDir --no-build
}
finally {
    Remove-Item Env:CORECLR_ENABLE_PROFILING -ErrorAction SilentlyContinue
    Remove-Item Env:CORECLR_PROFILER -ErrorAction SilentlyContinue
    Remove-Item Env:CORECLR_PROFILER_PATH -ErrorAction SilentlyContinue
    Remove-Item Env:CORECLR_NEWRELIC_HOME -ErrorAction SilentlyContinue
    Remove-Item Env:CORECLR_NEW_RELIC_HOME -ErrorAction SilentlyContinue
    Remove-Item Env:NEW_RELIC_PROFILER_DUMP_IL -ErrorAction SilentlyContinue
}
