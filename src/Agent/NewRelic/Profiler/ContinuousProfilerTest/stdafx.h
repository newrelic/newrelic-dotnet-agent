// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#pragma once
// Headers for CppUnitTest
#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <CppUnitTest.h>

// The ContinuousProfiler headers under test use CComPtr and the CLR profiling-API types without
// including their providers themselves -- they rely on the profiler project's forced-include stdafx.h
// for that. Mirror the Windows half of that header here so those types are equally available to the
// tests. (The vendored corprof.h/cor.h come from the include paths this project sets, matching
// Profiler.vcxproj, so ICorProfilerInfo12 resolves the same way it does in the real build.)
#include <atlbase.h>
#include <atlcomcli.h>
