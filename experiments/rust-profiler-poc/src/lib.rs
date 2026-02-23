//! New Relic .NET Agent Profiler - Rust POC
//!
//! Simplified implementation focused on proving cross-platform compilation,
//! especially musl-based Linux distributions (Alpine Linux, etc.)
//!
//! Key goals for POC:
//! - Prove musl compilation works (current C++ limitation)
//! - Demonstrate improved maintainability with Rust
//! - Show cross-platform ARM support
//! - Establish validation framework for perfect fidelity

pub mod profiler_callback;
pub mod validation;

use log::info;

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
    static PLATFORM: &str = "Linux-musl\0"; // KEY VALUE: musl support!

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

/// Initialize logging for the profiler
fn init_logging() {
    if std::env::var("NEWRELIC_PROFILER_LOG").is_ok() {
        env_logger::builder()
            .format_timestamp_micros()
            .init();

        info!("New Relic Profiler POC - Rust implementation started");
        info!("Platform: {}", get_platform_string());
    }
}

/// Get platform string for logging
fn get_platform_string() -> &'static str {
    #[cfg(target_os = "windows")]
    return "Windows";

    #[cfg(all(target_os = "linux", target_env = "gnu"))]
    return "Linux-glibc";

    #[cfg(all(target_os = "linux", target_env = "musl"))]
    return "Linux-musl (Alpine Linux compatible!)"; // This is our key value!

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

// Platform-specific initialization
#[cfg(windows)]
#[no_mangle]
pub extern "system" fn DllMain(
    _hinst_dll: *const std::ffi::c_void,
    fdw_reason: u32,
    _lpv_reserved: *mut std::ffi::c_void,
) -> bool {
    match fdw_reason {
        1 => {  // DLL_PROCESS_ATTACH
            init_logging();
            info!("New Relic Profiler POC DLL attached to process");
            true
        }
        0 => {  // DLL_PROCESS_DETACH
            info!("New Relic Profiler POC DLL detached from process");
            true
        }
        _ => true,
    }
}

// Unix shared library initialization
#[cfg(unix)]
#[no_mangle]
pub extern "C" fn _init() {
    init_logging();
    info!("New Relic Profiler POC shared library loaded (Unix)");
}