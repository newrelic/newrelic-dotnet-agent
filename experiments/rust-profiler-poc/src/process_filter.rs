// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

//! Process filtering to determine whether the profiler should instrument
//! the current process.
//!
//! The C++ profiler has extensive filtering logic in Configuration.h
//! (ShouldInstrument, ShouldInstrumentNetCore, etc.) that checks process
//! paths, parent processes, app pool IDs, and command lines. For the POC,
//! we implement the most impactful subset: excluding dotnet CLI commands
//! and known system processes that should never be instrumented.

use log::{info, trace};

/// Check if the current process should be instrumented.
/// Returns `true` if we should proceed with profiling, `false` to detach.
pub fn should_instrument_process(is_core_clr: bool) -> bool {
    let process_path = get_process_path();
    let command_line = get_command_line();

    trace!("Process path: {}", process_path);
    trace!("Command line: {}", command_line);

    if is_core_clr {
        should_instrument_core_clr(&process_path, &command_line)
    } else {
        should_instrument_net_framework(&process_path)
    }
}

/// .NET Core / .NET filtering.
/// Excludes `dotnet run`, `dotnet publish`, `dotnet restore`, `dotnet new`,
/// MSBuild invocations, and other known non-application processes.
fn should_instrument_core_clr(process_path: &str, command_line: &str) -> bool {
    let command_line_lower = command_line.to_lowercase();

    // Exclude MSBuild invocations
    if command_line_lower.contains("msbuild.dll") {
        info!("Not instrumenting: MSBuild invocation");
        return false;
    }

    // Exclude Kudu (Azure deployment engine)
    if command_line_lower.contains("kudu.services.web")
        || command_line_lower.contains("kuduagent.dll")
    {
        info!("Not instrumenting: Kudu process");
        return false;
    }

    // Exclude Azure DiagServer
    if command_line_lower.contains("./diagserver") {
        info!("Not instrumenting: DiagServer process");
        return false;
    }

    // Check for `dotnet <command>` CLI invocations that shouldn't be profiled.
    // The C++ profiler tokenizes the command line and checks if a dotnet process
    // is followed by run/publish/restore/new.
    if is_dotnet_cli_command(command_line) {
        info!("Not instrumenting: dotnet CLI command");
        return false;
    }

    true
}

/// .NET Framework filtering.
/// Excludes known system processes.
fn should_instrument_net_framework(process_path: &str) -> bool {
    let path_upper = process_path.to_uppercase();

    // SMSvcHost.exe causes connection failures when instrumented
    if path_upper.ends_with("SMSVCHOST.EXE") {
        info!("Not instrumenting: SMSvcHost.exe is an ignored process");
        return false;
    }

    true
}

/// Check if the command line represents a `dotnet` CLI command that
/// shouldn't be instrumented (run, publish, restore, new).
///
/// Matches the C++ logic in ShouldInstrumentNetCore which tokenizes
/// the command line and looks for patterns like:
///   dotnet run
///   dotnet.exe publish -f net8.0
///   "C:\Program Files\dotnet\dotnet.exe" restore
fn is_dotnet_cli_command(command_line: &str) -> bool {
    let tokens: Vec<&str> = command_line.split_whitespace().collect();

    for (i, token) in tokens.iter().enumerate() {
        let token_lower = token.to_lowercase();
        // Strip surrounding quotes
        let token_clean = token_lower.trim_matches('"').trim_matches('\'');

        let is_dotnet = token_clean.ends_with("dotnet")
            || token_clean.ends_with("dotnet.exe");

        if is_dotnet {
            // Check the next token for CLI commands
            if let Some(next) = tokens.get(i + 1) {
                let next_lower = next.to_lowercase();
                if matches!(
                    next_lower.as_str(),
                    "run" | "publish" | "restore" | "new"
                ) {
                    return true;
                }
            }
            break;
        }
    }

    false
}

/// Get the path of the current process.
/// Uses std::env::current_exe() which maps to GetModuleFileNameW on Windows
/// and readlink("/proc/self/exe") on Linux.
fn get_process_path() -> String {
    std::env::current_exe()
        .map(|p| p.to_string_lossy().into_owned())
        .unwrap_or_else(|_| String::from("."))
}

/// Get the command line of the current process.
fn get_command_line() -> String {
    std::env::args().collect::<Vec<_>>().join(" ")
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_dotnet_run_excluded() {
        assert!(is_dotnet_cli_command("dotnet run"));
        assert!(is_dotnet_cli_command("dotnet.exe run"));
        assert!(is_dotnet_cli_command("dotnet run -f net8.0"));
        assert!(is_dotnet_cli_command(r#""C:\Program Files\dotnet\dotnet.exe" run"#));
        assert!(is_dotnet_cli_command("dotnet publish"));
        assert!(is_dotnet_cli_command("dotnet restore"));
        assert!(is_dotnet_cli_command("dotnet new"));
    }

    #[test]
    fn test_dotnet_app_not_excluded() {
        // Running an actual app DLL should NOT be excluded
        assert!(!is_dotnet_cli_command("dotnet MyApp.dll"));
        assert!(!is_dotnet_cli_command("dotnet.exe MyApp.dll"));
        assert!(!is_dotnet_cli_command(r#""C:\dotnet\dotnet.exe" MyApp.dll"#));
    }

    #[test]
    fn test_non_dotnet_process_not_excluded() {
        assert!(!is_dotnet_cli_command("MyApp.exe --some-flag"));
        assert!(!is_dotnet_cli_command("w3wp.exe"));
    }

    #[test]
    fn test_msbuild_excluded_from_coreclr() {
        assert!(!should_instrument_core_clr(
            "dotnet.exe",
            "dotnet exec MSBuild.dll /restore"
        ));
    }

    #[test]
    fn test_smsvchost_excluded_from_netfx() {
        assert!(!should_instrument_net_framework(r"C:\Windows\System32\SMSvcHost.exe"));
    }

    #[test]
    fn test_normal_app_passes() {
        assert!(should_instrument_core_clr("dotnet.exe", "dotnet MyApp.dll"));
        assert!(should_instrument_net_framework(r"C:\MyApp\MyApp.exe"));
    }
}
