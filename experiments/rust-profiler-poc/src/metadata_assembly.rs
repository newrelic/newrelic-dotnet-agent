// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

//! IMetaDataAssemblyEmit and IMetaDataAssemblyImport COM interface definitions.
//!
//! Derived from Microsoft's cor.h header in the .NET runtime (MIT licensed).
//! Vtable ordering referenced from the Elastic .NET APM agent's Rust profiler
//! (Apache 2.0 licensed) which uses the same `com` crate v0.6.0.
//!
//! IMetaDataAssemblyEmit: Write assembly-level metadata (define assembly refs).
//! IMetaDataAssemblyImport: Read assembly-level metadata (enumerate assembly refs).
//!
//! The profiler uses these to resolve assembly references when building
//! metadata tokens for the IL injection template. On CoreCLR, new assembly
//! references may need to be created (e.g., for System.Runtime, System.Reflection).

use crate::ffi::*;
use com::{
    interfaces::IUnknown,
    sys::HRESULT,
};
use std::ffi::c_void;

/// ASSEMBLYMETADATA structure used by assembly emit/import.
/// Matches the CLR's ASSEMBLYMETADATA layout.
#[repr(C)]
#[derive(Debug, Default)]
pub struct ASSEMBLYMETADATA {
    pub us_major_version: USHORT,
    pub us_minor_version: USHORT,
    pub us_build_number: USHORT,
    pub us_revision_number: USHORT,
    pub sz_locale: *mut WCHAR,
    pub cb_locale: ULONG,
    pub r_processor: *mut DWORD,
    pub ul_processor: ULONG,
    pub r_os: *mut c_void, // OSINFO*
    pub ul_os: ULONG,
}

// Additional metadata token types for assembly operations
pub type mdAssembly = mdToken;
pub type mdAssemblyRef = mdToken;
pub type mdFile = mdToken;
pub type mdExportedType = mdToken;
pub type mdManifestResource = mdToken;

interfaces! {
    /// IMetaDataAssemblyEmit — write assembly-level metadata.
    /// GUID: 211EF15B-5317-4438-B196-DEC87B887693
    ///
    /// 9 methods. DefineAssemblyRef is the key one for the profiler.
    #[uuid("211EF15B-5317-4438-B196-DEC87B887693")]
    pub unsafe interface IMetaDataAssemblyEmit: IUnknown {
        // 1
        pub fn DefineAssembly(&self,
            pbPublicKey: *const c_void,
            cbPublicKey: ULONG,
            ulHashAlgId: ULONG,
            szName: LPCWSTR,
            pMetaData: *const ASSEMBLYMETADATA,
            dwAssemblyFlags: DWORD,
            pmda: *mut mdAssembly,
        ) -> HRESULT;
        // 2 ** Used: create reference to external assembly **
        pub fn DefineAssemblyRef(&self,
            pbPublicKeyOrToken: *const c_void,
            cbPublicKeyOrToken: ULONG,
            szName: LPCWSTR,
            pMetaData: *const ASSEMBLYMETADATA,
            pbHashValue: *const c_void,
            cbHashValue: ULONG,
            dwAssemblyRefFlags: DWORD,
            pmdar: *mut mdAssemblyRef,
        ) -> HRESULT;
        // 3
        pub fn DefineExportedType(&self,
            szName: LPCWSTR,
            tkImplementation: mdToken,
            tkTypeDef: mdTypeDef,
            dwExportedTypeFlags: DWORD,
            pmdct: *mut mdExportedType,
        ) -> HRESULT;
        // 4
        pub fn DefineManifestResource(&self,
            szName: LPCWSTR,
            tkImplementation: mdToken,
            dwOffset: DWORD,
            dwResourceFlags: DWORD,
            pmdmr: *mut mdManifestResource,
        ) -> HRESULT;
        // 5
        pub fn SetAssemblyProps(&self,
            pma: mdAssembly,
            pbPublicKey: *const c_void,
            cbPublicKey: ULONG,
            ulHashAlgId: ULONG,
            szName: LPCWSTR,
            pMetaData: *const ASSEMBLYMETADATA,
            dwAssemblyFlags: DWORD,
        ) -> HRESULT;
        // 6
        pub fn SetAssemblyRefProps(&self,
            ar: mdAssemblyRef,
            pbPublicKeyOrToken: *const c_void,
            cbPublicKeyOrToken: ULONG,
            szName: LPCWSTR,
            pMetaData: *const ASSEMBLYMETADATA,
            pbHashValue: *const c_void,
            cbHashValue: ULONG,
            dwAssemblyRefFlags: DWORD,
        ) -> HRESULT;
        // 7
        pub fn SetFileProps(&self,
            file: mdFile,
            pbHashValue: *const c_void,
            cbHashValue: ULONG,
            dwFileFlags: DWORD,
        ) -> HRESULT;
        // 8
        pub fn SetExportedTypeProps(&self,
            ct: mdExportedType,
            tkImplementation: mdToken,
            tkTypeDef: mdTypeDef,
            dwExportedTypeFlags: DWORD,
        ) -> HRESULT;
        // 9
        pub fn SetManifestResourceProps(&self,
            mr: mdManifestResource,
            tkImplementation: mdToken,
            dwOffset: DWORD,
            dwResourceFlags: DWORD,
        ) -> HRESULT;
    }

    /// IMetaDataAssemblyImport — read assembly-level metadata.
    /// GUID: EE62470B-E94B-424E-9B7C-2F00C9249F93
    ///
    /// 14 methods. EnumAssemblyRefs, GetAssemblyRefProps, and CloseEnum
    /// are the key ones for the profiler.
    ///
    /// Note: CloseEnum returns void (not HRESULT). The com crate requires
    /// HRESULT returns, but we define it with the actual void return since
    /// the Elastic APM agent demonstrates this works with the com crate.
    #[uuid("EE62470B-E94B-424E-9B7C-2F00C9249F93")]
    pub unsafe interface IMetaDataAssemblyImport: IUnknown {
        // 1
        pub fn GetAssemblyProps(&self,
            mda: mdAssembly,
            ppbPublicKey: *mut *mut c_void,
            pcbPublicKey: *mut ULONG,
            pulHashAlgId: *mut ULONG,
            szName: *mut WCHAR,
            cchName: ULONG,
            pchName: *mut ULONG,
            pMetaData: *mut ASSEMBLYMETADATA,
            pdwAssemblyFlags: *mut DWORD,
        ) -> HRESULT;
        // 2 ** Used: get assembly reference properties (name, version, etc.) **
        pub fn GetAssemblyRefProps(&self,
            mdar: mdAssemblyRef,
            ppbPublicKeyOrToken: *mut *mut c_void,
            pcbPublicKeyOrToken: *mut ULONG,
            szName: *mut WCHAR,
            cchName: ULONG,
            pchName: *mut ULONG,
            pMetaData: *mut ASSEMBLYMETADATA,
            ppbHashValue: *mut *mut c_void,
            pcbHashValue: *mut ULONG,
            pdwAssemblyRefFlags: *mut DWORD,
        ) -> HRESULT;
        // 3
        pub fn GetFileProps(&self,
            mdf: mdFile,
            szName: *mut WCHAR,
            cchName: ULONG,
            pchName: *mut ULONG,
            ppbHashValue: *mut *mut c_void,
            pcbHashValue: *mut ULONG,
            pdwFileFlags: *mut DWORD,
        ) -> HRESULT;
        // 4
        pub fn GetExportedTypeProps(&self,
            mdct: mdExportedType,
            szName: *mut WCHAR,
            cchName: ULONG,
            pchName: *mut ULONG,
            ptkImplementation: *mut mdToken,
            ptkTypeDef: *mut mdTypeDef,
            pdwExportedTypeFlags: *mut DWORD,
        ) -> HRESULT;
        // 5
        pub fn GetManifestResourceProps(&self,
            mdmr: mdManifestResource,
            szName: *mut WCHAR,
            cchName: ULONG,
            pchName: *mut ULONG,
            ptkImplementation: *mut mdToken,
            pdwOffset: *mut DWORD,
            pdwResourceFlags: *mut DWORD,
        ) -> HRESULT;
        // 6 ** Used: enumerate assembly references **
        pub fn EnumAssemblyRefs(&self,
            phEnum: *mut HCORENUM,
            rAssemblyRefs: *mut mdAssemblyRef,
            cMax: ULONG,
            pcTokens: *mut ULONG,
        ) -> HRESULT;
        // 7
        pub fn EnumFiles(&self,
            phEnum: *mut HCORENUM,
            rFiles: *mut mdFile,
            cMax: ULONG,
            pcTokens: *mut ULONG,
        ) -> HRESULT;
        // 8
        pub fn EnumExportedTypes(&self,
            phEnum: *mut HCORENUM,
            rExportedTypes: *mut mdExportedType,
            cMax: ULONG,
            pcTokens: *mut ULONG,
        ) -> HRESULT;
        // 9
        pub fn EnumManifestResources(&self,
            phEnum: *mut HCORENUM,
            rManifestResources: *mut mdManifestResource,
            cMax: ULONG,
            pcTokens: *mut ULONG,
        ) -> HRESULT;
        // 10
        pub fn GetAssemblyFromScope(&self, ptkAssembly: *mut mdAssembly) -> HRESULT;
        // 11
        pub fn FindExportedTypeByName(&self,
            szName: LPCWSTR,
            mdtExportedType: mdToken,
            ptkExportedType: *mut mdExportedType,
        ) -> HRESULT;
        // 12
        pub fn FindManifestResourceByName(&self,
            szName: LPCWSTR,
            ptkManifestResource: *mut mdManifestResource,
        ) -> HRESULT;
        // 13 ** Used: close enumeration handle **
        // Note: This actually returns void in the CLR, but we define it as
        // HRESULT for the com crate. The return value is never checked.
        pub fn CloseEnum(&self, hEnum: HCORENUM) -> HRESULT;
        // 14
        pub fn FindAssembliesByName(&self,
            szAppBase: LPCWSTR,
            szPrivateBin: LPCWSTR,
            szAssemblyName: LPCWSTR,
            ppIUnk: *mut *mut IUnknown,
            cMax: ULONG,
            pcAssemblies: *mut ULONG,
        ) -> HRESULT;
    }
}
