// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

//! Metadata token resolution for IL injection.
//!
//! Wraps the IMetaDataEmit2, IMetaDataAssemblyEmit, and IMetaDataAssemblyImport
//! COM interfaces to resolve assembly names, type names, and method signatures
//! into metadata tokens that can be embedded in IL bytecode.
//!
//! Rust equivalent of `CorTokenizer.h` (323 lines), including the
//! `CoreCLRCorTokenizer` subclass that maps type names to CoreCLR assembly names.
//!
//! Reference: `src/Agent/NewRelic/Profiler/Profiler/CorTokenizer.h`

use crate::ffi::*;
use crate::il::IlError;
use crate::metadata_assembly::{
    IMetaDataAssemblyEmit, IMetaDataAssemblyImport, ASSEMBLYMETADATA,
};
use crate::metadata_emit::IMetaDataEmit2;
use log::{error, info, trace};
use std::collections::HashMap;
use std::ffi::c_void;

/// Resolves metadata tokens from type/method names for IL injection.
///
/// On CoreCLR, types live in different assemblies than on .NET Framework
/// (e.g., System.Object is in System.Runtime, not mscorlib). The tokenizer
/// handles this mapping transparently.
pub struct Tokenizer {
    metadata_emit: IMetaDataEmit2,
    metadata_assembly_emit: IMetaDataAssemblyEmit,
    metadata_assembly_import: IMetaDataAssemblyImport,
    is_core_clr: bool,
    /// Cache: assembly name → assembly ref token
    assembly_ref_cache: HashMap<String, mdToken>,
    /// Cache: (assembly_ref_token, type_name) → type ref token
    type_ref_cache: HashMap<(mdToken, String), mdToken>,
}

/// CoreCLR type-to-assembly mapping.
///
/// On .NET Core/.NET 5+, types that lived in mscorlib on .NET Framework
/// are split across multiple assemblies. This maps type names to the
/// assembly they live in.
///
/// Reference: `CorTokenizer.h` CoreCLRCorTokenizer lines 265-271.
fn core_clr_assembly_for_type(type_name: &str) -> Option<&'static str> {
    match type_name {
        "System.Object" => Some("System.Runtime"),
        "System.Exception" => Some("System.Runtime"),
        "System.Type" => Some("System.Runtime"),
        "System.RuntimeTypeHandle" => Some("System.Runtime"),
        "System.UInt32" => Some("System.Runtime"),
        "System.UInt64" => Some("System.Runtime"),
        "System.Int32" => Some("System.Runtime"),
        "System.Int64" => Some("System.Runtime"),
        "System.Boolean" => Some("System.Runtime"),
        "System.String" => Some("System.Runtime"),
        "System.Reflection.MethodBase" => Some("System.Runtime"),
        "System.Reflection.MethodInfo" => Some("System.Runtime"),
        "System.Reflection.Assembly" => Some("System.Runtime"),
        "System.Action`2" => Some("System.Runtime"),
        "System.Console" => Some("System.Console"),
        _ => None,
    }
}

impl Tokenizer {
    /// Create a new tokenizer from the metadata COM interfaces.
    pub fn new(
        metadata_emit: IMetaDataEmit2,
        metadata_assembly_emit: IMetaDataAssemblyEmit,
        metadata_assembly_import: IMetaDataAssemblyImport,
        is_core_clr: bool,
    ) -> Self {
        Self {
            metadata_emit,
            metadata_assembly_emit,
            metadata_assembly_import,
            is_core_clr,
            assembly_ref_cache: HashMap::new(),
            type_ref_cache: HashMap::new(),
        }
    }

    /// Get an assembly reference token by name.
    ///
    /// First checks the cache, then enumerates existing assembly refs.
    /// On CoreCLR, if the assembly isn't found, creates a new reference
    /// via DefineAssemblyRef.
    ///
    /// Reference: `CorTokenizer.h` lines 140-175 (base),
    /// `CoreCLRCorTokenizer` lines 279-300.
    pub fn get_assembly_ref_token(&mut self, assembly_name: &str) -> Result<mdToken, IlError> {
        // Check cache first
        if let Some(&token) = self.assembly_ref_cache.get(assembly_name) {
            return Ok(token);
        }

        // Enumerate existing assembly refs to find a match
        if let Some(token) = self.find_existing_assembly_ref(assembly_name)? {
            self.assembly_ref_cache
                .insert(assembly_name.to_string(), token);
            return Ok(token);
        }

        // On CoreCLR, try mapping "mscorlib" to "System.Runtime"
        if self.is_core_clr && assembly_name == "mscorlib" {
            if let Some(token) = self.find_existing_assembly_ref("System.Runtime")? {
                self.assembly_ref_cache
                    .insert(assembly_name.to_string(), token);
                return Ok(token);
            }
            // If System.Runtime not found either, try to create it
            return self.define_assembly_ref("System.Runtime");
        }

        // On CoreCLR, create a new assembly ref if not found
        if self.is_core_clr {
            return self.define_assembly_ref(assembly_name);
        }

        Err(IlError::TokenResolutionFailed(format!(
            "Assembly ref not found: {}",
            assembly_name
        )))
    }

    /// Get a type reference token.
    ///
    /// On CoreCLR, resolves the assembly name via the type-to-assembly map
    /// before calling DefineTypeRefByName.
    ///
    /// Reference: `CorTokenizer.h` lines 48-52 (base),
    /// `CoreCLRCorTokenizer` lines 274-277.
    pub fn get_type_ref_token(
        &mut self,
        assembly_name: &str,
        type_name: &str,
    ) -> Result<mdToken, IlError> {
        // On CoreCLR, resolve the actual assembly for this type
        let resolved_assembly = if self.is_core_clr {
            core_clr_assembly_for_type(type_name).unwrap_or(assembly_name)
        } else {
            assembly_name
        };

        let assembly_ref = self.get_assembly_ref_token(resolved_assembly)?;

        // Check cache
        let cache_key = (assembly_ref, type_name.to_string());
        if let Some(&token) = self.type_ref_cache.get(&cache_key) {
            return Ok(token);
        }

        // Create the type ref
        let type_name_wide = to_wide_string(type_name);
        let mut type_ref_token: mdToken = 0;

        let hr = unsafe {
            self.metadata_emit.DefineTypeRefByName(
                assembly_ref,
                type_name_wide.as_ptr(),
                &mut type_ref_token,
            )
        };

        if failed(hr) {
            return Err(IlError::TokenResolutionFailed(format!(
                "DefineTypeRefByName failed for {} in {}: 0x{:08x}",
                type_name, resolved_assembly, hr
            )));
        }

        trace!(
            "TypeRef: [{}] {} → 0x{:08x}",
            resolved_assembly,
            type_name,
            type_ref_token
        );

        self.type_ref_cache
            .insert(cache_key, type_ref_token);
        Ok(type_ref_token)
    }

    /// Get a member reference token (method or field on a type).
    ///
    /// Reference: `CorTokenizer.h` lines 85-101.
    pub fn get_member_ref_token(
        &mut self,
        parent_token: mdToken,
        method_name: &str,
        signature: &[u8],
    ) -> Result<mdToken, IlError> {
        let method_name_wide = to_wide_string(method_name);
        let mut member_ref_token: mdToken = 0;

        let hr = unsafe {
            self.metadata_emit.DefineMemberRef(
                parent_token,
                method_name_wide.as_ptr(),
                signature.as_ptr(),
                signature.len() as ULONG,
                &mut member_ref_token,
            )
        };

        if failed(hr) {
            return Err(IlError::TokenResolutionFailed(format!(
                "DefineMemberRef failed for {}: 0x{:08x}",
                method_name, hr
            )));
        }

        trace!("MemberRef: {} → 0x{:08x}", method_name, member_ref_token);
        Ok(member_ref_token)
    }

    /// Get a user string token for an embedded string literal.
    ///
    /// Reference: `CorTokenizer.h` lines 118-125.
    pub fn get_string_token(&mut self, string: &str) -> Result<mdToken, IlError> {
        let wide = to_wide_string(string);
        // Length in characters (not including null terminator)
        let char_count = wide.len() - 1; // subtract the null terminator
        let mut string_token: mdToken = 0;

        let hr = unsafe {
            self.metadata_emit.DefineUserString(
                wide.as_ptr(),
                char_count as ULONG,
                &mut string_token,
            )
        };

        if failed(hr) {
            return Err(IlError::TokenResolutionFailed(format!(
                "DefineUserString failed for '{}': 0x{:08x}",
                string, hr
            )));
        }

        trace!("UserString: '{}' → 0x{:08x}", string, string_token);
        Ok(string_token)
    }

    /// Get a TypeSpec token for a generic type instantiation signature.
    ///
    /// Reference: `CorTokenizer.h` lines 78-83.
    pub fn get_type_spec_token(&mut self, signature: &[u8]) -> Result<mdToken, IlError> {
        let mut type_spec_token: mdToken = 0;

        let hr = unsafe {
            self.metadata_emit.GetTokenFromTypeSpec(
                signature.as_ptr(),
                signature.len() as ULONG,
                &mut type_spec_token,
            )
        };

        if failed(hr) {
            return Err(IlError::TokenResolutionFailed(format!(
                "GetTokenFromTypeSpec failed: 0x{:08x}",
                hr
            )));
        }

        trace!("TypeSpec → 0x{:08x}", type_spec_token);
        Ok(type_spec_token)
    }

    /// Get a signature token from a local variable signature blob.
    ///
    /// Reference: `CorTokenizer.h` — used via GetTokenFromSig.
    pub fn get_token_from_signature(&mut self, signature: &[u8]) -> Result<mdToken, IlError> {
        let mut sig_token: mdToken = 0;

        let hr = unsafe {
            self.metadata_emit.GetTokenFromSig(
                signature.as_ptr(),
                signature.len() as ULONG,
                &mut sig_token,
            )
        };

        if failed(hr) {
            return Err(IlError::TokenResolutionFailed(format!(
                "GetTokenFromSig failed: 0x{:08x}",
                hr
            )));
        }

        trace!("Signature → 0x{:08x}", sig_token);
        Ok(sig_token)
    }

    /// Get a MethodSpec token for a generic method instantiation.
    ///
    /// Reference: `CorTokenizer.h` lines 107-116.
    pub fn get_method_spec_token(
        &mut self,
        method_token: mdToken,
        instantiation_signature: &[u8],
    ) -> Result<mdToken, IlError> {
        let mut method_spec_token: mdToken = 0;

        let hr = unsafe {
            self.metadata_emit.DefineMethodSpec(
                method_token,
                instantiation_signature.as_ptr(),
                instantiation_signature.len() as ULONG,
                &mut method_spec_token,
            )
        };

        if failed(hr) {
            return Err(IlError::TokenResolutionFailed(format!(
                "DefineMethodSpec failed: 0x{:08x}",
                hr
            )));
        }

        trace!("MethodSpec → 0x{:08x}", method_spec_token);
        Ok(method_spec_token)
    }

    // ==================== Private helpers ====================

    /// Enumerate existing assembly refs to find one matching the given name.
    fn find_existing_assembly_ref(
        &self,
        assembly_name: &str,
    ) -> Result<Option<mdToken>, IlError> {
        unsafe {
            let mut enum_handle: HCORENUM = std::ptr::null_mut();
            let mut assembly_refs = [0u32; 64];
            let mut count: ULONG = 0;

            let hr = self.metadata_assembly_import.EnumAssemblyRefs(
                &mut enum_handle,
                assembly_refs.as_mut_ptr(),
                64,
                &mut count,
            );

            if failed(hr) {
                return Err(IlError::TokenResolutionFailed(format!(
                    "EnumAssemblyRefs failed: 0x{:08x}",
                    hr
                )));
            }

            for i in 0..count as usize {
                let ref_token = assembly_refs[i];
                let mut name_buf = [0u16; 512];
                let mut name_len: ULONG = 0;

                let hr = self.metadata_assembly_import.GetAssemblyRefProps(
                    ref_token,
                    std::ptr::null_mut(), // ppbPublicKeyOrToken
                    std::ptr::null_mut(), // pcbPublicKeyOrToken
                    name_buf.as_mut_ptr(),
                    512,
                    &mut name_len,
                    std::ptr::null_mut(), // pMetaData
                    std::ptr::null_mut(), // ppbHashValue
                    std::ptr::null_mut(), // pcbHashValue
                    std::ptr::null_mut(), // pdwAssemblyRefFlags
                );

                if failed(hr) {
                    continue;
                }

                let ref_name = wchar_to_string(&name_buf, name_len);
                if ref_name == assembly_name {
                    // Close the enumerator
                    self.metadata_assembly_import.CloseEnum(enum_handle);
                    return Ok(Some(ref_token));
                }
            }

            // Close the enumerator
            if !enum_handle.is_null() {
                self.metadata_assembly_import.CloseEnum(enum_handle);
            }

            Ok(None)
        }
    }

    /// Create a new assembly reference.
    fn define_assembly_ref(&mut self, assembly_name: &str) -> Result<mdToken, IlError> {
        let name_wide = to_wide_string(assembly_name);
        let metadata = ASSEMBLYMETADATA::default();
        let mut assembly_ref_token: mdToken = 0;

        let hr = unsafe {
            self.metadata_assembly_emit.DefineAssemblyRef(
                std::ptr::null(),     // pbPublicKeyOrToken
                0,                    // cbPublicKeyOrToken
                name_wide.as_ptr(),
                &metadata,
                std::ptr::null(),     // pbHashValue
                0,                    // cbHashValue
                0,                    // dwAssemblyRefFlags
                &mut assembly_ref_token,
            )
        };

        if failed(hr) {
            return Err(IlError::TokenResolutionFailed(format!(
                "DefineAssemblyRef failed for {}: 0x{:08x}",
                assembly_name, hr
            )));
        }

        info!(
            "Created assembly ref: {} → 0x{:08x}",
            assembly_name, assembly_ref_token
        );

        self.assembly_ref_cache
            .insert(assembly_name.to_string(), assembly_ref_token);
        Ok(assembly_ref_token)
    }
}

// ==================== String helpers ====================

/// Convert a Rust string to a null-terminated UTF-16 (wide) string.
fn to_wide_string(s: &str) -> Vec<u16> {
    s.encode_utf16().chain(std::iter::once(0)).collect()
}

/// Convert a WCHAR buffer with length to a Rust String.
fn wchar_to_string(buf: &[u16], len: ULONG) -> String {
    let len = len as usize;
    if len == 0 {
        return String::new();
    }
    // Trim null terminator if present
    let slice = if len > 0 && buf[len - 1] == 0 {
        &buf[..len - 1]
    } else {
        &buf[..len]
    };
    String::from_utf16_lossy(slice)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn core_clr_type_resolution() {
        assert_eq!(
            core_clr_assembly_for_type("System.Object"),
            Some("System.Runtime")
        );
        assert_eq!(
            core_clr_assembly_for_type("System.Exception"),
            Some("System.Runtime")
        );
        assert_eq!(
            core_clr_assembly_for_type("System.Type"),
            Some("System.Runtime")
        );
        assert_eq!(
            core_clr_assembly_for_type("System.UInt32"),
            Some("System.Runtime")
        );
        assert_eq!(
            core_clr_assembly_for_type("System.UInt64"),
            Some("System.Runtime")
        );
        assert_eq!(
            core_clr_assembly_for_type("System.Reflection.MethodBase"),
            Some("System.Runtime")
        );
        assert_eq!(
            core_clr_assembly_for_type("System.Action`2"),
            Some("System.Runtime")
        );
        assert_eq!(
            core_clr_assembly_for_type("System.Console"),
            Some("System.Console")
        );
    }

    #[test]
    fn core_clr_unknown_type_returns_none() {
        assert_eq!(core_clr_assembly_for_type("MyApp.MyClass"), None);
        assert_eq!(core_clr_assembly_for_type("System.Custom"), None);
    }

    #[test]
    fn to_wide_string_basic() {
        let wide = to_wide_string("Hello");
        assert_eq!(wide, vec![0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x00]);
    }

    #[test]
    fn to_wide_string_empty() {
        let wide = to_wide_string("");
        assert_eq!(wide, vec![0x00]);
    }

    #[test]
    fn wchar_to_string_with_null() {
        let buf = [0x48u16, 0x69, 0x00]; // "Hi\0"
        assert_eq!(wchar_to_string(&buf, 3), "Hi");
    }

    #[test]
    fn wchar_to_string_without_null() {
        let buf = [0x48u16, 0x69]; // "Hi" (no null)
        assert_eq!(wchar_to_string(&buf, 2), "Hi");
    }

    #[test]
    fn wchar_to_string_empty() {
        let buf = [0u16; 0];
        assert_eq!(wchar_to_string(&buf, 0), "");
    }
}
