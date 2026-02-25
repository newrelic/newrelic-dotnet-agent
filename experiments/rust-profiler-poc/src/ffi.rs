// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

//! FFI type definitions for CLR Profiling API
//!
//! These types match the definitions in corprof.h and cor.h from the .NET runtime.
//! Type aliases use the same names as the C++ headers for easy cross-reference.

#![allow(non_camel_case_types, non_snake_case, non_upper_case_globals)]

use com::sys::{GUID, HRESULT};

// Numeric type aliases matching Windows/CLR conventions
pub type BOOL = i32;
pub type USHORT = u16;
pub type UINT = u32;
pub type ULONG = u32;
pub type DWORD = u32;
pub type BYTE = u8;
pub type WCHAR = u16;
pub type UINT_PTR = usize;
pub type SIZE_T = usize;
pub type LPCBYTE = *const u8;

// CLR-specific ID types (all pointer-sized)
pub type FunctionID = usize;
pub type ClassID = usize;
pub type ModuleID = usize;
pub type ThreadID = usize;
pub type AssemblyID = usize;
pub type AppDomainID = usize;
pub type ProcessID = usize;
pub type ObjectID = usize;
pub type GCHandleID = usize;
pub type ReJITID = usize;

// Metadata token types
pub type mdToken = u32;
pub type mdMethodDef = mdToken;
pub type mdTypeDef = mdToken;
pub type mdTypeRef = mdToken;
pub type mdMemberRef = mdToken;
pub type mdSignature = mdToken;
pub type mdTypeSpec = mdToken;
pub type mdString = mdToken;

// String and signature pointer types
pub type LPCWSTR = *const WCHAR;
pub type PCCOR_SIGNATURE = *const u8;

// Pointer/handle types
pub type HANDLE = *mut std::ffi::c_void;
pub type ULONG32 = u32;
pub type HCORENUM = *mut std::ffi::c_void;

// GUID reference types
pub type REFGUID = *const GUID;
pub type REFIID = *const GUID;

// Additional CLR ID types
pub type ContextID = usize;
pub type mdFieldDef = mdToken;

// Profiling API opaque types (pointer-sized)
pub type COR_PRF_FRAME_INFO = usize;
pub type COR_PRF_ELT_INFO = usize;

// COR_PRF_JIT_CACHE enum
pub type COR_PRF_JIT_CACHE = u32;

// COR_PRF_TRANSITION_REASON enum
pub type COR_PRF_TRANSITION_REASON = u32;

// COR_PRF_SUSPEND_REASON enum
pub type COR_PRF_SUSPEND_REASON = u32;

// COR_PRF_GC_REASON enum
pub type COR_PRF_GC_REASON = u32;

// COR_PRF_GC_ROOT_KIND enum
pub type COR_PRF_GC_ROOT_KIND = u32;

// COR_PRF_GC_ROOT_FLAGS enum
pub type COR_PRF_GC_ROOT_FLAGS = u32;

// COR_PRF_RUNTIME_TYPE enum values
#[repr(u32)]
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum COR_PRF_RUNTIME_TYPE {
    COR_PRF_DESKTOP_CLR = 0x1,
    COR_PRF_CORE_CLR = 0x2,
}

bitflags::bitflags! {
    /// COR_PRF_MONITOR flags for SetEventMask
    pub struct COR_PRF_MONITOR: u32 {
        const COR_PRF_MONITOR_NONE                  = 0x00000000;
        const COR_PRF_MONITOR_FUNCTION_UNLOADS       = 0x00000001;
        const COR_PRF_MONITOR_CLASS_LOADS             = 0x00000002;
        const COR_PRF_MONITOR_MODULE_LOADS            = 0x00000004;
        const COR_PRF_MONITOR_ASSEMBLY_LOADS          = 0x00000008;
        const COR_PRF_MONITOR_APPDOMAIN_LOADS         = 0x00000010;
        const COR_PRF_MONITOR_JIT_COMPILATION         = 0x00000020;
        const COR_PRF_MONITOR_EXCEPTIONS              = 0x00000040;
        const COR_PRF_MONITOR_GC                      = 0x00000080;
        const COR_PRF_MONITOR_OBJECT_ALLOCATED        = 0x00000100;
        const COR_PRF_MONITOR_THREADS                 = 0x00000200;
        const COR_PRF_MONITOR_REMOTING                = 0x00000400;
        const COR_PRF_MONITOR_CODE_TRANSITIONS        = 0x00000800;
        const COR_PRF_MONITOR_ENTERLEAVE              = 0x00001000;
        const COR_PRF_MONITOR_CCW                     = 0x00002000;
        const COR_PRF_MONITOR_REMOTING_COOKIE         = 0x00004000;
        const COR_PRF_MONITOR_REMOTING_ASYNC          = 0x00008000;
        const COR_PRF_MONITOR_SUSPENDS                = 0x00010000;
        const COR_PRF_MONITOR_CACHE_SEARCHES          = 0x00020000;
        const COR_PRF_ENABLE_REJIT                    = 0x00040000;
        const COR_PRF_ENABLE_INPROC_DEBUGGING         = 0x00080000;
        const COR_PRF_ENABLE_JIT_MAPS                 = 0x00100000;
        const COR_PRF_DISABLE_INLINING                = 0x00200000;
        const COR_PRF_DISABLE_OPTIMIZATIONS           = 0x00400000;
        const COR_PRF_ENABLE_OBJECT_ALLOCATED         = 0x00800000;
        const COR_PRF_MONITOR_CLR_EXCEPTIONS          = 0x01000000;
        const COR_PRF_ENABLE_FUNCTION_ARGS            = 0x02000000;
        const COR_PRF_ENABLE_FUNCTION_RETVAL          = 0x04000000;
        const COR_PRF_ENABLE_FRAME_INFO               = 0x08000000;
        const COR_PRF_ENABLE_STACK_SNAPSHOT           = 0x10000000;
        const COR_PRF_USE_PROFILE_IMAGES              = 0x20000000;
        const COR_PRF_DISABLE_TRANSPARENCY_CHECKS_UNDER_FULL_TRUST = 0x40000000;
        const COR_PRF_DISABLE_ALL_NGEN_IMAGES         = 0x80000000;
    }
}

/// Metadata open flags for GetModuleMetaData
pub const OF_READ: DWORD = 0x00000000;
pub const OF_WRITE: DWORD = 0x00000001;

/// HRESULT constants
/// The `com` crate defines HRESULT as i32
pub const S_OK: HRESULT = 0;
pub const S_FALSE: HRESULT = 1;
pub const E_FAIL: HRESULT = 0x80004005_u32 as i32;
pub const E_NOINTERFACE: HRESULT = 0x80004002_u32 as i32;
pub const CORPROF_E_PROFILER_CANCEL_ACTIVATION: HRESULT = 0x80131351_u32 as i32;

/// Check if an HRESULT indicates failure
pub fn failed(hr: HRESULT) -> bool {
    hr < 0
}

/// Check if an HRESULT indicates success
pub fn succeeded(hr: HRESULT) -> bool {
    hr >= 0
}
