// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

//! Cross-module integration tests for process filtering.
//! Unit tests live in src/process_filter.rs; these test the public API.

use newrelic_profiler_poc::process_filter;

#[test]
fn dotnet_cli_commands_are_detected() {
    assert!(process_filter::is_dotnet_cli_command("dotnet run"));
    assert!(process_filter::is_dotnet_cli_command("dotnet publish"));
    assert!(process_filter::is_dotnet_cli_command("dotnet restore"));
    assert!(process_filter::is_dotnet_cli_command("dotnet new"));
}

#[test]
fn dotnet_app_execution_is_not_excluded() {
    assert!(!process_filter::is_dotnet_cli_command("dotnet MyApp.dll"));
    assert!(!process_filter::is_dotnet_cli_command("dotnet.exe MyApp.dll"));
}

#[test]
fn non_dotnet_processes_are_not_excluded() {
    assert!(!process_filter::is_dotnet_cli_command("w3wp.exe"));
    assert!(!process_filter::is_dotnet_cli_command("MyApp.exe --flag"));
}

#[test]
fn should_instrument_process_does_not_panic() {
    // We can't control the real process path/command line in a test,
    // but we can verify the function doesn't panic for either CLR type.
    let _ = process_filter::should_instrument_process(true);
    let _ = process_filter::should_instrument_process(false);
}
