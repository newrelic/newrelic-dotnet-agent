// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

//! Method resolution: converts FunctionIDs from JIT events into human-readable
//! assembly, type, and method names.
//!
//! Mirrors the C++ profiler's Function::Create() logic from Function.h, which:
//! 1. GetFunctionInfo(functionId) → ClassID, ModuleID, mdMethodDef
//! 2. GetModuleInfo(moduleId) → AssemblyID
//! 3. GetAssemblyInfo(assemblyId) → assembly name
//! 4. GetTokenAndMetaDataFromFunction(functionId) → IMetaDataImport
//! 5. IMetaDataImport::GetMethodProps(token) → method name, type token
//! 6. IMetaDataImport::GetTypeDefProps(typeToken) → type/class name

use crate::ffi::*;
use crate::metadata_import::IMetaDataImport;
use crate::profiler_info::ICorProfilerInfo4;
use com::{interfaces::iunknown::IUnknown, Interface};
use std::ffi::c_void;

/// Resolved method information from a FunctionID.
#[derive(Debug, Clone)]
pub struct FunctionInfo {
    pub assembly_name: String,
    pub type_name: String,
    pub method_name: String,
    pub module_id: ModuleID,
    pub method_token: mdMethodDef,
}

/// Resolve a FunctionID into assembly, type, and method names.
///
/// # Safety
/// Calls CLR profiling APIs through COM interface pointers.
/// Must only be called from profiler callback methods where the CLR
/// guarantees the FunctionID is valid.
pub unsafe fn resolve_function_name(
    profiler_info: &ICorProfilerInfo4,
    function_id: FunctionID,
) -> Option<FunctionInfo> {
    // Step 1: GetFunctionInfo → ClassID, ModuleID, mdMethodDef
    let mut class_id: ClassID = 0;
    let mut module_id: ModuleID = 0;
    let mut method_token: mdToken = 0;

    let hr = profiler_info.GetFunctionInfo(
        function_id,
        &mut class_id,
        &mut module_id,
        &mut method_token,
    );
    if failed(hr) {
        return None;
    }

    // Step 2: GetModuleInfo → AssemblyID
    let mut assembly_id: AssemblyID = 0;
    let hr = profiler_info.GetModuleInfo(
        module_id,
        std::ptr::null_mut(),
        0,
        std::ptr::null_mut(),
        std::ptr::null_mut(),
        &mut assembly_id,
    );
    if failed(hr) {
        return None;
    }

    // Step 3: GetAssemblyInfo → assembly name
    let assembly_name = get_assembly_name(profiler_info, assembly_id)
        .unwrap_or_else(|| String::from("?"));

    // Step 4-6: Resolve method and type names via IMetaDataImport
    let (method_name, type_name) = resolve_method_and_type_names(
        profiler_info, function_id, method_token
    ).unwrap_or_else(|| (format!("token_0x{:08x}", method_token), String::from("?")));

    Some(FunctionInfo {
        assembly_name,
        type_name,
        method_name,
        module_id,
        method_token,
    })
}

/// Get an assembly name from an AssemblyID.
unsafe fn get_assembly_name(
    profiler_info: &ICorProfilerInfo4,
    assembly_id: AssemblyID,
) -> Option<String> {
    // First call to get buffer size
    let mut name_len: ULONG = 0;
    let hr = profiler_info.GetAssemblyInfo(
        assembly_id,
        0,
        &mut name_len,
        std::ptr::null_mut(),
        std::ptr::null_mut(),
        std::ptr::null_mut(),
    );
    if failed(hr) || name_len == 0 {
        return None;
    }

    // Second call to get the actual name
    let mut name_buf = vec![0u16; name_len as usize];
    let hr = profiler_info.GetAssemblyInfo(
        assembly_id,
        name_len,
        std::ptr::null_mut(),
        name_buf.as_mut_ptr(),
        std::ptr::null_mut(),
        std::ptr::null_mut(),
    );
    if failed(hr) {
        return None;
    }

    Some(wchar_to_string(&name_buf))
}

/// Resolve method and type names via IMetaDataImport.
/// Returns (method_name, type_name) or None if resolution fails.
unsafe fn resolve_method_and_type_names(
    profiler_info: &ICorProfilerInfo4,
    function_id: FunctionID,
    method_token: mdMethodDef,
) -> Option<(String, String)> {
    // GetTokenAndMetaDataFromFunction → raw IMetaDataImport pointer
    let mut raw_import: *mut c_void = std::ptr::null_mut();
    let hr = profiler_info.GetTokenAndMetaDataFromFunction(
        function_id,
        &IMetaDataImport::IID,
        &mut raw_import as *mut *mut c_void as *mut *mut IUnknown,
        std::ptr::null_mut(),
    );
    if failed(hr) || raw_import.is_null() {
        return None;
    }

    // The CLR wrote a raw IMetaDataImport* into raw_import.
    // Wrap it as IMetaDataImport — transmute from raw pointer.
    let metadata_import: IMetaDataImport = std::mem::transmute(raw_import);

    // GetMethodProps → method name and type token
    let mut type_def_token: mdTypeDef = 0;
    let mut method_name_len: ULONG = 0;

    let hr = metadata_import.GetMethodProps(
        method_token,
        &mut type_def_token,
        std::ptr::null_mut(),
        0,
        &mut method_name_len,
        std::ptr::null_mut(),
        std::ptr::null_mut(),
        std::ptr::null_mut(),
        std::ptr::null_mut(),
        std::ptr::null_mut(),
    );
    if failed(hr) || method_name_len == 0 {
        return None;
    }

    let mut method_name_buf = vec![0u16; method_name_len as usize];
    let hr = metadata_import.GetMethodProps(
        method_token,
        std::ptr::null_mut(),
        method_name_buf.as_mut_ptr(),
        method_name_len,
        std::ptr::null_mut(),
        std::ptr::null_mut(),
        std::ptr::null_mut(),
        std::ptr::null_mut(),
        std::ptr::null_mut(),
        std::ptr::null_mut(),
    );
    if failed(hr) {
        return None;
    }
    let method_name = wchar_to_string(&method_name_buf);

    // GetTypeDefProps → type/class name
    let type_name = get_type_name(&metadata_import, type_def_token)
        .unwrap_or_else(|| String::from("?"));

    Some((method_name, type_name))
}

/// Get a type name from a mdTypeDef token via IMetaDataImport.
unsafe fn get_type_name(
    metadata_import: &IMetaDataImport,
    type_def_token: mdTypeDef,
) -> Option<String> {
    // First call to get buffer size
    let mut name_len: ULONG = 0;
    let hr = metadata_import.GetTypeDefProps(
        type_def_token,
        std::ptr::null_mut(),
        0,
        &mut name_len,
        std::ptr::null_mut(),
        std::ptr::null_mut(),
    );
    if failed(hr) || name_len == 0 {
        return None;
    }

    // Second call to get the actual name
    let mut name_buf = vec![0u16; name_len as usize];
    let hr = metadata_import.GetTypeDefProps(
        type_def_token,
        name_buf.as_mut_ptr(),
        name_len,
        std::ptr::null_mut(),
        std::ptr::null_mut(),
        std::ptr::null_mut(),
    );
    if failed(hr) {
        return None;
    }

    Some(wchar_to_string(&name_buf))
}

/// Convert a null-terminated WCHAR (UTF-16) buffer to a Rust String.
pub fn wchar_to_string(buf: &[u16]) -> String {
    // Find the null terminator
    let len = buf.iter().position(|&c| c == 0).unwrap_or(buf.len());
    String::from_utf16_lossy(&buf[..len])
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn wchar_to_string_basic() {
        // "Hello" in UTF-16 with null terminator
        let buf: Vec<u16> = vec![0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x00];
        assert_eq!(wchar_to_string(&buf), "Hello");
    }

    #[test]
    fn wchar_to_string_empty() {
        let buf: Vec<u16> = vec![0x00];
        assert_eq!(wchar_to_string(&buf), "");
    }

    #[test]
    fn wchar_to_string_no_null() {
        // Buffer without null terminator — should use full length
        let buf: Vec<u16> = vec![0x41, 0x42, 0x43]; // "ABC"
        assert_eq!(wchar_to_string(&buf), "ABC");
    }

    #[test]
    fn wchar_to_string_dotnet_namespace() {
        // "System.Console" in UTF-16
        let s = "System.Console";
        let buf: Vec<u16> = s.encode_utf16().chain(std::iter::once(0)).collect();
        assert_eq!(wchar_to_string(&buf), "System.Console");
    }
}
