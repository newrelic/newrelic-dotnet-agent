//! Validation framework for ensuring perfect fidelity with C++ profiler
//!
//! This module provides tools for side-by-side comparison between the
//! C++ and Rust profiler implementations. Given the extremely low risk
//! tolerance for this project, we need comprehensive validation that
//! proves byte-for-byte compatibility.

use log::{info, debug, warn};
use serde::{Deserialize, Serialize};
use std::collections::HashMap;
use std::fs;
use std::path::Path;
use std::sync::{Arc, Mutex};

/// Validation event captured from profiler execution
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ValidationEvent {
    pub timestamp_micros: u64,
    pub event_type: String,
    pub function_id: Option<u64>,
    pub module_id: Option<u64>,
    pub method_name: Option<String>,
    pub assembly_name: Option<String>,
    pub parameters: HashMap<String, String>,
}

/// Validation framework for comparing C++ vs Rust profiler behavior
pub struct ValidationFramework {
    /// Events captured during profiler execution
    events: Arc<Mutex<Vec<ValidationEvent>>>,

    /// Enable/disable event capture
    enabled: bool,

    /// Output directory for validation logs
    output_dir: String,
}

impl ValidationFramework {
    pub fn new() -> Self {
        let output_dir = std::env::var("NEWRELIC_VALIDATION_OUTPUT")
            .unwrap_or_else(|_| "./validation_output".to_string());

        // Create output directory if it doesn't exist
        if let Err(e) = fs::create_dir_all(&output_dir) {
            warn!("Failed to create validation output directory: {}", e);
        }

        Self {
            events: Arc::new(Mutex::new(Vec::new())),
            enabled: std::env::var("NEWRELIC_VALIDATION_ENABLED").is_ok(),
            output_dir,
        }
    }

    /// Log a profiler event for validation
    pub fn log_event(&self, event: ValidationEvent) {
        if !self.enabled {
            return;
        }

        debug!("Validation event: {:?}", event);

        if let Ok(mut events) = self.events.lock() {
            events.push(event);
        }
    }

    /// Log JIT compilation event
    pub fn log_jit_compilation(&self, function_id: u64, is_safe_to_block: bool) {
        let mut params = HashMap::new();
        params.insert("is_safe_to_block".to_string(), is_safe_to_block.to_string());

        let event = ValidationEvent {
            timestamp_micros: get_timestamp_micros(),
            event_type: "JITCompilationStarted".to_string(),
            function_id: Some(function_id),
            module_id: None,
            method_name: None,
            assembly_name: None,
            parameters: params,
        };

        self.log_event(event);
    }

    /// Log module load event
    pub fn log_module_load(&self, module_id: u64, assembly_name: Option<String>) {
        let event = ValidationEvent {
            timestamp_micros: get_timestamp_micros(),
            event_type: "ModuleLoadFinished".to_string(),
            function_id: None,
            module_id: Some(module_id),
            method_name: None,
            assembly_name,
            parameters: HashMap::new(),
        };

        self.log_event(event);
    }

    /// Export captured events to JSON file for analysis
    pub fn export_events(&self, filename: &str) -> Result<(), Box<dyn std::error::Error + '_>> {
        if !self.enabled {
            return Ok(());
        }

        let events = self.events.lock()?;
        let output_path = Path::new(&self.output_dir).join(filename);

        let json = serde_json::to_string_pretty(&*events)?;
        fs::write(&output_path, json)?;

        info!("Exported {} validation events to {:?}", events.len(), output_path);
        Ok(())
    }

    /// Compare events from two profiler runs (C++ vs Rust)
    pub fn compare_event_logs(
        cpp_file: &str,
        rust_file: &str,
    ) -> Result<ValidationReport, Box<dyn std::error::Error>> {
        let cpp_events: Vec<ValidationEvent> =
            serde_json::from_str(&fs::read_to_string(cpp_file)?)?;
        let rust_events: Vec<ValidationEvent> =
            serde_json::from_str(&fs::read_to_string(rust_file)?)?;

        let report = ValidationReport::compare(&cpp_events, &rust_events);

        info!("Validation comparison: {} differences found", report.differences.len());

        Ok(report)
    }
}

/// Report from comparing C++ vs Rust profiler behavior
#[derive(Debug, Serialize, Deserialize)]
pub struct ValidationReport {
    pub cpp_event_count: usize,
    pub rust_event_count: usize,
    pub differences: Vec<ValidationDifference>,
    pub summary: ValidationSummary,
}

#[derive(Debug, Serialize, Deserialize)]
pub struct ValidationDifference {
    pub event_index: usize,
    pub difference_type: String,
    pub description: String,
    pub cpp_value: Option<String>,
    pub rust_value: Option<String>,
}

#[derive(Debug, Serialize, Deserialize)]
pub struct ValidationSummary {
    pub total_differences: usize,
    pub critical_differences: usize,
    pub acceptable_differences: usize,
    pub fidelity_score: f64, // 0.0 to 1.0, where 1.0 is perfect match
}

impl ValidationReport {
    /// Compare event sequences from C++ and Rust profilers
    pub fn compare(cpp_events: &[ValidationEvent], rust_events: &[ValidationEvent]) -> Self {
        let mut differences = Vec::new();

        // Check event count difference
        if cpp_events.len() != rust_events.len() {
            differences.push(ValidationDifference {
                event_index: 0,
                difference_type: "EventCount".to_string(),
                description: "Different number of events captured".to_string(),
                cpp_value: Some(cpp_events.len().to_string()),
                rust_value: Some(rust_events.len().to_string()),
            });
        }

        // Compare events pairwise
        let min_len = std::cmp::min(cpp_events.len(), rust_events.len());
        for i in 0..min_len {
            let cpp_event = &cpp_events[i];
            let rust_event = &rust_events[i];

            // Compare event types
            if cpp_event.event_type != rust_event.event_type {
                differences.push(ValidationDifference {
                    event_index: i,
                    difference_type: "EventType".to_string(),
                    description: "Event type mismatch".to_string(),
                    cpp_value: Some(cpp_event.event_type.clone()),
                    rust_value: Some(rust_event.event_type.clone()),
                });
            }

            // Compare function IDs
            if cpp_event.function_id != rust_event.function_id {
                differences.push(ValidationDifference {
                    event_index: i,
                    difference_type: "FunctionID".to_string(),
                    description: "Function ID mismatch".to_string(),
                    cpp_value: cpp_event.function_id.map(|id| id.to_string()),
                    rust_value: rust_event.function_id.map(|id| id.to_string()),
                });
            }

            // TODO: Add more detailed comparisons for parameters, timing, etc.
        }

        let critical_differences = differences.iter()
            .filter(|d| d.difference_type != "Timestamp") // Timing differences are acceptable
            .count();

        let total_differences = differences.len();
        let acceptable_differences = total_differences - critical_differences;

        let fidelity_score = if differences.is_empty() {
            1.0
        } else {
            1.0 - (critical_differences as f64 / cpp_events.len() as f64)
        };

        Self {
            cpp_event_count: cpp_events.len(),
            rust_event_count: rust_events.len(),
            differences,
            summary: ValidationSummary {
                total_differences,
                critical_differences,
                acceptable_differences,
                fidelity_score,
            },
        }
    }

    /// Export validation report to file
    pub fn export(&self, filename: &str) -> Result<(), Box<dyn std::error::Error>> {
        let json = serde_json::to_string_pretty(self)?;
        fs::write(filename, json)?;
        Ok(())
    }
}

/// Get current timestamp in microseconds for precise event timing
fn get_timestamp_micros() -> u64 {
    std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .unwrap_or_default()
        .as_micros() as u64
}

/// Global validation framework instance
static mut VALIDATION_FRAMEWORK: Option<ValidationFramework> = None;
static INIT_ONCE: std::sync::Once = std::sync::Once::new();

/// Get the global validation framework instance
pub fn get_validation_framework() -> &'static ValidationFramework {
    unsafe {
        INIT_ONCE.call_once(|| {
            VALIDATION_FRAMEWORK = Some(ValidationFramework::new());
        });
        VALIDATION_FRAMEWORK.as_ref().unwrap()
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_validation_event_creation() {
        let event = ValidationEvent {
            timestamp_micros: get_timestamp_micros(),
            event_type: "Test".to_string(),
            function_id: Some(12345),
            module_id: None,
            method_name: Some("TestMethod".to_string()),
            assembly_name: None,
            parameters: HashMap::new(),
        };

        assert_eq!(event.event_type, "Test");
        assert_eq!(event.function_id, Some(12345));
    }

    #[test]
    fn test_validation_framework() {
        let framework = ValidationFramework::new();

        framework.log_jit_compilation(12345, true);
        framework.log_module_load(67890, Some("TestAssembly".to_string()));

        // Test event export (will only work if validation is enabled)
        let _ = framework.export_events("test_events.json");
    }

    #[test]
    fn test_validation_report() {
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
        ];

        let rust_events = vec![
            ValidationEvent {
                timestamp_micros: 1001, // Slight timing difference
                event_type: "JITCompilationStarted".to_string(),
                function_id: Some(1),
                module_id: None,
                method_name: None,
                assembly_name: None,
                parameters: HashMap::new(),
            },
        ];

        let report = ValidationReport::compare(&cpp_events, &rust_events);

        // Should be identical except for timing
        assert_eq!(report.cpp_event_count, 1);
        assert_eq!(report.rust_event_count, 1);
        assert_eq!(report.summary.critical_differences, 0);
    }
}