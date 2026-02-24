// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

//! Basic integration tests for New Relic Profiler POC
//!
//! These tests verify that the POC can be built and basic functionality works.
//! More comprehensive testing will be added as the implementation progresses.

#[test]
fn test_profiler_version_export() {
    extern "C" {
        fn NewRelic_Profiler_GetVersion() -> *const std::ffi::c_char;
    }

    unsafe {
        let version_ptr = NewRelic_Profiler_GetVersion();
        assert!(!version_ptr.is_null(), "Version pointer should not be null");

        let version_cstr = std::ffi::CStr::from_ptr(version_ptr);
        let version_str = version_cstr.to_string_lossy();

        assert!(
            version_str.contains("NewRelic.Profiler.Rust.POC"),
            "Version string should identify as Rust POC: {}",
            version_str
        );
    }
}

#[test]
fn test_profiler_platform_export() {
    extern "C" {
        fn NewRelic_Profiler_GetPlatformInfo() -> *const std::ffi::c_char;
    }

    unsafe {
        let platform_ptr = NewRelic_Profiler_GetPlatformInfo();
        assert!(!platform_ptr.is_null());

        let platform = std::ffi::CStr::from_ptr(platform_ptr).to_string_lossy();
        assert!(
            platform.contains("Windows") || platform.contains("Linux") || platform.contains("macOS"),
            "Platform should be recognized: {}",
            platform
        );
    }
}

#[test]
fn test_profiler_clsid_constants() {
    use newrelic_profiler_poc::profiler_callback::{CLSID_PROFILER_CORECLR, CLSID_PROFILER_NETFX};

    // .NET Framework: {71DA0A04-7777-4EC6-9643-7D28B46A8A41}
    assert_eq!(CLSID_PROFILER_NETFX.data1, 0x71DA0A04);
    assert_eq!(CLSID_PROFILER_NETFX.data2, 0x7777);
    assert_eq!(CLSID_PROFILER_NETFX.data3, 0x4EC6);

    // .NET Core: {36032161-FFC0-4B61-B559-F6C5D41BAE5A}
    assert_eq!(CLSID_PROFILER_CORECLR.data1, 0x36032161);
    assert_eq!(CLSID_PROFILER_CORECLR.data2, 0xFFC0);
    assert_eq!(CLSID_PROFILER_CORECLR.data3, 0x4B61);
}

#[test]
fn test_validation_framework() {
    let framework = newrelic_profiler_poc::validation::ValidationFramework::new();

    framework.log_jit_compilation(12345, true);
    framework.log_module_load(67890, Some("TestAssembly".to_string()));

    assert!(
        framework.export_events("test_basic_integration.json").is_ok(),
        "Should be able to export events"
    );
}
