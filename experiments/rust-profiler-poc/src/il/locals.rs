// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

//! Local variable signature construction (ECMA-335 II.23.2.6).
//!
//! Every .NET method can have local variables described by a LOCAL_SIG blob.
//! Format: `0x07` (LOCAL_SIG header), compressed count, followed by type
//! signatures for each local variable.
//!
//! During instrumentation, the profiler adds new locals (tracer object,
//! exception, and optionally return value) to the method's existing signature.
//!
//! Reference: `FunctionManipulator.h` lines 196-470, `IncrementLocalCount` lines 452-471.

use super::sig_compression::{compress_data, compress_token, uncompress_data};
use super::IlError;

/// LOCAL_SIG header byte per ECMA-335 II.23.2.6
const LOCAL_SIG_HEADER: u8 = 0x07;

/// ELEMENT_TYPE_CLASS — reference to a class type
const ELEMENT_TYPE_CLASS: u8 = 0x12;

/// Builds and modifies local variable signature blobs.
///
/// The signature tracks the raw bytes and provides methods to append new
/// local variable types and update the compressed count.
#[derive(Debug, Clone)]
pub struct LocalVariableSignature {
    bytes: Vec<u8>,
}

impl LocalVariableSignature {
    /// Create a new empty local variable signature: `[0x07, 0x00]`.
    ///
    /// This represents a method with zero local variables.
    pub fn new() -> Self {
        Self {
            bytes: vec![LOCAL_SIG_HEADER, 0x00],
        }
    }

    /// Create a signature from existing bytes (e.g., from GetSignatureFromToken).
    pub fn from_existing(bytes: Vec<u8>) -> Result<Self, IlError> {
        if bytes.is_empty() {
            return Err(IlError::InvalidHeader(
                "Empty local variable signature".to_string(),
            ));
        }
        if bytes[0] != LOCAL_SIG_HEADER {
            return Err(IlError::InvalidHeader(format!(
                "Expected LOCAL_SIG header 0x07, got 0x{:02x}",
                bytes[0]
            )));
        }
        Ok(Self { bytes })
    }

    /// Append a local variable with the given type bytes.
    ///
    /// Returns the index of the new local variable (0-based).
    /// The compressed count in the signature is incremented.
    ///
    /// Reference: `FunctionManipulator::AppendToLocalsSignature` at
    /// `FunctionManipulator.h` lines 442-450.
    pub fn append_type(&mut self, type_bytes: &[u8]) -> Result<u16, IlError> {
        // Append the type bytes to the end of the signature
        self.bytes.extend_from_slice(type_bytes);
        // Increment the count and return the new index
        self.increment_count()
    }

    /// Append a local of type `class <token>` (ELEMENT_TYPE_CLASS + compressed token).
    ///
    /// This is the encoding for reference types like System.Object and System.Exception.
    /// Returns the index of the new local variable.
    pub fn append_class_type(&mut self, class_token: u32) -> Result<u16, IlError> {
        let compressed = compress_token(class_token)?;
        let mut type_bytes = vec![ELEMENT_TYPE_CLASS];
        type_bytes.extend_from_slice(&compressed);
        self.append_type(&type_bytes)
    }

    /// Get the raw signature bytes.
    pub fn get_bytes(&self) -> &[u8] {
        &self.bytes
    }

    /// Consume the signature and return the raw bytes.
    pub fn into_bytes(self) -> Vec<u8> {
        self.bytes
    }

    /// Get the current number of local variables.
    pub fn count(&self) -> Result<u32, IlError> {
        if self.bytes.len() < 2 {
            return Err(IlError::UnexpectedEnd);
        }
        let (count, _) = uncompress_data(&self.bytes[1..])?;
        Ok(count)
    }

    /// Increment the local count and return the index of the new local.
    ///
    /// This modifies the compressed count in-place, handling the case where
    /// the compressed size changes (e.g., from 1 byte to 2 bytes when
    /// crossing the 0x80 boundary).
    ///
    /// Reference: `FunctionManipulator::IncrementLocalCount` at
    /// `FunctionManipulator.h` lines 452-471.
    fn increment_count(&mut self) -> Result<u16, IlError> {
        // Parse existing count starting at offset 1 (after 0x07 header)
        let (old_count, old_count_bytes) = uncompress_data(&self.bytes[1..])?;

        if old_count >= 0xFFFE {
            return Err(IlError::GenerationError(
                "Local variable count overflow".to_string(),
            ));
        }

        let new_count = old_count + 1;
        let new_count_compressed = compress_data(new_count)?;

        // Replace the old compressed count with the new one
        // Range to replace: bytes[1..1+old_count_bytes]
        let replace_start = 1;
        let replace_end = 1 + old_count_bytes;

        // Remove old count bytes and insert new ones
        self.bytes
            .splice(replace_start..replace_end, new_count_compressed);

        // Return the index of the newly added local (0-based)
        Ok((new_count - 1) as u16)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn new_empty_signature() {
        let sig = LocalVariableSignature::new();
        assert_eq!(sig.get_bytes(), &[0x07, 0x00]);
        assert_eq!(sig.count().unwrap(), 0);
    }

    #[test]
    fn append_one_local() {
        let mut sig = LocalVariableSignature::new();
        // Append a System.Object type (class + compressed token)
        // Using a simple type byte for testing
        let index = sig.append_type(&[0x1C]).unwrap(); // ELEMENT_TYPE_OBJECT
        assert_eq!(index, 0); // First local is index 0
        assert_eq!(sig.count().unwrap(), 1);
        assert_eq!(sig.get_bytes(), &[0x07, 0x01, 0x1C]);
    }

    #[test]
    fn append_three_locals() {
        let mut sig = LocalVariableSignature::new();

        let idx0 = sig.append_type(&[0x1C]).unwrap(); // object
        let idx1 = sig.append_type(&[0x12, 0x04]).unwrap(); // class token
        let idx2 = sig.append_type(&[0x08]).unwrap(); // int32

        assert_eq!(idx0, 0);
        assert_eq!(idx1, 1);
        assert_eq!(idx2, 2);
        assert_eq!(sig.count().unwrap(), 3);

        // Verify bytes: 0x07, 0x03, 0x1C, 0x12, 0x04, 0x08
        assert_eq!(sig.get_bytes(), &[0x07, 0x03, 0x1C, 0x12, 0x04, 0x08]);
    }

    #[test]
    fn append_class_type_with_typeref_token() {
        let mut sig = LocalVariableSignature::new();
        // TypeRef token 0x01000001 → compressed: (1 << 2) | 1 = 5 → [0x05]
        let idx = sig.append_class_type(0x0100_0001).unwrap();
        assert_eq!(idx, 0);
        // Bytes: 0x07, 0x01, 0x12 (CLASS), 0x05 (compressed token)
        assert_eq!(sig.get_bytes(), &[0x07, 0x01, 0x12, 0x05]);
    }

    #[test]
    fn append_to_existing_signature() {
        // Existing signature: 2 locals (int32, string)
        let existing = vec![0x07, 0x02, 0x08, 0x0E];
        let mut sig = LocalVariableSignature::from_existing(existing).unwrap();
        assert_eq!(sig.count().unwrap(), 2);

        let idx = sig.append_type(&[0x1C]).unwrap(); // object
        assert_eq!(idx, 2); // Third local (0-based)
        assert_eq!(sig.count().unwrap(), 3);

        // Verify: 0x07, 0x03, 0x08, 0x0E, 0x1C
        assert_eq!(sig.get_bytes(), &[0x07, 0x03, 0x08, 0x0E, 0x1C]);
    }

    #[test]
    fn count_boundary_crossing_to_two_byte() {
        // Start with 127 locals (max for 1-byte compressed count)
        let mut sig = LocalVariableSignature::new();

        // Manually set count to 127: 0x07, 0x7F
        sig.bytes = vec![0x07, 0x7F];
        // Add dummy type bytes for 127 locals
        for _ in 0..127 {
            sig.bytes.push(0x08); // int32 for each
        }

        assert_eq!(sig.count().unwrap(), 127);

        // Append one more — count should go to 128, requiring 2-byte encoding
        let idx = sig.append_type(&[0x08]).unwrap();
        assert_eq!(idx, 127); // 128th local, 0-based index 127
        assert_eq!(sig.count().unwrap(), 128);

        // Count 128 compressed: [0x80, 0x80]
        assert_eq!(sig.bytes[0], 0x07);
        assert_eq!(sig.bytes[1], 0x80);
        assert_eq!(sig.bytes[2], 0x80);
    }

    #[test]
    fn from_existing_invalid_header() {
        // Missing LOCAL_SIG marker
        assert!(LocalVariableSignature::from_existing(vec![0x06, 0x00]).is_err());
    }

    #[test]
    fn from_existing_empty() {
        assert!(LocalVariableSignature::from_existing(vec![]).is_err());
    }

    #[test]
    fn into_bytes_consumes_signature() {
        let mut sig = LocalVariableSignature::new();
        sig.append_type(&[0x08]).unwrap();
        let bytes = sig.into_bytes();
        assert_eq!(bytes, vec![0x07, 0x01, 0x08]);
    }
}
