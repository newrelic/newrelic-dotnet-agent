// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

fn main() {
    // The `com` crate's production feature uses Windows registry functions
    // (RegCreateKeyExA, RegSetValueExA, etc.) for COM class registration.
    // We don't actually use COM registration (the CLR finds us via
    // COR_PROFILER_PATH), but the com crate links these unconditionally.
    #[cfg(windows)]
    println!("cargo:rustc-link-lib=advapi32");

    // Link ole32 for CoTaskMemAlloc used by COM
    #[cfg(windows)]
    println!("cargo:rustc-link-lib=ole32");
}
