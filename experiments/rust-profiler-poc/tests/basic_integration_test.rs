//! Basic integration tests for New Relic Profiler POC
//!
//! These tests verify that the POC can be built and basic functionality works.
//! More comprehensive testing will be added as the implementation progresses.

use std::ffi::CString;

#[test]
fn test_profiler_version_export() {
    // Test that we can call the P/Invoke export
    extern "C" {
        fn NewRelic_Profiler_GetVersion() -> *const std::ffi::c_char;
    }

    unsafe {
        let version_ptr = NewRelic_Profiler_GetVersion();
        assert!(!version_ptr.is_null(), "Version pointer should not be null");

        let version_cstr = CString::from_raw(version_ptr as *mut _);
        let version_str = version_cstr.to_string_lossy();

        assert!(
            version_str.contains("NewRelic.Profiler.Rust.POC"),
            "Version string should identify as Rust POC: {}",
            version_str
        );
    }
}

#[test]
fn test_validation_framework() {
    let framework = newrelic_profiler_poc::validation::ValidationFramework::new();

    // Test basic event logging
    framework.log_jit_compilation(12345, true);
    framework.log_module_load(67890, Some("TestAssembly".to_string()));

    // Test event export (will create file if validation enabled)
    assert!(
        framework.export_events("test_basic_integration.json").is_ok(),
        "Should be able to export events"
    );
}

#[cfg(windows)]
#[test]
fn test_profiler_guids() {
    use newrelic_profiler_poc::profiler_callback::{PROFILER_GUID_NETFX, PROFILER_GUID_NETCORE};

    // Verify GUIDs are set correctly
    assert_eq!(
        PROFILER_GUID_NETFX.to_string(),
        "{71da0a04-7777-4ec6-9643-7d28b46a8a41}",
        ".NET Framework profiler GUID must match C++ implementation"
    );

    assert_eq!(
        PROFILER_GUID_NETCORE.to_string(),
        "{36032161-ffc0-4b61-b559-f6c5d41bae5a}",
        ".NET Core profiler GUID must match C++ implementation"
    );
}

#[test]
fn test_profiler_callback_creation() {
    // Test that we can create profiler callback instances
    let _callback = newrelic_profiler_poc::profiler_callback::CorProfilerCallbackImpl::new();
    // If this doesn't panic, the basic structure is working
}

// TODO: Add tests for:
// - COM interface functionality (Windows)
// - Event logging and validation
// - Cross-platform compatibility
// - Performance benchmarks
// - Side-by-side comparison with C++ profiler