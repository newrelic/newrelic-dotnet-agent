// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

//! Exception clause parsing, offset shifting, and serialization.
//!
//! When the profiler instruments a method, it prepends instructions before
//! the original code and adds new try-catch blocks. Existing exception
//! clauses must have their offsets shifted by the number of prepended bytes
//! (the "user code offset"), and new clauses must be added for the injected
//! try-catch wrapper.
//!
//! The CLR stores exception clauses in "extra sections" following the IL code,
//! using either "small" (12 bytes/clause) or "fat" (24 bytes/clause) format.
//! The profiler always writes fat format for simplicity.
//!
//! Reference: `ExceptionHandlerManipulator.h` lines 147-277, ECMA-335 II.25.4.

use super::instruction_builder::ExceptionClause;
use super::IlError;

// ==================== Extra section flags ====================

/// Exception handling table flag
const COR_ILEXCEPTION_SECT_EHTABLE: u8 = 0x01;
/// Fat format flag (24-byte clauses)
const COR_ILEXCEPTION_SECT_FATFORMAT: u8 = 0x40;
/// More sections follow flag
#[allow(dead_code)]
const COR_ILEXCEPTION_SECT_MORE_SECTS: u8 = 0x80;

/// Size of a fat exception clause in bytes
const FAT_CLAUSE_SIZE: usize = 24;
/// Size of a small exception clause in bytes
const SMALL_CLAUSE_SIZE: usize = 12;
/// Size of the extra section header in bytes
const EXTRA_SECTION_HEADER_SIZE: usize = 4;

// ==================== Exception handler manipulator ====================

/// Manages exception handling clauses for a method being instrumented.
///
/// Holds both original clauses (parsed from the method's extra sections)
/// and new clauses (added by the instrumentation template). When building
/// the final extra section, original clauses are shifted by the user code
/// offset, then all clauses are serialized in fat format.
pub struct ExceptionHandlerManipulator {
    /// Original clauses from the method (will be shifted)
    original_clauses: Vec<ExceptionClause>,
    /// New clauses added by instrumentation
    new_clauses: Vec<ExceptionClause>,
}

impl ExceptionHandlerManipulator {
    /// Create an empty manipulator (method had no existing exception handlers).
    pub fn new() -> Self {
        Self {
            original_clauses: Vec::new(),
            new_clauses: Vec::new(),
        }
    }

    /// Create a manipulator by parsing existing extra section bytes.
    ///
    /// The bytes should start at the beginning of the extra section
    /// (right after 4-byte alignment padding following the code).
    pub fn from_extra_section(bytes: &[u8]) -> Result<Self, IlError> {
        let clauses = parse_extra_section(bytes)?;
        Ok(Self {
            original_clauses: clauses,
            new_clauses: Vec::new(),
        })
    }

    /// Add a new exception clause (from the instrumentation template).
    pub fn add_clause(&mut self, clause: ExceptionClause) {
        self.new_clauses.push(clause);
    }

    /// Add multiple new clauses from the instruction builder.
    pub fn add_clauses(&mut self, clauses: &[ExceptionClause]) {
        self.new_clauses.extend_from_slice(clauses);
    }

    /// Build the complete extra section bytes.
    ///
    /// Original clauses are shifted by `user_code_offset`, then all clauses
    /// (new + shifted originals) are serialized in fat format.
    ///
    /// Reference: `ExceptionHandlerManipulator::GetExtraSectionBytes` at
    /// `ExceptionHandlerManipulator.h` lines 245-277.
    pub fn get_extra_section_bytes(&self, user_code_offset: u32) -> Vec<u8> {
        let total_clauses = self.new_clauses.len() + self.original_clauses.len();
        if total_clauses == 0 {
            return Vec::new();
        }

        let extra_section_size = EXTRA_SECTION_HEADER_SIZE + total_clauses * FAT_CLAUSE_SIZE;
        let mut bytes = Vec::with_capacity(extra_section_size);

        // Extra section header: flags byte + 24-bit LE size
        bytes.push(COR_ILEXCEPTION_SECT_EHTABLE | COR_ILEXCEPTION_SECT_FATFORMAT);
        append_le_u24(&mut bytes, extra_section_size as u32);

        // Write new clauses first (from the instrumentation template)
        for clause in &self.new_clauses {
            write_fat_clause(&mut bytes, clause);
        }

        // Write original clauses with shifted offsets
        for clause in &self.original_clauses {
            let mut shifted = clause.clone();
            shifted.try_offset += user_code_offset;
            shifted.handler_offset += user_code_offset;
            // For filter clauses, also shift the filter offset
            if shifted.flags == 0x0001 {
                shifted.filter_offset += user_code_offset;
            }
            write_fat_clause(&mut bytes, &shifted);
        }

        bytes
    }

    /// Get the total number of clauses (original + new).
    pub fn clause_count(&self) -> usize {
        self.original_clauses.len() + self.new_clauses.len()
    }
}

// ==================== Parsing ====================

/// Parse an extra section into exception clauses.
fn parse_extra_section(bytes: &[u8]) -> Result<Vec<ExceptionClause>, IlError> {
    if bytes.len() < EXTRA_SECTION_HEADER_SIZE {
        return Err(IlError::InvalidExceptionClause(
            "Extra section too short for header".to_string(),
        ));
    }

    let flags = bytes[0];

    // Must be an exception handling table
    if (flags & COR_ILEXCEPTION_SECT_EHTABLE) == 0 {
        return Err(IlError::InvalidExceptionClause(
            "Extra section is not an EH table".to_string(),
        ));
    }

    let is_fat = (flags & COR_ILEXCEPTION_SECT_FATFORMAT) != 0;

    if is_fat {
        parse_fat_section(bytes)
    } else {
        parse_small_section(bytes)
    }
}

/// Parse fat-format exception clauses.
fn parse_fat_section(bytes: &[u8]) -> Result<Vec<ExceptionClause>, IlError> {
    // Size is 24-bit LE at bytes[1..4]
    let size = read_le_u24(&bytes[1..4]) as usize;

    if size < EXTRA_SECTION_HEADER_SIZE {
        return Err(IlError::InvalidExceptionClause(format!(
            "Fat section size {} too small",
            size
        )));
    }

    let clause_count = (size - EXTRA_SECTION_HEADER_SIZE) / FAT_CLAUSE_SIZE;
    let mut clauses = Vec::with_capacity(clause_count);

    for i in 0..clause_count {
        let offset = EXTRA_SECTION_HEADER_SIZE + i * FAT_CLAUSE_SIZE;
        if offset + FAT_CLAUSE_SIZE > bytes.len() {
            return Err(IlError::InvalidExceptionClause(format!(
                "Fat clause {} extends beyond extra section",
                i
            )));
        }
        clauses.push(parse_fat_clause(&bytes[offset..]));
    }

    Ok(clauses)
}

/// Parse small-format exception clauses.
fn parse_small_section(bytes: &[u8]) -> Result<Vec<ExceptionClause>, IlError> {
    // Size is 1 byte at bytes[1] for small format
    let size = bytes[1] as usize;

    if size < EXTRA_SECTION_HEADER_SIZE {
        return Err(IlError::InvalidExceptionClause(format!(
            "Small section size {} too small",
            size
        )));
    }

    let clause_count = (size - EXTRA_SECTION_HEADER_SIZE) / SMALL_CLAUSE_SIZE;
    let mut clauses = Vec::with_capacity(clause_count);

    for i in 0..clause_count {
        let offset = EXTRA_SECTION_HEADER_SIZE + i * SMALL_CLAUSE_SIZE;
        if offset + SMALL_CLAUSE_SIZE > bytes.len() {
            return Err(IlError::InvalidExceptionClause(format!(
                "Small clause {} extends beyond extra section",
                i
            )));
        }
        clauses.push(parse_small_clause(&bytes[offset..]));
    }

    Ok(clauses)
}

/// Parse a single fat exception clause (24 bytes).
///
/// Layout: flags u32, tryOffset u32, tryLength u32,
/// handlerOffset u32, handlerLength u32, classToken/filterOffset u32.
fn parse_fat_clause(bytes: &[u8]) -> ExceptionClause {
    let flags = u32::from_le_bytes([bytes[0], bytes[1], bytes[2], bytes[3]]);
    let try_offset = u32::from_le_bytes([bytes[4], bytes[5], bytes[6], bytes[7]]);
    let try_length = u32::from_le_bytes([bytes[8], bytes[9], bytes[10], bytes[11]]);
    let handler_offset = u32::from_le_bytes([bytes[12], bytes[13], bytes[14], bytes[15]]);
    let handler_length = u32::from_le_bytes([bytes[16], bytes[17], bytes[18], bytes[19]]);
    let token_or_filter = u32::from_le_bytes([bytes[20], bytes[21], bytes[22], bytes[23]]);

    let (class_token, filter_offset) = if flags == 0x0001 {
        (0, token_or_filter)
    } else {
        (token_or_filter, 0)
    };

    ExceptionClause {
        flags,
        try_offset,
        try_length,
        handler_offset,
        handler_length,
        class_token,
        filter_offset,
    }
}

/// Parse a single small exception clause (12 bytes).
///
/// Layout: flags u16, tryOffset u16, tryLength u8,
/// handlerOffset u16, handlerLength u8, classToken/filterOffset u32.
fn parse_small_clause(bytes: &[u8]) -> ExceptionClause {
    let flags = u16::from_le_bytes([bytes[0], bytes[1]]) as u32;
    let try_offset = u16::from_le_bytes([bytes[2], bytes[3]]) as u32;
    let try_length = bytes[4] as u32;
    let handler_offset = u16::from_le_bytes([bytes[5], bytes[6]]) as u32;
    let handler_length = bytes[7] as u32;
    let token_or_filter = u32::from_le_bytes([bytes[8], bytes[9], bytes[10], bytes[11]]);

    let (class_token, filter_offset) = if flags == 0x0001 {
        (0, token_or_filter)
    } else {
        (token_or_filter, 0)
    };

    ExceptionClause {
        flags,
        try_offset,
        try_length,
        handler_offset,
        handler_length,
        class_token,
        filter_offset,
    }
}

// ==================== Serialization ====================

/// Write a single fat exception clause (24 bytes) to a byte vector.
fn write_fat_clause(bytes: &mut Vec<u8>, clause: &ExceptionClause) {
    bytes.extend_from_slice(&clause.flags.to_le_bytes());
    bytes.extend_from_slice(&clause.try_offset.to_le_bytes());
    bytes.extend_from_slice(&clause.try_length.to_le_bytes());
    bytes.extend_from_slice(&clause.handler_offset.to_le_bytes());
    bytes.extend_from_slice(&clause.handler_length.to_le_bytes());

    // Last 4 bytes: classToken for catch, filterOffset for filter, 0 otherwise
    if clause.flags == 0x0001 {
        bytes.extend_from_slice(&clause.filter_offset.to_le_bytes());
    } else {
        bytes.extend_from_slice(&clause.class_token.to_le_bytes());
    }
}

// ==================== Helpers ====================

/// Read a 24-bit little-endian unsigned integer.
fn read_le_u24(bytes: &[u8]) -> u32 {
    (bytes[0] as u32) | ((bytes[1] as u32) << 8) | ((bytes[2] as u32) << 16)
}

/// Append a 24-bit little-endian unsigned integer.
fn append_le_u24(bytes: &mut Vec<u8>, value: u32) {
    bytes.push((value & 0xFF) as u8);
    bytes.push(((value >> 8) & 0xFF) as u8);
    bytes.push(((value >> 16) & 0xFF) as u8);
}

#[cfg(test)]
mod tests {
    use super::*;

    // ==================== Empty manipulator ====================

    #[test]
    fn empty_manipulator_produces_no_bytes() {
        let manip = ExceptionHandlerManipulator::new();
        assert_eq!(manip.get_extra_section_bytes(0), Vec::<u8>::new());
        assert_eq!(manip.clause_count(), 0);
    }

    // ==================== Fat clause parsing ====================

    #[test]
    fn parse_fat_section_one_clause() {
        // Fat section: flags=0x41, size=28 (4 + 24)
        let mut bytes = vec![
            0x41, // EHTable | FatFormat
            0x1C, 0x00, 0x00, // size = 28 (24-bit LE)
        ];
        // One fat clause: catch, try@0 len=10, handler@10 len=5, token=0x01000042
        bytes.extend_from_slice(&0u32.to_le_bytes()); // flags = catch
        bytes.extend_from_slice(&0u32.to_le_bytes()); // tryOffset = 0
        bytes.extend_from_slice(&10u32.to_le_bytes()); // tryLength = 10
        bytes.extend_from_slice(&10u32.to_le_bytes()); // handlerOffset = 10
        bytes.extend_from_slice(&5u32.to_le_bytes()); // handlerLength = 5
        bytes.extend_from_slice(&0x01000042u32.to_le_bytes()); // classToken

        let manip = ExceptionHandlerManipulator::from_extra_section(&bytes).unwrap();
        assert_eq!(manip.original_clauses.len(), 1);
        assert_eq!(manip.original_clauses[0].flags, 0);
        assert_eq!(manip.original_clauses[0].try_offset, 0);
        assert_eq!(manip.original_clauses[0].try_length, 10);
        assert_eq!(manip.original_clauses[0].handler_offset, 10);
        assert_eq!(manip.original_clauses[0].handler_length, 5);
        assert_eq!(manip.original_clauses[0].class_token, 0x01000042);
    }

    // ==================== Small clause parsing ====================

    #[test]
    fn parse_small_section_one_clause() {
        // Small section: flags=0x01 (EHTable, not fat), size=16 (4 + 12)
        #[rustfmt::skip]
        let bytes = vec![
            0x01,                   // EHTable (not fat)
            0x10, 0x00, 0x00,       // size = 16 (only low byte used for small)
            0x00, 0x00,             // flags = catch
            0x00, 0x00,             // tryOffset = 0
            0x01,                   // tryLength = 1
            0x02, 0x00,             // handlerOffset = 2
            0x01,                   // handlerLength = 1
            0x00, 0x00, 0x00, 0x00, // classToken = 0
        ];

        let manip = ExceptionHandlerManipulator::from_extra_section(&bytes).unwrap();
        assert_eq!(manip.original_clauses.len(), 1);
        assert_eq!(manip.original_clauses[0].try_offset, 0);
        assert_eq!(manip.original_clauses[0].try_length, 1);
        assert_eq!(manip.original_clauses[0].handler_offset, 2);
        assert_eq!(manip.original_clauses[0].handler_length, 1);
    }

    // ==================== Offset shifting ====================

    #[test]
    fn offset_shifting_applied_to_original_clauses() {
        let mut manip = ExceptionHandlerManipulator::new();
        manip.original_clauses.push(ExceptionClause {
            flags: 0,
            try_offset: 10,
            try_length: 20,
            handler_offset: 30,
            handler_length: 10,
            class_token: 0x01000042,
            filter_offset: 0,
        });

        let bytes = manip.get_extra_section_bytes(50);

        // Parse back to verify shifting
        let result = ExceptionHandlerManipulator::from_extra_section(&bytes).unwrap();
        assert_eq!(result.original_clauses[0].try_offset, 60); // 10 + 50
        assert_eq!(result.original_clauses[0].handler_offset, 80); // 30 + 50
        assert_eq!(result.original_clauses[0].try_length, 20); // unchanged
        assert_eq!(result.original_clauses[0].handler_length, 10); // unchanged
    }

    #[test]
    fn filter_clause_offset_shifted() {
        let mut manip = ExceptionHandlerManipulator::new();
        manip.original_clauses.push(ExceptionClause {
            flags: 0x0001, // filter clause
            try_offset: 5,
            try_length: 10,
            handler_offset: 20,
            handler_length: 5,
            class_token: 0,
            filter_offset: 15,
        });

        let bytes = manip.get_extra_section_bytes(100);

        let result = ExceptionHandlerManipulator::from_extra_section(&bytes).unwrap();
        assert_eq!(result.original_clauses[0].try_offset, 105);
        assert_eq!(result.original_clauses[0].handler_offset, 120);
        assert_eq!(result.original_clauses[0].filter_offset, 115);
    }

    // ==================== New clauses ====================

    #[test]
    fn new_clauses_written_before_original() {
        let mut manip = ExceptionHandlerManipulator::new();
        manip.original_clauses.push(ExceptionClause {
            flags: 0,
            try_offset: 0,
            try_length: 10,
            handler_offset: 10,
            handler_length: 5,
            class_token: 0x01000001,
            filter_offset: 0,
        });
        manip.add_clause(ExceptionClause {
            flags: 0,
            try_offset: 100,
            try_length: 20,
            handler_offset: 120,
            handler_length: 10,
            class_token: 0x01000002,
            filter_offset: 0,
        });

        let bytes = manip.get_extra_section_bytes(50);
        let result = ExceptionHandlerManipulator::from_extra_section(&bytes).unwrap();

        assert_eq!(result.original_clauses.len(), 2);
        // New clause first (unshifted)
        assert_eq!(result.original_clauses[0].try_offset, 100);
        assert_eq!(result.original_clauses[0].class_token, 0x01000002);
        // Original clause second (shifted by 50)
        assert_eq!(result.original_clauses[1].try_offset, 50); // 0 + 50
        assert_eq!(result.original_clauses[1].class_token, 0x01000001);
    }

    // ==================== Extra section header format ====================

    #[test]
    fn extra_section_header_format() {
        let mut manip = ExceptionHandlerManipulator::new();
        manip.add_clause(ExceptionClause {
            flags: 0,
            try_offset: 0,
            try_length: 5,
            handler_offset: 5,
            handler_length: 3,
            class_token: 0x01000001,
            filter_offset: 0,
        });

        let bytes = manip.get_extra_section_bytes(0);

        // Header byte: EHTable | FatFormat = 0x41
        assert_eq!(bytes[0], 0x41);
        // Size: 4 (header) + 24 (one clause) = 28 = 0x1C
        assert_eq!(bytes[1], 0x1C);
        assert_eq!(bytes[2], 0x00);
        assert_eq!(bytes[3], 0x00);
        // Total bytes: 28
        assert_eq!(bytes.len(), 28);
    }

    #[test]
    fn multiple_clauses_section_size() {
        let mut manip = ExceptionHandlerManipulator::new();
        for _ in 0..3 {
            manip.add_clause(ExceptionClause::default());
        }

        let bytes = manip.get_extra_section_bytes(0);

        // Size: 4 + 3*24 = 76
        let size = read_le_u24(&bytes[1..4]);
        assert_eq!(size, 76);
        assert_eq!(bytes.len(), 76);
    }

    // ==================== Round-trip: write then parse ====================

    #[test]
    fn fat_clause_round_trip() {
        let original = ExceptionClause {
            flags: 0,
            try_offset: 42,
            try_length: 100,
            handler_offset: 142,
            handler_length: 50,
            class_token: 0x01000099,
            filter_offset: 0,
        };

        let mut manip = ExceptionHandlerManipulator::new();
        manip.add_clause(original.clone());

        let bytes = manip.get_extra_section_bytes(0);
        let result = ExceptionHandlerManipulator::from_extra_section(&bytes).unwrap();

        assert_eq!(result.original_clauses.len(), 1);
        let parsed = &result.original_clauses[0];
        assert_eq!(parsed.flags, original.flags);
        assert_eq!(parsed.try_offset, original.try_offset);
        assert_eq!(parsed.try_length, original.try_length);
        assert_eq!(parsed.handler_offset, original.handler_offset);
        assert_eq!(parsed.handler_length, original.handler_length);
        assert_eq!(parsed.class_token, original.class_token);
    }

    // ==================== Error cases ====================

    #[test]
    fn parse_too_short_fails() {
        assert!(ExceptionHandlerManipulator::from_extra_section(&[0x41]).is_err());
    }

    #[test]
    fn parse_non_eh_table_fails() {
        // flags=0x40 (fat but not EH table)
        assert!(ExceptionHandlerManipulator::from_extra_section(&[0x40, 0x04, 0x00, 0x00]).is_err());
    }

    // ==================== LE 24-bit helpers ====================

    #[test]
    fn le_u24_round_trip() {
        let mut bytes = Vec::new();
        append_le_u24(&mut bytes, 0x1C);
        assert_eq!(read_le_u24(&bytes), 0x1C);

        let mut bytes2 = Vec::new();
        append_le_u24(&mut bytes2, 0x123456);
        assert_eq!(read_le_u24(&bytes2), 0x123456);
    }
}
