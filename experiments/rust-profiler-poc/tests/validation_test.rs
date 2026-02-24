// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

//! Integration tests for the validation framework.
//! Verifies event capture, export, and fidelity comparison work correctly.

use newrelic_profiler_poc::validation::{ValidationEvent, ValidationFramework, ValidationReport};
use std::collections::HashMap;

#[test]
fn event_capture_and_export() {
    let framework = ValidationFramework::new();

    framework.log_jit_compilation(12345, true);
    framework.log_jit_compilation(67890, false);
    framework.log_module_load(11111, Some("TestAssembly".to_string()));
    framework.log_module_load(22222, None);

    assert!(
        framework.export_events("test_validation_export.json").is_ok(),
        "Should be able to export events"
    );
}

#[test]
fn identical_events_yield_perfect_fidelity() {
    let events = vec![
        ValidationEvent {
            timestamp_micros: 1000,
            event_type: "JITCompilationStarted".to_string(),
            function_id: Some(1),
            module_id: None,
            method_name: None,
            assembly_name: None,
            parameters: HashMap::new(),
        },
        ValidationEvent {
            timestamp_micros: 2000,
            event_type: "ModuleLoadFinished".to_string(),
            function_id: None,
            module_id: Some(100),
            method_name: None,
            assembly_name: Some("MyAssembly".to_string()),
            parameters: HashMap::new(),
        },
    ];

    let report = ValidationReport::compare(&events, &events);
    assert_eq!(report.summary.fidelity_score, 1.0);
    assert_eq!(report.summary.critical_differences, 0);
    assert_eq!(report.differences.len(), 0);
}

#[test]
fn function_id_mismatch_detected() {
    let cpp_events = vec![ValidationEvent {
        timestamp_micros: 1000,
        event_type: "JITCompilationStarted".to_string(),
        function_id: Some(1),
        module_id: None,
        method_name: None,
        assembly_name: None,
        parameters: HashMap::new(),
    }];

    let rust_events = vec![ValidationEvent {
        timestamp_micros: 1000,
        event_type: "JITCompilationStarted".to_string(),
        function_id: Some(999),
        module_id: None,
        method_name: None,
        assembly_name: None,
        parameters: HashMap::new(),
    }];

    let report = ValidationReport::compare(&cpp_events, &rust_events);
    assert!(report.summary.critical_differences > 0);
    assert!(report.summary.fidelity_score < 1.0);
}

#[test]
fn event_count_mismatch_detected() {
    let cpp_events = vec![
        ValidationEvent {
            timestamp_micros: 1000,
            event_type: "JITCompilationStarted".to_string(),
            function_id: Some(1),
            module_id: None,
            method_name: None,
            assembly_name: None,
            parameters: HashMap::new(),
        },
        ValidationEvent {
            timestamp_micros: 2000,
            event_type: "JITCompilationStarted".to_string(),
            function_id: Some(2),
            module_id: None,
            method_name: None,
            assembly_name: None,
            parameters: HashMap::new(),
        },
    ];

    let rust_events = vec![ValidationEvent {
        timestamp_micros: 1000,
        event_type: "JITCompilationStarted".to_string(),
        function_id: Some(1),
        module_id: None,
        method_name: None,
        assembly_name: None,
        parameters: HashMap::new(),
    }];

    let report = ValidationReport::compare(&cpp_events, &rust_events);
    assert_eq!(report.cpp_event_count, 2);
    assert_eq!(report.rust_event_count, 1);
    assert!(!report.differences.is_empty(), "Should detect event count difference");
}
