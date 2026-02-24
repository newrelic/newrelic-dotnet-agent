// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

//! New Relic .NET Agent Profiler - Rust POC
//!
//! This is a COM-based CLR profiler that implements ICorProfilerCallback4.
//! The CLR loads this DLL and calls DllGetClassObject to obtain a class factory,
//! which then creates our profiler callback instance.
//!
//! Key goals for POC:
//! - Prove musl compilation works (current C++ limitation)
//! - Demonstrate COM interface implementation in Rust
//! - Show cross-platform ARM support
//! - Establish validation framework for perfect fidelity

#[macro_use]
extern crate com;

pub mod ffi;
pub mod interfaces;
pub mod metadata_import;
pub mod method_resolver;
pub mod process_filter;
pub mod profiler_info;
pub mod profiler_callback;
pub mod validation;

use log::info;
use profiler_callback::{NewRelicProfiler, CLSID_PROFILER_CORECLR, CLSID_PROFILER_NETFX};

/// Called by the CLR to get an instance of the profiler class factory.
///
/// The CLR calls this when it sees COR_PROFILER / CORECLR_PROFILER environment
/// variables pointing to our CLSID. We create a class factory that can produce
/// NewRelicProfiler instances.
#[no_mangle]
unsafe extern "system" fn DllGetClassObject(
    class_id: *const ::com::sys::CLSID,
    iid: *const ::com::sys::IID,
    result: *mut *mut ::core::ffi::c_void,
) -> ::com::sys::HRESULT {
    init_logging();
    info!("DllGetClassObject called");

    let class_id = &*class_id;
    if class_id == &CLSID_PROFILER_NETFX || class_id == &CLSID_PROFILER_CORECLR {
        info!(
            "Creating NewRelicProfiler for CLSID {:08x}-{:04x}-{:04x}",
            class_id.data1, class_id.data2, class_id.data3
        );
        let instance = <NewRelicProfiler as ::com::production::Class>::Factory::allocate();
        instance.QueryInterface(&*iid, result)
    } else {
        info!("Unknown CLSID requested");
        ::com::sys::CLASS_E_CLASSNOTAVAILABLE
    }
}

/// Called by the CLR to check if the DLL can be unloaded.
#[no_mangle]
extern "system" fn DllCanUnloadNow() -> ::com::sys::HRESULT {
    // Always return S_OK — we don't prevent unloading
    ::com::sys::S_OK
}

/// Test export to validate P/Invoke marshaling with managed agent
#[no_mangle]
pub extern "C" fn NewRelic_Profiler_GetVersion() -> *const std::ffi::c_char {
    static VERSION: &str = "NewRelic.Profiler.Rust.POC.0.1.0\0";
    VERSION.as_ptr() as *const std::ffi::c_char
}

/// Test export for profiler info - shows platform detection
#[no_mangle]
pub extern "C" fn NewRelic_Profiler_GetPlatformInfo() -> *const std::ffi::c_char {
    #[cfg(target_os = "windows")]
    static PLATFORM: &str = "Windows\0";

    #[cfg(all(target_os = "linux", target_env = "gnu"))]
    static PLATFORM: &str = "Linux-glibc\0";

    #[cfg(all(target_os = "linux", target_env = "musl"))]
    static PLATFORM: &str = "Linux-musl\0";

    #[cfg(target_os = "macos")]
    static PLATFORM: &str = "macOS\0";

    #[cfg(not(any(
        target_os = "windows",
        all(target_os = "linux", target_env = "gnu"),
        all(target_os = "linux", target_env = "musl"),
        target_os = "macos"
    )))]
    static PLATFORM: &str = "Unknown\0";

    PLATFORM.as_ptr() as *const std::ffi::c_char
}

/// Initialize logging for the profiler.
/// Uses env_logger for POC — will be replaced with file-based logger
/// matching C++ format before integration testing. See docs/logging-requirements.md.
fn init_logging() {
    use std::sync::Once;
    static INIT: Once = Once::new();

    INIT.call_once(|| {
        env_logger::builder()
            .filter_level(log::LevelFilter::Info)
            .format_timestamp_micros()
            .init();

        info!("New Relic Profiler POC (Rust) - logging initialized");
        info!("Platform: {}", get_platform_string());
    });
}

/// Get platform string for logging
fn get_platform_string() -> &'static str {
    #[cfg(target_os = "windows")]
    return "Windows";

    #[cfg(all(target_os = "linux", target_env = "gnu"))]
    return "Linux-glibc";

    #[cfg(all(target_os = "linux", target_env = "musl"))]
    return "Linux-musl (Alpine Linux compatible!)";

    #[cfg(target_os = "macos")]
    return "macOS";

    #[cfg(not(any(
        target_os = "windows",
        all(target_os = "linux", target_env = "gnu"),
        all(target_os = "linux", target_env = "musl"),
        target_os = "macos"
    )))]
    return "Unknown platform";
}

// Platform-specific DLL initialization
#[cfg(windows)]
#[no_mangle]
pub extern "system" fn DllMain(
    _hinst_dll: *const std::ffi::c_void,
    fdw_reason: u32,
    _lpv_reserved: *mut std::ffi::c_void,
) -> bool {
    match fdw_reason {
        1 => true, // DLL_PROCESS_ATTACH
        0 => true, // DLL_PROCESS_DETACH
        _ => true,
    }
}

// Unix shared library initialization
#[cfg(unix)]
#[no_mangle]
pub extern "C" fn _init() {
    init_logging();
}
