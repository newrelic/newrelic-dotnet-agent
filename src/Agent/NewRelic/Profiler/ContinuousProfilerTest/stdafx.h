// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#pragma once

// Windows headers
#include <SDKDDKVer.h>
#define WIN32_LEAN_AND_MEAN
#include <windows.h>

// ATL (CComPtr) -- needed by ContinuousProfiler.h's COM/metadata usage when it is compiled directly
// into this test. Mirrors the profiler's own stdafx.h ATL includes on the Windows (non-PAL) path.
#include <atlbase.h>
#include <atlcomcli.h>

// Headers for CppUnitTest
#include <CppUnitTest.h>
