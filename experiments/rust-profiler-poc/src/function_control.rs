// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

//! ICorProfilerFunctionControl COM interface definition
//!
//! Derived from Microsoft's corprof.h header in the .NET runtime (MIT licensed).
//! This interface is passed to GetReJITParameters and provides the method
//! for setting new IL bytecode on a method being re-JIT compiled.

use crate::ffi::*;
use com::{
    interfaces::IUnknown,
    sys::HRESULT,
};
use std::ffi::c_void;

interfaces! {
    /// ICorProfilerFunctionControl - controls ReJIT method recompilation.
    /// GUID: F0963021-E1EA-4732-8581-E01B0BD3C0C6
    ///
    /// Passed to GetReJITParameters. The key method is SetILFunctionBody
    /// which replaces the method's IL bytecode during recompilation.
    #[uuid("F0963021-E1EA-4732-8581-E01B0BD3C0C6")]
    pub unsafe interface ICorProfilerFunctionControl: IUnknown {
        // 1
        pub fn SetCodegenFlags(&self, flags: DWORD) -> HRESULT;
        // 2 ** The key method for IL injection **
        pub fn SetILFunctionBody(&self, cbNewILMethodHeader: ULONG, pbNewILMethodHeader: LPCBYTE) -> HRESULT;
        // 3
        pub fn SetILInstrumentedCodeMap(&self, cILMapEntries: ULONG, rgILMapEntries: *const c_void) -> HRESULT;
    }
}
