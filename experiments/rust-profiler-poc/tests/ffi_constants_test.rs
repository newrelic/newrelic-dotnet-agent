// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

//! Tests for FFI constants and type definitions.
//! Verifies HRESULT values and COR_PRF_MONITOR flags match the CLR's corprof.h.

use newrelic_profiler_poc::ffi::*;

#[test]
fn hresult_success_values() {
    assert_eq!(S_OK, 0);
    assert_eq!(S_FALSE, 1);
    assert!(succeeded(S_OK));
    assert!(succeeded(S_FALSE));
}

#[test]
fn hresult_failure_values() {
    assert!(failed(E_FAIL));
    assert!(failed(E_NOINTERFACE));
    assert!(failed(CORPROF_E_PROFILER_CANCEL_ACTIVATION));
    assert!(!succeeded(E_FAIL));
    assert!(!succeeded(E_NOINTERFACE));
}

#[test]
fn cor_prf_monitor_individual_flag_values() {
    // Each flag value must match corprof.h exactly
    assert_eq!(COR_PRF_MONITOR::COR_PRF_MONITOR_JIT_COMPILATION.bits(), 0x00000020);
    assert_eq!(COR_PRF_MONITOR::COR_PRF_MONITOR_MODULE_LOADS.bits(), 0x00000004);
    assert_eq!(COR_PRF_MONITOR::COR_PRF_MONITOR_THREADS.bits(), 0x00000200);
    assert_eq!(COR_PRF_MONITOR::COR_PRF_ENABLE_REJIT.bits(), 0x00040000);
    assert_eq!(COR_PRF_MONITOR::COR_PRF_ENABLE_STACK_SNAPSHOT.bits(), 0x10000000);
    assert_eq!(COR_PRF_MONITOR::COR_PRF_USE_PROFILE_IMAGES.bits(), 0x20000000);
    assert_eq!(COR_PRF_MONITOR::COR_PRF_DISABLE_ALL_NGEN_IMAGES.bits(), 0x80000000);
}

#[test]
fn event_mask_matches_cpp_profiler() {
    // The C++ profiler sets this exact mask in CorProfilerCallbackImpl.h line 875-876.
    // Our Initialize() must produce the same combined value.
    let mask = COR_PRF_MONITOR::COR_PRF_MONITOR_JIT_COMPILATION
        | COR_PRF_MONITOR::COR_PRF_MONITOR_MODULE_LOADS
        | COR_PRF_MONITOR::COR_PRF_USE_PROFILE_IMAGES
        | COR_PRF_MONITOR::COR_PRF_MONITOR_THREADS
        | COR_PRF_MONITOR::COR_PRF_ENABLE_STACK_SNAPSHOT
        | COR_PRF_MONITOR::COR_PRF_ENABLE_REJIT
        | COR_PRF_MONITOR::COR_PRF_DISABLE_ALL_NGEN_IMAGES;

    let expected: u32 = 0x00000020 | 0x00000004 | 0x20000000 | 0x00000200
        | 0x10000000 | 0x00040000 | 0x80000000;
    assert_eq!(mask.bits(), expected);
}
