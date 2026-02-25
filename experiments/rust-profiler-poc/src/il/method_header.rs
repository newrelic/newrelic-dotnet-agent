// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

//! IL method header parsing and writing (tiny/fat formats).
//!
//! Every .NET method has an IL header that describes its code size, max stack
//! depth, local variables, and exception handlers. The CLR supports two formats:
//!
//! **Tiny header** (1 byte): For simple methods with ≤63 bytes of code, no locals,
//! no exception handlers, and max stack ≤8. Format: `bits[1:0]=0x2`, `bits[7:2]=code_size`.
//!
//! **Fat header** (12 bytes): For complex methods. Contains flags, max stack,
//! code size, and local variable signature token.
//!
//! During instrumentation, tiny headers are always converted to fat headers
//! because we need local variables and exception handlers.
//!
//! Reference: `FunctionManipulator.h` lines 134-194, ECMA-335 II.25.4.

use super::IlError;

// ==================== Header format flags ====================

/// Tiny header format marker (bits[1:0] = 0x2)
const TINY_FORMAT: u8 = 0x02;
/// Mask for testing tiny format
const TINY_FORMAT_MASK: u8 = 0x03;

/// Fat header format flag
pub const COR_ILMETHOD_FAT_FORMAT: u16 = 0x0003;
/// InitLocals flag — CLR zero-initializes local variables
pub const COR_ILMETHOD_INIT_LOCALS: u16 = 0x0010;
/// MoreSects flag — extra data sections follow the code (exception handlers)
pub const COR_ILMETHOD_MORE_SECTS: u16 = 0x0008;

/// Standard fat header size in bytes
pub const FAT_HEADER_SIZE: usize = 12;
/// Fat header size in DWORDs (always 3)
pub const FAT_HEADER_DWORDS: u16 = 3;

// ==================== Header types ====================

/// A fat IL method header (12 bytes).
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct FatHeader {
    /// Flags: FatFormat, InitLocals, MoreSects
    pub flags: u16,
    /// Header size in DWORDs (always 3 for standard fat header)
    pub size: u16,
    /// Maximum evaluation stack depth
    pub max_stack: u16,
    /// IL code size in bytes
    pub code_size: u32,
    /// Metadata token for local variable signature (0 if no locals)
    pub local_var_sig_tok: u32,
}

/// Parsed result from extracting a method's IL.
#[derive(Debug)]
pub struct ParsedMethod {
    /// The method header (converted to fat if originally tiny)
    pub header: FatHeader,
    /// The original code bytes (method body)
    pub code: Vec<u8>,
    /// Extra section bytes (exception handler tables), if any
    pub extra_sections: Option<Vec<u8>>,
}

// ==================== Parsing ====================

/// Parse a complete IL method from raw bytes.
///
/// Returns the header (converted to fat if necessary), code bytes, and
/// optional extra section bytes. The header is always returned as fat
/// because instrumentation requires locals and exception handlers.
///
/// Reference: `FunctionManipulator::ExtractHeaderBodyAndExtra` at
/// `FunctionManipulator.h` lines 134-194.
pub fn parse_method(bytes: &[u8]) -> Result<ParsedMethod, IlError> {
    if bytes.is_empty() {
        return Err(IlError::InvalidHeader("Empty method bytes".to_string()));
    }

    let first_byte = bytes[0];

    // Check fat format first (bits[1:0] of the u16 flags)
    // Fat format: read first two bytes as u16 LE, check bits[1:0] == 0x3
    if bytes.len() >= 2 {
        let flags_word = u16::from_le_bytes([bytes[0], bytes[1]]);
        if (flags_word & 0x0003) == COR_ILMETHOD_FAT_FORMAT {
            return parse_fat_method(bytes);
        }
    }

    // Check tiny format: bits[1:0] == 0x2
    if (first_byte & TINY_FORMAT_MASK) == TINY_FORMAT {
        return parse_tiny_method(bytes);
    }

    Err(IlError::InvalidHeader(format!(
        "Unrecognized header format: first byte 0x{:02x}",
        first_byte
    )))
}

/// Parse a tiny-format method and convert to fat header.
fn parse_tiny_method(bytes: &[u8]) -> Result<ParsedMethod, IlError> {
    let code_size = (bytes[0] >> 2) as u32;
    let code_offset = 1usize; // Tiny header is 1 byte

    if bytes.len() < code_offset + code_size as usize {
        return Err(IlError::InvalidHeader(format!(
            "Tiny method: code_size={} but only {} bytes available",
            code_size,
            bytes.len() - code_offset
        )));
    }

    let code = bytes[code_offset..code_offset + code_size as usize].to_vec();

    // Convert to fat header with defaults
    let header = tiny_to_fat(code_size);

    Ok(ParsedMethod {
        header,
        code,
        extra_sections: None, // Tiny methods never have extra sections
    })
}

/// Parse a fat-format method.
fn parse_fat_method(bytes: &[u8]) -> Result<ParsedMethod, IlError> {
    if bytes.len() < FAT_HEADER_SIZE {
        return Err(IlError::InvalidHeader(format!(
            "Fat header requires {} bytes, got {}",
            FAT_HEADER_SIZE,
            bytes.len()
        )));
    }

    let header = parse_fat_header(bytes)?;

    // Header size in bytes
    let header_size_bytes = (header.size as usize) * 4;
    let code_offset = header_size_bytes;
    let code_end = code_offset + header.code_size as usize;

    if bytes.len() < code_end {
        return Err(IlError::InvalidHeader(format!(
            "Fat method: code_size={} but only {} bytes available after header",
            header.code_size,
            bytes.len() - code_offset
        )));
    }

    let code = bytes[code_offset..code_end].to_vec();

    // Check for extra sections (exception handler tables)
    let extra_sections = if (header.flags & COR_ILMETHOD_MORE_SECTS) != 0 {
        // Extra sections must be 4-byte aligned after code
        let mut extra_offset = code_end;
        while (extra_offset % 4) != 0 {
            extra_offset += 1;
        }

        if extra_offset < bytes.len() {
            Some(bytes[extra_offset..].to_vec())
        } else {
            None
        }
    } else {
        None
    };

    Ok(ParsedMethod {
        header,
        code,
        extra_sections,
    })
}

/// Parse a fat header from the first 12 bytes.
pub fn parse_fat_header(bytes: &[u8]) -> Result<FatHeader, IlError> {
    if bytes.len() < FAT_HEADER_SIZE {
        return Err(IlError::InvalidHeader(format!(
            "Need {} bytes for fat header, got {}",
            FAT_HEADER_SIZE,
            bytes.len()
        )));
    }

    // Bytes 0-1: flags (low 12 bits) and size (high 4 bits) as u16 LE
    let flags_and_size = u16::from_le_bytes([bytes[0], bytes[1]]);
    let flags = flags_and_size & 0x0FFF;
    let size = (flags_and_size >> 12) & 0x0F;

    // Bytes 2-3: max stack
    let max_stack = u16::from_le_bytes([bytes[2], bytes[3]]);

    // Bytes 4-7: code size
    let code_size = u32::from_le_bytes([bytes[4], bytes[5], bytes[6], bytes[7]]);

    // Bytes 8-11: local var sig tok
    let local_var_sig_tok = u32::from_le_bytes([bytes[8], bytes[9], bytes[10], bytes[11]]);

    Ok(FatHeader {
        flags,
        size,
        max_stack,
        code_size,
        local_var_sig_tok,
    })
}

// ==================== Construction ====================

/// Convert a tiny header to a fat header with default settings.
///
/// Sets: FatFormat | InitLocals, size=3, max_stack=8, local_var_sig_tok=0.
/// Reference: `FunctionManipulator.h` lines 181-187.
pub fn tiny_to_fat(code_size: u32) -> FatHeader {
    FatHeader {
        flags: COR_ILMETHOD_FAT_FORMAT | COR_ILMETHOD_INIT_LOCALS,
        size: FAT_HEADER_DWORDS,
        max_stack: 8,
        code_size,
        local_var_sig_tok: 0,
    }
}

/// Serialize a fat header to 12 bytes.
///
/// Layout: bytes 0-1 = flags | (size << 12), bytes 2-3 = max_stack,
/// bytes 4-7 = code_size (LE), bytes 8-11 = local_var_sig_tok (LE).
pub fn write_fat_header(header: &FatHeader) -> [u8; FAT_HEADER_SIZE] {
    let mut bytes = [0u8; FAT_HEADER_SIZE];

    // Bytes 0-1: flags (low 12 bits) | size (high 4 bits)
    let flags_and_size = (header.flags & 0x0FFF) | ((header.size & 0x0F) << 12);
    let fs_bytes = flags_and_size.to_le_bytes();
    bytes[0] = fs_bytes[0];
    bytes[1] = fs_bytes[1];

    // Bytes 2-3: max stack
    let ms_bytes = header.max_stack.to_le_bytes();
    bytes[2] = ms_bytes[0];
    bytes[3] = ms_bytes[1];

    // Bytes 4-7: code size
    let cs_bytes = header.code_size.to_le_bytes();
    bytes[4] = cs_bytes[0];
    bytes[5] = cs_bytes[1];
    bytes[6] = cs_bytes[2];
    bytes[7] = cs_bytes[3];

    // Bytes 8-11: local var sig tok
    let lv_bytes = header.local_var_sig_tok.to_le_bytes();
    bytes[8] = lv_bytes[0];
    bytes[9] = lv_bytes[1];
    bytes[10] = lv_bytes[2];
    bytes[11] = lv_bytes[3];

    bytes
}

/// Build a complete instrumented method: fat header + code + padded extra sections.
///
/// The code bytes and extra section bytes are provided separately; this function
/// handles 4-byte alignment padding between code and extra sections.
pub fn build_method_bytes(
    header: &FatHeader,
    code: &[u8],
    extra_section: Option<&[u8]>,
) -> Vec<u8> {
    let header_bytes = write_fat_header(header);
    let mut result = Vec::with_capacity(
        FAT_HEADER_SIZE + code.len() + extra_section.map_or(0, |s| s.len() + 4),
    );

    result.extend_from_slice(&header_bytes);
    result.extend_from_slice(code);

    if let Some(extra) = extra_section {
        // Pad to 4-byte boundary
        while result.len() % 4 != 0 {
            result.push(0x00);
        }
        result.extend_from_slice(extra);
    }

    result
}

#[cfg(test)]
mod tests {
    use super::*;

    // ==================== Tiny header parsing ====================

    #[test]
    fn parse_tiny_header_single_ret() {
        // Tiny header: code_size=1, format=0x2 → byte = (1 << 2) | 0x2 = 0x06
        // Code: 0x2A (ret)
        let bytes = vec![0x06, 0x2A];
        let result = parse_method(&bytes).unwrap();

        assert_eq!(result.code, vec![0x2A]);
        assert_eq!(result.header.code_size, 1);
        assert_eq!(result.header.flags & COR_ILMETHOD_FAT_FORMAT, COR_ILMETHOD_FAT_FORMAT);
        assert_eq!(result.header.flags & COR_ILMETHOD_INIT_LOCALS, COR_ILMETHOD_INIT_LOCALS);
        assert_eq!(result.header.max_stack, 8);
        assert_eq!(result.header.local_var_sig_tok, 0);
        assert!(result.extra_sections.is_none());
    }

    #[test]
    fn parse_tiny_header_multiple_instructions() {
        // code_size=3, format=0x2 → byte = (3 << 2) | 0x2 = 0x0E
        // Code: nop, nop, ret
        let bytes = vec![0x0E, 0x00, 0x00, 0x2A];
        let result = parse_method(&bytes).unwrap();
        assert_eq!(result.code, vec![0x00, 0x00, 0x2A]);
        assert_eq!(result.header.code_size, 3);
    }

    #[test]
    fn parse_tiny_header_max_code_size() {
        // Max tiny code_size = 63 → byte = (63 << 2) | 0x2 = 0xFE
        let mut bytes = vec![0xFE];
        bytes.extend(vec![0x00; 63]); // 63 bytes of nop
        let result = parse_method(&bytes).unwrap();
        assert_eq!(result.header.code_size, 63);
        assert_eq!(result.code.len(), 63);
    }

    #[test]
    fn parse_tiny_header_truncated_code() {
        // code_size=5 but only 2 bytes of code available
        let bytes = vec![(5 << 2) | 0x02, 0x00, 0x00];
        assert!(parse_method(&bytes).is_err());
    }

    // ==================== Fat header parsing ====================

    #[test]
    fn parse_fat_header_basic() {
        // Fat header: flags=0x13 (Fat|InitLocals), size=3, max_stack=4, code_size=2, localVarSig=0
        // flags_and_size = 0x0013 | (3 << 12) = 0x3013
        let mut bytes = vec![0x13, 0x30]; // flags_and_size LE
        bytes.extend_from_slice(&4u16.to_le_bytes()); // max_stack=4
        bytes.extend_from_slice(&2u32.to_le_bytes()); // code_size=2
        bytes.extend_from_slice(&0u32.to_le_bytes()); // local_var_sig=0
        bytes.push(0x00); // nop
        bytes.push(0x2A); // ret

        let result = parse_method(&bytes).unwrap();
        assert_eq!(result.header.flags, 0x0013);
        assert_eq!(result.header.size, 3);
        assert_eq!(result.header.max_stack, 4);
        assert_eq!(result.header.code_size, 2);
        assert_eq!(result.header.local_var_sig_tok, 0);
        assert_eq!(result.code, vec![0x00, 0x2A]);
        assert!(result.extra_sections.is_none());
    }

    #[test]
    fn parse_fat_header_with_locals() {
        let mut bytes = vec![0x13, 0x30]; // flags=FatFormat|InitLocals, size=3
        bytes.extend_from_slice(&8u16.to_le_bytes()); // max_stack=8
        bytes.extend_from_slice(&1u32.to_le_bytes()); // code_size=1
        bytes.extend_from_slice(&0x11000001u32.to_le_bytes()); // local_var_sig=0x11000001
        bytes.push(0x2A); // ret

        let result = parse_method(&bytes).unwrap();
        assert_eq!(result.header.local_var_sig_tok, 0x11000001);
    }

    #[test]
    fn parse_fat_header_with_extra_sections() {
        // flags with MoreSects set: 0x001B (Fat|InitLocals|MoreSects)
        let mut bytes = vec![0x1B, 0x30]; // flags=0x001B, size=3
        bytes.extend_from_slice(&8u16.to_le_bytes()); // max_stack
        bytes.extend_from_slice(&2u32.to_le_bytes()); // code_size=2
        bytes.extend_from_slice(&0u32.to_le_bytes()); // local_var_sig
        bytes.push(0x00); // nop (offset 12)
        bytes.push(0x2A); // ret (offset 13)
        // Padding to 4-byte boundary (offset 14, 15)
        bytes.push(0x00);
        bytes.push(0x00);
        // Extra section at offset 16 (4-byte aligned)
        bytes.extend_from_slice(&[0x41, 0x1C, 0x00, 0x00]); // EH header: fat, size=28

        let result = parse_method(&bytes).unwrap();
        assert!(result.extra_sections.is_some());
        let extra = result.extra_sections.unwrap();
        assert_eq!(extra[0], 0x41); // Fat exception section
    }

    #[test]
    fn parse_fat_header_truncated() {
        let bytes = vec![0x13, 0x30, 0x00]; // Only 3 bytes — need 12
        assert!(parse_method(&bytes).is_err());
    }

    // ==================== Tiny to fat conversion ====================

    #[test]
    fn tiny_to_fat_defaults() {
        let fat = tiny_to_fat(10);
        assert_eq!(fat.flags, COR_ILMETHOD_FAT_FORMAT | COR_ILMETHOD_INIT_LOCALS);
        assert_eq!(fat.size, FAT_HEADER_DWORDS);
        assert_eq!(fat.max_stack, 8);
        assert_eq!(fat.code_size, 10);
        assert_eq!(fat.local_var_sig_tok, 0);
    }

    // ==================== Write/parse round-trip ====================

    #[test]
    fn write_parse_round_trip() {
        let original = FatHeader {
            flags: COR_ILMETHOD_FAT_FORMAT | COR_ILMETHOD_INIT_LOCALS | COR_ILMETHOD_MORE_SECTS,
            size: FAT_HEADER_DWORDS,
            max_stack: 10,
            code_size: 256,
            local_var_sig_tok: 0x11000042,
        };

        let bytes = write_fat_header(&original);
        let parsed = parse_fat_header(&bytes).unwrap();

        assert_eq!(parsed.flags, original.flags);
        assert_eq!(parsed.size, original.size);
        assert_eq!(parsed.max_stack, original.max_stack);
        assert_eq!(parsed.code_size, original.code_size);
        assert_eq!(parsed.local_var_sig_tok, original.local_var_sig_tok);
    }

    #[test]
    fn write_fat_header_byte_layout() {
        let header = FatHeader {
            flags: 0x001B, // FatFormat|InitLocals|MoreSects
            size: 3,
            max_stack: 10,
            code_size: 100,
            local_var_sig_tok: 0x11000001,
        };

        let bytes = write_fat_header(&header);

        // flags_and_size = 0x001B | (3 << 12) = 0x301B
        assert_eq!(bytes[0], 0x1B);
        assert_eq!(bytes[1], 0x30);
        // max_stack = 10 LE
        assert_eq!(bytes[2], 0x0A);
        assert_eq!(bytes[3], 0x00);
        // code_size = 100 LE
        assert_eq!(bytes[4], 0x64);
        assert_eq!(bytes[5], 0x00);
        assert_eq!(bytes[6], 0x00);
        assert_eq!(bytes[7], 0x00);
        // local_var_sig_tok = 0x11000001 LE
        assert_eq!(bytes[8], 0x01);
        assert_eq!(bytes[9], 0x00);
        assert_eq!(bytes[10], 0x00);
        assert_eq!(bytes[11], 0x11);
    }

    // ==================== Build method bytes ====================

    #[test]
    fn build_method_no_extra_sections() {
        let header = tiny_to_fat(2);
        let code = vec![0x00, 0x2A]; // nop, ret
        let result = build_method_bytes(&header, &code, None);

        assert_eq!(result.len(), FAT_HEADER_SIZE + 2); // 12 + 2
        // Verify header is at start
        let parsed = parse_fat_header(&result).unwrap();
        assert_eq!(parsed.code_size, 2);
    }

    #[test]
    fn build_method_with_extra_sections_pads_to_alignment() {
        let header = FatHeader {
            flags: COR_ILMETHOD_FAT_FORMAT | COR_ILMETHOD_INIT_LOCALS | COR_ILMETHOD_MORE_SECTS,
            size: FAT_HEADER_DWORDS,
            max_stack: 8,
            code_size: 3, // 3 bytes of code → header(12) + code(3) = 15, needs 1 byte padding
            local_var_sig_tok: 0,
        };
        let code = vec![0x00, 0x00, 0x2A]; // nop, nop, ret
        let extra = vec![0x41, 0x1C, 0x00, 0x00]; // fake extra section header

        let result = build_method_bytes(&header, &code, Some(&extra));

        // 12 (header) + 3 (code) + 1 (pad) + 4 (extra) = 20
        assert_eq!(result.len(), 20);
        // Extra section should start at 4-byte aligned offset (16)
        assert_eq!(result[16], 0x41);
    }

    // ==================== Empty input ====================

    #[test]
    fn parse_empty_bytes_fails() {
        assert!(parse_method(&[]).is_err());
    }
}
