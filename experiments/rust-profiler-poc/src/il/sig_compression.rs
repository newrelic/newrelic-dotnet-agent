// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

//! ECMA-335 compressed integer and token encoding.
//!
//! The CLR metadata system uses a variable-length encoding for unsigned integers
//! and metadata tokens. This module implements the compression/decompression
//! routines defined in ECMA-335 II.23.2.
//!
//! Reference: C++ `Sicily/codegen/ByteCodeGenerator.h` CorSigCompressData/CorSigUncompressData.

use super::IlError;

/// Maximum value that can be compressed: 0x1FFFFFFF
const MAX_COMPRESSED_VALUE: u32 = 0x1FFF_FFFF;

/// Compress an unsigned integer per ECMA-335 II.23.2.
///
/// Encoding:
/// - 0x00..=0x7F: 1 byte (value as-is)
/// - 0x80..=0x3FFF: 2 bytes (high bit 0x80 set, value split across bytes)
/// - 0x4000..=0x1FFFFFFF: 4 bytes (high bits 0xC0 set, value split across bytes)
///
/// Returns an error if the value exceeds the maximum compressible range.
pub fn compress_data(value: u32) -> Result<Vec<u8>, IlError> {
    if value <= 0x7F {
        Ok(vec![value as u8])
    } else if value <= 0x3FFF {
        Ok(vec![
            ((value >> 8) as u8) | 0x80,
            (value & 0xFF) as u8,
        ])
    } else if value <= MAX_COMPRESSED_VALUE {
        Ok(vec![
            ((value >> 24) as u8) | 0xC0,
            ((value >> 16) & 0xFF) as u8,
            ((value >> 8) & 0xFF) as u8,
            (value & 0xFF) as u8,
        ])
    } else {
        Err(IlError::CompressionOverflow(value))
    }
}

/// Decompress an unsigned integer per ECMA-335 II.23.2.
///
/// Returns `(value, bytes_consumed)` or an error if the input is too short.
pub fn uncompress_data(bytes: &[u8]) -> Result<(u32, usize), IlError> {
    if bytes.is_empty() {
        return Err(IlError::UnexpectedEnd);
    }

    let first = bytes[0];

    if (first & 0x80) == 0 {
        // 1-byte encoding: 0x00..0x7F
        Ok((first as u32, 1))
    } else if (first & 0xC0) == 0x80 {
        // 2-byte encoding: 0x80..0x3FFF
        if bytes.len() < 2 {
            return Err(IlError::UnexpectedEnd);
        }
        let value = (((first & 0x3F) as u32) << 8) | (bytes[1] as u32);
        Ok((value, 2))
    } else if (first & 0xE0) == 0xC0 {
        // 4-byte encoding: 0x4000..0x1FFFFFFF
        if bytes.len() < 4 {
            return Err(IlError::UnexpectedEnd);
        }
        let value = (((first & 0x1F) as u32) << 24)
            | ((bytes[1] as u32) << 16)
            | ((bytes[2] as u32) << 8)
            | (bytes[3] as u32);
        Ok((value, 4))
    } else {
        Err(IlError::InvalidHeader(format!(
            "Invalid compressed data encoding: 0x{:02x}",
            first
        )))
    }
}

/// Compress a metadata token per ECMA-335 TypeDefOrRefOrSpecEncoded.
///
/// Rotates the table index (high byte) into the low 2 bits:
/// - TypeDef  (0x02) → tag 0
/// - TypeRef  (0x01) → tag 1
/// - TypeSpec (0x1B) → tag 2
///
/// The row index is shifted left by 2 bits, and the tag is OR'd in.
pub fn compress_token(token: u32) -> Result<Vec<u8>, IlError> {
    let table = (token >> 24) & 0xFF;
    let row = token & 0x00FF_FFFF;

    let tag = match table {
        0x02 => 0u32, // TypeDef
        0x01 => 1u32, // TypeRef
        0x1B => 2u32, // TypeSpec
        0x00 => 3u32, // BaseType (used by some signatures)
        _ => {
            return Err(IlError::TokenResolutionFailed(format!(
                "Unsupported token table 0x{:02x} for compression",
                table
            )));
        }
    };

    let compressed_value = (row << 2) | tag;
    compress_data(compressed_value)
}

/// Decompress a metadata token per ECMA-335 TypeDefOrRefOrSpecEncoded.
///
/// Returns `(token, bytes_consumed)` where token has the full table index in the high byte.
pub fn uncompress_token(bytes: &[u8]) -> Result<(u32, usize), IlError> {
    let (compressed_value, consumed) = uncompress_data(bytes)?;

    let tag = compressed_value & 0x03;
    let row = compressed_value >> 2;

    let table = match tag {
        0 => 0x02u32, // TypeDef
        1 => 0x01u32, // TypeRef
        2 => 0x1Bu32, // TypeSpec
        3 => 0x00u32, // BaseType
        _ => unreachable!(), // tag is 2 bits, values 0-3 are exhaustive
    };

    let token = (table << 24) | row;
    Ok((token, consumed))
}

#[cfg(test)]
mod tests {
    use super::*;

    // ==================== compress_data ====================

    #[test]
    fn compress_data_zero() {
        assert_eq!(compress_data(0).unwrap(), vec![0x00]);
    }

    #[test]
    fn compress_data_single_byte_max() {
        assert_eq!(compress_data(0x7F).unwrap(), vec![0x7F]);
    }

    #[test]
    fn compress_data_two_byte_min() {
        // 0x80 → [0x80, 0x80]
        assert_eq!(compress_data(0x80).unwrap(), vec![0x80, 0x80]);
    }

    #[test]
    fn compress_data_two_byte_max() {
        // 0x3FFF → [0xBF, 0xFF]
        assert_eq!(compress_data(0x3FFF).unwrap(), vec![0xBF, 0xFF]);
    }

    #[test]
    fn compress_data_four_byte_min() {
        // 0x4000 → [0xC0, 0x00, 0x40, 0x00]
        assert_eq!(compress_data(0x4000).unwrap(), vec![0xC0, 0x00, 0x40, 0x00]);
    }

    #[test]
    fn compress_data_four_byte_max() {
        // 0x1FFFFFFF → [0xDF, 0xFF, 0xFF, 0xFF]
        assert_eq!(
            compress_data(0x1FFF_FFFF).unwrap(),
            vec![0xDF, 0xFF, 0xFF, 0xFF]
        );
    }

    #[test]
    fn compress_data_overflow() {
        assert!(compress_data(0x2000_0000).is_err());
    }

    #[test]
    fn compress_data_common_values() {
        // 1 → single byte
        assert_eq!(compress_data(1).unwrap(), vec![0x01]);
        // 3 → single byte (common local count)
        assert_eq!(compress_data(3).unwrap(), vec![0x03]);
        // 11 → single byte (array size for GetFinishTracerDelegate)
        assert_eq!(compress_data(11).unwrap(), vec![0x0B]);
    }

    // ==================== uncompress_data ====================

    #[test]
    fn uncompress_data_single_byte() {
        assert_eq!(uncompress_data(&[0x00]).unwrap(), (0, 1));
        assert_eq!(uncompress_data(&[0x7F]).unwrap(), (0x7F, 1));
        assert_eq!(uncompress_data(&[0x03]).unwrap(), (3, 1));
    }

    #[test]
    fn uncompress_data_two_byte() {
        assert_eq!(uncompress_data(&[0x80, 0x80]).unwrap(), (0x80, 2));
        assert_eq!(uncompress_data(&[0xBF, 0xFF]).unwrap(), (0x3FFF, 2));
    }

    #[test]
    fn uncompress_data_four_byte() {
        assert_eq!(
            uncompress_data(&[0xC0, 0x00, 0x40, 0x00]).unwrap(),
            (0x4000, 4)
        );
        assert_eq!(
            uncompress_data(&[0xDF, 0xFF, 0xFF, 0xFF]).unwrap(),
            (0x1FFF_FFFF, 4)
        );
    }

    #[test]
    fn uncompress_data_empty_input() {
        assert!(uncompress_data(&[]).is_err());
    }

    #[test]
    fn uncompress_data_truncated_two_byte() {
        assert!(uncompress_data(&[0x80]).is_err());
    }

    #[test]
    fn uncompress_data_truncated_four_byte() {
        assert!(uncompress_data(&[0xC0, 0x00]).is_err());
    }

    // ==================== Round-trip tests ====================

    #[test]
    fn compress_uncompress_round_trip() {
        let test_values: Vec<u32> = vec![
            0, 1, 0x7F, 0x80, 0xFF, 0x100, 0x3FFF, 0x4000, 0xFFFF, 0x10000, 0x1FFF_FFFF,
        ];
        for value in test_values {
            let compressed = compress_data(value).unwrap();
            let (decompressed, consumed) = uncompress_data(&compressed).unwrap();
            assert_eq!(
                decompressed, value,
                "Round-trip failed for 0x{:x}",
                value
            );
            assert_eq!(
                consumed,
                compressed.len(),
                "Consumed bytes mismatch for 0x{:x}",
                value
            );
        }
    }

    // ==================== compress_token ====================

    #[test]
    fn compress_typedef_token() {
        // TypeDef 0x02000001 → tag 0, row 1 → compressed value = (1 << 2) | 0 = 4
        let result = compress_token(0x0200_0001).unwrap();
        assert_eq!(result, vec![0x04]);
    }

    #[test]
    fn compress_typeref_token() {
        // TypeRef 0x01000042 → tag 1, row 0x42 → compressed value = (0x42 << 2) | 1 = 0x109
        let result = compress_token(0x0100_0042).unwrap();
        let (decompressed, _) = uncompress_data(&result).unwrap();
        assert_eq!(decompressed, (0x42 << 2) | 1);
    }

    #[test]
    fn compress_typespec_token() {
        // TypeSpec 0x1B000003 → tag 2, row 3 → compressed value = (3 << 2) | 2 = 14
        let result = compress_token(0x1B00_0003).unwrap();
        assert_eq!(result, vec![0x0E]);
    }

    #[test]
    fn compress_token_unsupported_table() {
        // MethodDef table (0x06) is not supported for TypeDefOrRefOrSpec encoding
        assert!(compress_token(0x0600_0001).is_err());
    }

    // ==================== uncompress_token ====================

    #[test]
    fn uncompress_typedef_token() {
        // compressed value 4 → tag 0 (TypeDef), row 1 → 0x02000001
        let (token, consumed) = uncompress_token(&[0x04]).unwrap();
        assert_eq!(token, 0x0200_0001);
        assert_eq!(consumed, 1);
    }

    #[test]
    fn uncompress_typespec_token() {
        // compressed value 14 → tag 2 (TypeSpec), row 3 → 0x1B000003
        let (token, consumed) = uncompress_token(&[0x0E]).unwrap();
        assert_eq!(token, 0x1B00_0003);
        assert_eq!(consumed, 1);
    }

    #[test]
    fn token_round_trip() {
        let tokens: Vec<u32> = vec![
            0x0200_0001, // TypeDef row 1
            0x0100_0001, // TypeRef row 1
            0x0100_0042, // TypeRef row 66
            0x1B00_0003, // TypeSpec row 3
            0x0200_0100, // TypeDef row 256 (needs 2-byte compressed)
        ];
        for token in tokens {
            let compressed = compress_token(token).unwrap();
            let (decompressed, consumed) = uncompress_token(&compressed).unwrap();
            assert_eq!(
                decompressed, token,
                "Token round-trip failed for 0x{:08x}",
                token
            );
            assert_eq!(consumed, compressed.len());
        }
    }
}
