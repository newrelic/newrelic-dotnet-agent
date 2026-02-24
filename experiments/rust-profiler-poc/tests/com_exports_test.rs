// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

//! Tests for COM DLL exports (DllGetClassObject, DllCanUnloadNow)
//! and P/Invoke exports (GetVersion, GetPlatformInfo).
//!
//! These are the entry points the CLR calls when loading the profiler.

use newrelic_profiler_poc::profiler_callback::{CLSID_PROFILER_CORECLR, CLSID_PROFILER_NETFX};

/// IID_IUnknown: {00000000-0000-0000-C000-000000000046}
const IID_IUNKNOWN: com::sys::IID = com::sys::IID {
    data1: 0x00000000,
    data2: 0x0000,
    data3: 0x0000,
    data4: [0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46],
};

extern "system" {
    fn DllGetClassObject(
        class_id: *const com::sys::CLSID,
        iid: *const com::sys::IID,
        result: *mut *mut std::ffi::c_void,
    ) -> com::sys::HRESULT;

    fn DllCanUnloadNow() -> com::sys::HRESULT;
}

extern "C" {
    fn NewRelic_Profiler_GetVersion() -> *const std::ffi::c_char;
    fn NewRelic_Profiler_GetPlatformInfo() -> *const std::ffi::c_char;
}

#[test]
fn dll_get_class_object_succeeds_for_coreclr_clsid() {
    let mut result: *mut std::ffi::c_void = std::ptr::null_mut();
    let hr = unsafe { DllGetClassObject(&CLSID_PROFILER_CORECLR, &IID_IUNKNOWN, &mut result) };

    assert_eq!(hr, com::sys::S_OK, "Should succeed for CoreCLR CLSID");
    assert!(!result.is_null(), "Should return a non-null class factory");
}

#[test]
fn dll_get_class_object_succeeds_for_netfx_clsid() {
    let mut result: *mut std::ffi::c_void = std::ptr::null_mut();
    let hr = unsafe { DllGetClassObject(&CLSID_PROFILER_NETFX, &IID_IUNKNOWN, &mut result) };

    assert_eq!(hr, com::sys::S_OK, "Should succeed for .NET Framework CLSID");
    assert!(!result.is_null(), "Should return a non-null class factory");
}

#[test]
fn dll_get_class_object_rejects_unknown_clsid() {
    let unknown_clsid = com::sys::CLSID {
        data1: 0xDEADBEEF,
        data2: 0x0000,
        data3: 0x0000,
        data4: [0x00; 8],
    };

    let mut result: *mut std::ffi::c_void = std::ptr::null_mut();
    let hr = unsafe { DllGetClassObject(&unknown_clsid, &IID_IUNKNOWN, &mut result) };

    assert_eq!(hr, com::sys::CLASS_E_CLASSNOTAVAILABLE, "Should reject unknown CLSID");
}

#[test]
fn dll_can_unload_now_returns_ok() {
    let hr = unsafe { DllCanUnloadNow() };
    assert_eq!(hr, com::sys::S_OK);
}

#[test]
fn get_version_returns_poc_identifier() {
    unsafe {
        let version_ptr = NewRelic_Profiler_GetVersion();
        assert!(!version_ptr.is_null());

        let version_str = std::ffi::CStr::from_ptr(version_ptr).to_string_lossy();
        assert!(
            version_str.contains("NewRelic.Profiler.Rust.POC"),
            "Expected POC identifier, got: {}",
            version_str
        );
    }
}

#[test]
fn get_platform_info_returns_known_platform() {
    unsafe {
        let platform_ptr = NewRelic_Profiler_GetPlatformInfo();
        assert!(!platform_ptr.is_null());

        let platform = std::ffi::CStr::from_ptr(platform_ptr).to_string_lossy();
        assert!(
            platform.contains("Windows")
                || platform.contains("Linux")
                || platform.contains("macOS"),
            "Expected known platform, got: {}",
            platform
        );
    }
}
