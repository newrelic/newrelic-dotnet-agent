//! Simplified profiler callback for POC
//!
//! This module contains a minimal profiler implementation to prove
//! cross-platform compilation works, especially musl targets.

use log::{info, debug};

/// Profiler GUIDs - must match exactly with C++ implementation
#[cfg(windows)]
pub const PROFILER_GUID_NETFX: &str = "{71DA0A04-7777-4EC6-9643-7D28B46A8A41}";
#[cfg(windows)]
pub const PROFILER_GUID_NETCORE: &str = "{36032161-FFC0-4B61-B559-F6C5D41BAE5A}";

/// Simplified profiler callback for POC
/// Real implementation will add COM interfaces later
pub struct CorProfilerCallbackImpl {
    /// Track initialization state
    initialized: bool,

    /// Simple event counter for validation
    jit_events_received: std::sync::atomic::AtomicU64,
    module_load_events: std::sync::atomic::AtomicU64,
}

impl CorProfilerCallbackImpl {
    pub fn new() -> Self {
        info!("Creating new CorProfilerCallbackImpl");

        Self {
            initialized: false,
            jit_events_received: std::sync::atomic::AtomicU64::new(0),
            module_load_events: std::sync::atomic::AtomicU64::new(0),
        }
    }

    /// Handle JIT compilation started event
    pub fn jit_compilation_started(&mut self, function_id: usize, f_is_safe_to_block: bool) -> i32 {
        let count = self.jit_events_received.fetch_add(1, std::sync::atomic::Ordering::Relaxed) + 1;

        if count <= 10 || count % 1000 == 0 {
            debug!("JITCompilationStarted: FunctionID={}, SafeToBlock={}, Count={}",
                   function_id, f_is_safe_to_block, count);
        }

        0 // S_OK equivalent
    }

    /// Handle module load finished event
    pub fn module_load_finished(&mut self, module_id: usize) -> i32 {
        let count = self.module_load_events.fetch_add(1, std::sync::atomic::Ordering::Relaxed) + 1;

        debug!("ModuleLoadFinished: ModuleID={}, Count={}", module_id, count);

        0 // S_OK equivalent
    }

    /// Get event counts for validation
    pub fn get_event_counts(&self) -> (u64, u64) {
        (
            self.jit_events_received.load(std::sync::atomic::Ordering::Relaxed),
            self.module_load_events.load(std::sync::atomic::Ordering::Relaxed),
        )
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_profiler_creation() {
        let _callback = CorProfilerCallbackImpl::new();
        // Basic smoke test - just ensure we can create the callback
    }

    #[test]
    fn test_profiler_guids() {
        #[cfg(windows)]
        {
            // Verify our GUIDs match the expected values from C++ implementation
            assert_eq!(PROFILER_GUID_NETFX, "{71DA0A04-7777-4EC6-9643-7D28B46A8A41}");
            assert_eq!(PROFILER_GUID_NETCORE, "{36032161-FFC0-4B61-B559-F6C5D41BAE5A}");
        }
    }

    #[test]
    fn test_event_handling() {
        let mut callback = CorProfilerCallbackImpl::new();

        // Test event handling
        assert_eq!(callback.jit_compilation_started(12345, true), 0);
        assert_eq!(callback.module_load_finished(67890), 0);

        // Check event counts
        let (jit_count, module_count) = callback.get_event_counts();
        assert_eq!(jit_count, 1);
        assert_eq!(module_count, 1);
    }
}