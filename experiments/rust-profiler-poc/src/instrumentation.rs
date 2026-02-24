// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

//! Instrumentation matching: determines which methods should be instrumented.
//!
//! The C++ profiler uses three-tier set matching (assembly → type → method)
//! populated from XML extension files and hardcoded entries. During JIT
//! compilation, methods are checked against these sets — assembly first
//! (cheapest filter, eliminates most events), then method name, then type.
//!
//! For the POC, we use hardcoded instrumentation points. XML-based
//! configuration loading is a Phase 2/3 item.

use std::collections::HashSet;

/// An instrumentation point defining a specific method to instrument.
#[derive(Debug, Clone, PartialEq, Eq, Hash)]
pub struct InstrumentationPoint {
    pub assembly_name: String,
    pub class_name: String,
    pub method_name: String,
}

/// Determines which methods should be instrumented based on assembly,
/// type, and method name matching.
///
/// Mirrors the C++ profiler's MethodRewriter.h which maintains three
/// sets (_instrumentedAssemblies, _instrumentedTypes, _instrumentedFunctionNames)
/// and checks JIT-compiled methods against them.
pub struct InstrumentationMatcher {
    assemblies: HashSet<String>,
    types: HashSet<String>,
    methods: HashSet<String>,
    points: Vec<InstrumentationPoint>,
}

impl InstrumentationMatcher {
    /// Create a new matcher with the given instrumentation points.
    pub fn new(points: Vec<InstrumentationPoint>) -> Self {
        let mut assemblies = HashSet::new();
        let mut types = HashSet::new();
        let mut methods = HashSet::new();

        // The C++ profiler hardcodes mscorlib and AgentShim helper methods.
        // These are needed for the agent's bootstrap mechanism.
        assemblies.insert("mscorlib".to_string());
        types.insert("System.CannotUnloadAppDomainException".to_string());
        methods.insert("GetAppDomainBoolean".to_string());
        methods.insert("GetThreadLocalBoolean".to_string());
        methods.insert("SetThreadLocalBoolean".to_string());
        methods.insert("GetMethodFromAppDomainStorageOrReflectionOrThrow".to_string());
        methods.insert("GetMethodFromAppDomainStorage".to_string());
        methods.insert("GetMethodViaReflectionOrThrow".to_string());
        methods.insert("GetTypeViaReflectionOrThrow".to_string());
        methods.insert("LoadAssemblyOrThrow".to_string());
        methods.insert("StoreMethodInAppDomainStorageOrThrow".to_string());

        // Add instrumentation points from configuration
        for point in &points {
            assemblies.insert(point.assembly_name.clone());
            types.insert(point.class_name.clone());
            methods.insert(point.method_name.clone());
        }

        Self {
            assemblies,
            types,
            methods,
            points,
        }
    }

    /// Create a matcher with POC test targets.
    /// These target methods in our ProfilerTestApp for validation.
    pub fn with_test_targets() -> Self {
        let points = vec![
            InstrumentationPoint {
                assembly_name: "ProfilerTestApp".to_string(),
                class_name: "ProfilerTestApp.Program".to_string(),
                method_name: "DoSomeWork".to_string(),
            },
            InstrumentationPoint {
                assembly_name: "ProfilerTestApp".to_string(),
                class_name: "ProfilerTestApp.Program".to_string(),
                method_name: "DoAsyncWork".to_string(),
            },
            InstrumentationPoint {
                assembly_name: "ProfilerTestApp".to_string(),
                class_name: "ProfilerTestApp.Program".to_string(),
                method_name: "TryCatchWork".to_string(),
            },
        ];
        Self::new(points)
    }

    /// Check if an assembly should be considered for instrumentation.
    /// This is the first (cheapest) filter — eliminates most JIT events.
    pub fn should_instrument_assembly(&self, assembly_name: &str) -> bool {
        self.assemblies.contains(assembly_name)
    }

    /// Check if a method name is in the instrumentation set.
    pub fn should_instrument_method(&self, method_name: &str) -> bool {
        self.methods.contains(method_name)
    }

    /// Check if a type name is in the instrumentation set.
    pub fn should_instrument_type(&self, type_name: &str) -> bool {
        self.types.contains(type_name)
    }

    /// Full three-tier check: does this specific assembly+type+method
    /// match any instrumentation point?
    pub fn matches(&self, assembly_name: &str, type_name: &str, method_name: &str) -> bool {
        // Fast path: assembly not in set at all
        if !self.should_instrument_assembly(assembly_name) {
            return false;
        }

        // Check for exact instrumentation point match
        self.points.iter().any(|p| {
            p.assembly_name == assembly_name
                && p.class_name == type_name
                && p.method_name == method_name
        })
    }

    /// Return the instrumentation points (for logging/debugging).
    pub fn points(&self) -> &[InstrumentationPoint] {
        &self.points
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn test_matcher() -> InstrumentationMatcher {
        InstrumentationMatcher::new(vec![
            InstrumentationPoint {
                assembly_name: "MyApp".to_string(),
                class_name: "MyApp.Controllers.HomeController".to_string(),
                method_name: "Index".to_string(),
            },
            InstrumentationPoint {
                assembly_name: "MyApp".to_string(),
                class_name: "MyApp.Services.DataService".to_string(),
                method_name: "GetData".to_string(),
            },
        ])
    }

    #[test]
    fn assembly_filter_excludes_non_targets() {
        let matcher = test_matcher();
        assert!(!matcher.should_instrument_assembly("System.Private.CoreLib"));
        assert!(!matcher.should_instrument_assembly("Microsoft.Extensions.Logging"));
        assert!(!matcher.should_instrument_assembly(""));
    }

    #[test]
    fn assembly_filter_includes_targets() {
        let matcher = test_matcher();
        assert!(matcher.should_instrument_assembly("MyApp"));
        // mscorlib is always included (hardcoded for agent bootstrap)
        assert!(matcher.should_instrument_assembly("mscorlib"));
    }

    #[test]
    fn method_filter_includes_targets() {
        let matcher = test_matcher();
        assert!(matcher.should_instrument_method("Index"));
        assert!(matcher.should_instrument_method("GetData"));
    }

    #[test]
    fn method_filter_includes_hardcoded_agent_methods() {
        let matcher = test_matcher();
        assert!(matcher.should_instrument_method("GetAppDomainBoolean"));
        assert!(matcher.should_instrument_method("LoadAssemblyOrThrow"));
    }

    #[test]
    fn full_match_requires_all_three() {
        let matcher = test_matcher();
        // Exact match
        assert!(matcher.matches("MyApp", "MyApp.Controllers.HomeController", "Index"));
        // Wrong method
        assert!(!matcher.matches("MyApp", "MyApp.Controllers.HomeController", "About"));
        // Wrong type
        assert!(!matcher.matches("MyApp", "MyApp.Controllers.OtherController", "Index"));
        // Wrong assembly
        assert!(!matcher.matches("OtherApp", "MyApp.Controllers.HomeController", "Index"));
    }

    #[test]
    fn test_targets_include_profiler_test_app() {
        let matcher = InstrumentationMatcher::with_test_targets();
        assert!(matcher.should_instrument_assembly("ProfilerTestApp"));
        assert!(matcher.matches("ProfilerTestApp", "ProfilerTestApp.Program", "DoSomeWork"));
        assert!(matcher.matches("ProfilerTestApp", "ProfilerTestApp.Program", "TryCatchWork"));
        assert!(!matcher.matches("ProfilerTestApp", "ProfilerTestApp.Program", "Main"));
    }

    #[test]
    fn empty_matcher_only_has_hardcoded_entries() {
        let matcher = InstrumentationMatcher::new(vec![]);
        assert!(matcher.should_instrument_assembly("mscorlib"));
        assert!(!matcher.should_instrument_assembly("SomeOtherAssembly"));
        assert!(matcher.should_instrument_method("GetAppDomainBoolean"));
    }
}
