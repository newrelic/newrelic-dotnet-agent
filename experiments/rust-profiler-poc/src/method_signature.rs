// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

//! Minimal method signature parser.
//!
//! Parses the blob signature from IMetaDataImport::GetMethodProps to determine
//! key properties needed for IL injection:
//! - Whether the method has a `this` parameter (instance vs static)
//! - Whether the method is generic
//! - The parameter count
//! - Whether the return type is void
//!
//! This is a simplified version of the C++ `SignatureParser` (~1000 lines).
//! Only the minimum needed for the injection template is implemented.
//!
//! Reference: ECMA-335 II.23.2.1 (MethodDefSig), II.23.2.2 (MethodRefSig),
//! C++ `SignatureParser.h` lines 87-100.

use crate::il::sig_compression::uncompress_data;
use crate::il::IlError;

/// Calling convention flag: method has an implicit `this` parameter
const IMAGE_CEE_CS_CALLCONV_HASTHIS: u8 = 0x20;
/// Calling convention flag: method has generic type parameters
const IMAGE_CEE_CS_CALLCONV_GENERIC: u8 = 0x10;

/// ELEMENT_TYPE_VOID
const ELEMENT_TYPE_VOID: u8 = 0x01;

/// Parsed method signature with just the fields needed for IL injection.
#[derive(Debug, Clone)]
pub struct MethodSignature {
    /// True if the method has an implicit `this` parameter (instance method)
    pub has_this: bool,
    /// True if the method has generic type parameters
    pub is_generic: bool,
    /// Number of generic type parameters (0 if not generic)
    pub generic_param_count: u32,
    /// Number of explicit parameters
    pub param_count: u32,
    /// True if the return type is void
    pub return_type_is_void: bool,
    /// The raw signature bytes (for passing through to other APIs)
    pub raw_signature: Vec<u8>,
}

/// Parse a method signature blob.
///
/// Signature format (ECMA-335 II.23.2.1):
/// ```text
/// [calling_convention] [generic_param_count?] [param_count] [return_type] [param_type...]
/// ```
///
/// - Byte 0: Calling convention flags
///   - bit 0x20: HASTHIS (instance method)
///   - bit 0x10: GENERIC (generic method)
/// - If GENERIC: compressed uint = generic parameter count
/// - Compressed uint: parameter count
/// - Return type: starts with ELEMENT_TYPE byte (0x01 = void)
/// - Parameter types: one per param_count (we don't parse these for the POC)
pub fn parse_method_signature(sig_bytes: &[u8]) -> Result<MethodSignature, IlError> {
    if sig_bytes.is_empty() {
        return Err(IlError::InvalidHeader(
            "Empty method signature".to_string(),
        ));
    }

    let calling_convention = sig_bytes[0];
    let has_this = (calling_convention & IMAGE_CEE_CS_CALLCONV_HASTHIS) != 0;
    let is_generic = (calling_convention & IMAGE_CEE_CS_CALLCONV_GENERIC) != 0;

    let mut offset = 1;

    // If generic, read the generic parameter count
    let generic_param_count = if is_generic {
        if offset >= sig_bytes.len() {
            return Err(IlError::UnexpectedEnd);
        }
        let (count, consumed) = uncompress_data(&sig_bytes[offset..])?;
        offset += consumed;
        count
    } else {
        0
    };

    // Read parameter count
    if offset >= sig_bytes.len() {
        return Err(IlError::UnexpectedEnd);
    }
    let (param_count, consumed) = uncompress_data(&sig_bytes[offset..])?;
    offset += consumed;

    // Read return type — just check if it's void
    let return_type_is_void = if offset < sig_bytes.len() {
        sig_bytes[offset] == ELEMENT_TYPE_VOID
    } else {
        // Default to void if no return type byte (shouldn't happen in practice)
        true
    };

    Ok(MethodSignature {
        has_this,
        is_generic,
        generic_param_count,
        param_count,
        return_type_is_void,
        raw_signature: sig_bytes.to_vec(),
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn static_void_no_params() {
        // Calling convention: 0x00 (DEFAULT, no HASTHIS)
        // Param count: 0
        // Return type: VOID (0x01)
        let sig = vec![0x00, 0x00, 0x01];
        let result = parse_method_signature(&sig).unwrap();

        assert!(!result.has_this);
        assert!(!result.is_generic);
        assert_eq!(result.generic_param_count, 0);
        assert_eq!(result.param_count, 0);
        assert!(result.return_type_is_void);
    }

    #[test]
    fn instance_void_no_params() {
        // Calling convention: 0x20 (HASTHIS)
        // Param count: 0
        // Return type: VOID (0x01)
        let sig = vec![0x20, 0x00, 0x01];
        let result = parse_method_signature(&sig).unwrap();

        assert!(result.has_this);
        assert!(!result.is_generic);
        assert_eq!(result.param_count, 0);
        assert!(result.return_type_is_void);
    }

    #[test]
    fn static_int32_two_params() {
        // Calling convention: 0x00 (DEFAULT)
        // Param count: 2
        // Return type: INT32 (0x08)
        // Params: INT32 (0x08), STRING (0x0E)
        let sig = vec![0x00, 0x02, 0x08, 0x08, 0x0E];
        let result = parse_method_signature(&sig).unwrap();

        assert!(!result.has_this);
        assert_eq!(result.param_count, 2);
        assert!(!result.return_type_is_void);
    }

    #[test]
    fn generic_method() {
        // Calling convention: 0x10 (GENERIC)
        // Generic param count: 1
        // Param count: 0
        // Return type: VOID (0x01)
        let sig = vec![0x10, 0x01, 0x00, 0x01];
        let result = parse_method_signature(&sig).unwrap();

        assert!(!result.has_this);
        assert!(result.is_generic);
        assert_eq!(result.generic_param_count, 1);
        assert_eq!(result.param_count, 0);
        assert!(result.return_type_is_void);
    }

    #[test]
    fn instance_generic_method() {
        // Calling convention: 0x30 (HASTHIS | GENERIC)
        // Generic param count: 2
        // Param count: 1
        // Return type: OBJECT (0x1C)
        // Param: INT32 (0x08)
        let sig = vec![0x30, 0x02, 0x01, 0x1C, 0x08];
        let result = parse_method_signature(&sig).unwrap();

        assert!(result.has_this);
        assert!(result.is_generic);
        assert_eq!(result.generic_param_count, 2);
        assert_eq!(result.param_count, 1);
        assert!(!result.return_type_is_void);
    }

    #[test]
    fn realistic_void_method_with_one_param() {
        // Real-world example: void DoSomeWork(string)
        // 0x00 = DEFAULT calling convention
        // 0x01 = 1 parameter
        // 0x01 = return type VOID
        // 0x0E = parameter type STRING
        let sig = vec![0x00, 0x01, 0x01, 0x0E];
        let result = parse_method_signature(&sig).unwrap();

        assert!(!result.has_this);
        assert_eq!(result.param_count, 1);
        assert!(result.return_type_is_void);
    }

    #[test]
    fn empty_signature_fails() {
        assert!(parse_method_signature(&[]).is_err());
    }

    #[test]
    fn truncated_signature_fails() {
        // Just calling convention, no param count
        assert!(parse_method_signature(&[0x00]).is_err());
    }

    #[test]
    fn raw_signature_preserved() {
        let sig = vec![0x20, 0x02, 0x01, 0x08, 0x0E];
        let result = parse_method_signature(&sig).unwrap();
        assert_eq!(result.raw_signature, sig);
    }
}
