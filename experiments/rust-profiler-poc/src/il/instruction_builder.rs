// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

//! IL instruction builder — bytecode generation with jump labels and exception tracking.
//!
//! Rust equivalent of the C++ `InstructionSet.h` (~800 lines). Provides:
//! - Opcode encoding (1-byte and 2-byte 0xFE-prefixed)
//! - Operand encoding (u8, u16, u32, u64, all little-endian)
//! - Jump label system with forward-reference patching
//! - Load/store argument/local optimization (ldarg.0-3, ldarg.s, ldarg, etc.)
//! - Exception handling clause boundary tracking
//!
//! Reference: `src/Agent/NewRelic/Profiler/MethodRewriter/InstructionSet.h`

use std::collections::HashMap;

use super::opcodes::*;

/// An exception handling clause being constructed.
#[derive(Debug, Clone, Default)]
pub struct ExceptionClause {
    /// Exception clause flags: 0x0000 = catch, 0x0001 = filter, 0x0002 = finally
    pub flags: u32,
    /// Byte offset of the start of the try block
    pub try_offset: u32,
    /// Length of the try block in bytes
    pub try_length: u32,
    /// Byte offset of the start of the handler
    pub handler_offset: u32,
    /// Length of the handler in bytes
    pub handler_length: u32,
    /// Token of the exception type (for catch clauses)
    pub class_token: u32,
    /// Offset of the filter expression (for filter clauses)
    pub filter_offset: u32,
}

/// Builds IL bytecode with support for jump labels and exception clause tracking.
///
/// Mirrors the C++ `InstructionSet` class. Instructions are appended sequentially;
/// jump targets are resolved when labels are placed.
pub struct InstructionBuilder {
    /// Raw IL bytes being built
    bytes: Vec<u8>,
    /// Forward references: label name → list of positions where 4-byte distance must be patched
    jumps: HashMap<String, Vec<usize>>,
    /// Stack of exception clauses being constructed (nested try-catch)
    exception_stack: Vec<ExceptionClause>,
    /// Completed exception clauses ready for serialization
    completed_clauses: Vec<ExceptionClause>,
    /// Byte offset where user (original) code begins
    user_code_offset: u32,
    /// Counter for auto-generated label names
    label_counter: u32,
}

impl InstructionBuilder {
    /// Create a new instruction builder with pre-allocated capacity.
    pub fn new() -> Self {
        Self {
            bytes: Vec::with_capacity(500), // C++ reserves 500
            jumps: HashMap::new(),
            exception_stack: Vec::new(),
            completed_clauses: Vec::new(),
            user_code_offset: 0,
            label_counter: 0,
        }
    }

    // ==================== Opcode encoding ====================

    /// Append an opcode with no operand.
    ///
    /// Single-byte opcodes (≤0xFF) emit 1 byte; two-byte opcodes (>0xFF)
    /// emit `[0xFE, low_byte]`.
    pub fn append_opcode(&mut self, opcode: IlOpcode) {
        if is_two_byte_opcode(opcode) {
            self.bytes.push(0xFE);
            self.bytes.push((opcode & 0xFF) as u8);
        } else {
            self.bytes.push(opcode as u8);
        }
    }

    /// Append an opcode with a u8 operand.
    pub fn append_opcode_u8(&mut self, opcode: IlOpcode, operand: u8) {
        self.append_opcode(opcode);
        self.bytes.push(operand);
    }

    /// Append an opcode with a u16 operand (little-endian).
    pub fn append_opcode_u16(&mut self, opcode: IlOpcode, operand: u16) {
        self.append_opcode(opcode);
        self.append_le_u16(operand);
    }

    /// Append an opcode with a u32 operand (little-endian).
    pub fn append_opcode_u32(&mut self, opcode: IlOpcode, operand: u32) {
        self.append_opcode(opcode);
        self.append_le_u32(operand);
    }

    /// Append an opcode with a u64 operand (little-endian).
    pub fn append_opcode_u64(&mut self, opcode: IlOpcode, operand: u64) {
        self.append_opcode(opcode);
        self.append_le_u64(operand);
    }

    // ==================== Raw byte helpers ====================

    /// Append raw bytes (e.g., original method body).
    pub fn append_raw_bytes(&mut self, bytes: &[u8]) {
        self.bytes.extend_from_slice(bytes);
    }

    /// Append a u16 in little-endian.
    fn append_le_u16(&mut self, value: u16) {
        self.bytes.push((value & 0xFF) as u8);
        self.bytes.push(((value >> 8) & 0xFF) as u8);
    }

    /// Append a u32 in little-endian.
    fn append_le_u32(&mut self, value: u32) {
        self.bytes.push((value & 0xFF) as u8);
        self.bytes.push(((value >> 8) & 0xFF) as u8);
        self.bytes.push(((value >> 16) & 0xFF) as u8);
        self.bytes.push(((value >> 24) & 0xFF) as u8);
    }

    /// Append a u64 in little-endian.
    fn append_le_u64(&mut self, value: u64) {
        for i in 0..8 {
            self.bytes.push(((value >> (8 * i)) & 0xFF) as u8);
        }
    }

    // ==================== Jump label system ====================

    /// Emit a jump instruction with a named forward label.
    ///
    /// The jump opcode is emitted followed by a 4-byte placeholder (all zeros).
    /// When `append_label` is called with the same label name, the placeholder
    /// is patched with the correct distance.
    ///
    /// The instruction byte is the opcode value (e.g., 0xDD for `leave`).
    pub fn append_jump(&mut self, instruction: u8, label: &str) {
        self.bytes.push(instruction);
        let placeholder_pos = self.bytes.len();
        self.jumps
            .entry(label.to_string())
            .or_default()
            .push(placeholder_pos);
        // Reserve 4 bytes for the 32-bit distance
        self.bytes.push(0x00);
        self.bytes.push(0x00);
        self.bytes.push(0x00);
        self.bytes.push(0x00);
    }

    /// Emit a jump instruction with an auto-generated label. Returns the label name.
    pub fn append_jump_auto(&mut self, instruction: u8) -> String {
        let label = format!("__auto_{}", self.label_counter);
        self.label_counter += 1;
        self.append_jump(instruction, &label);
        label
    }

    /// Place a label at the current position. All jumps targeting this label
    /// are patched with the correct distance.
    ///
    /// Jump distance formula (from C++ InstructionSet.h lines 286-296):
    /// `distance = current_pos - (placeholder_pos + 4)`
    /// This is because the distance is relative to the instruction AFTER the 4-byte operand.
    pub fn append_label(&mut self, label: &str) {
        if let Some(positions) = self.jumps.get(label) {
            let current_pos = self.bytes.len();
            for &placeholder_pos in positions {
                // Distance from the byte after the 4-byte operand to the label
                let distance = (current_pos as i32) - ((placeholder_pos + 4) as i32);
                let distance_bytes = distance.to_le_bytes();
                self.bytes[placeholder_pos] = distance_bytes[0];
                self.bytes[placeholder_pos + 1] = distance_bytes[1];
                self.bytes[placeholder_pos + 2] = distance_bytes[2];
                self.bytes[placeholder_pos + 3] = distance_bytes[3];
            }
        }
    }

    // ==================== Optimized load/store ====================

    /// Emit an optimized ldarg instruction.
    ///
    /// - Index 0-3: `ldarg.0` through `ldarg.3` (single byte)
    /// - Index 4-254: `ldarg.s` (2 bytes)
    /// - Index 255+: `ldarg` (3 bytes: 0xFE 0x09 + u16)
    pub fn append_load_argument(&mut self, index: u16) {
        if index < 4 {
            self.append_opcode(CEE_LDARG_0 + index);
        } else if index < 255 {
            self.append_opcode_u8(CEE_LDARG_S, index as u8);
        } else {
            self.append_opcode_u16(CEE_LDARG, index);
        }
    }

    /// Emit an optimized ldloc instruction.
    ///
    /// - Index 0-3: `ldloc.0` through `ldloc.3` (single byte)
    /// - Index 4-254: `ldloc.s` (2 bytes)
    /// - Index 255+: `ldloc` (4 bytes: 0xFE 0x0C + u16)
    pub fn append_load_local(&mut self, index: u16) {
        if index < 4 {
            self.append_opcode(CEE_LDLOC_0 + index);
        } else if index < 255 {
            self.append_opcode_u8(CEE_LDLOC_S, index as u8);
        } else {
            self.append_opcode_u16(CEE_LDLOC, index);
        }
    }

    /// Emit an optimized stloc instruction.
    ///
    /// - Index 0-3: `stloc.0` through `stloc.3` (single byte)
    /// - Index 4-254: `stloc.s` (2 bytes)
    /// - Index 255+: `stloc` (4 bytes: 0xFE 0x0E + u16)
    pub fn append_store_local(&mut self, index: u16) {
        if index < 4 {
            self.append_opcode(CEE_STLOC_0 + index);
        } else if index < 255 {
            self.append_opcode_u8(CEE_STLOC_S, index as u8);
        } else {
            self.append_opcode_u16(CEE_STLOC, index);
        }
    }

    /// Emit an optimized ldc.i4 instruction for a small integer.
    ///
    /// - -1 through 8: single-byte encoding (`ldc.i4.m1` through `ldc.i4.8`)
    /// - -128 through 127: `ldc.i4.s` (2 bytes)
    /// - Otherwise: `ldc.i4` (5 bytes)
    pub fn append_ldc_i4(&mut self, value: i32) {
        let (opcode, operand) = ldc_i4_opcode_for(value);
        match operand {
            None => self.append_opcode(opcode),
            Some(v) if opcode == CEE_LDC_I4_S => self.append_opcode_u8(opcode, v as u8),
            Some(v) => self.append_opcode_u32(opcode, v as u32),
        }
    }

    // ==================== User code insertion ====================

    /// Record the user code offset and append the original method body.
    ///
    /// The user code offset is used by the exception handler manipulator
    /// to shift original exception clause offsets.
    pub fn append_user_code(&mut self, user_code: &[u8]) {
        self.user_code_offset = self.bytes.len() as u32;
        self.bytes.extend_from_slice(user_code);
    }

    // ==================== Exception clause tracking ====================

    /// Mark the start of a try block. Must be paired with `append_try_end`.
    pub fn append_try_start(&mut self) {
        let clause = ExceptionClause {
            try_offset: self.bytes.len() as u32,
            ..Default::default()
        };
        self.exception_stack.push(clause);
    }

    /// Mark the end of a try block. Must be called after `append_try_start`.
    pub fn append_try_end(&mut self) {
        if let Some(clause) = self.exception_stack.last_mut() {
            clause.try_length = (self.bytes.len() as u32) - clause.try_offset;
        }
    }

    /// Mark the start of a catch handler. Sets the handler offset and class token.
    pub fn append_catch_start(&mut self, class_token: u32) {
        if let Some(clause) = self.exception_stack.last_mut() {
            clause.handler_offset = self.bytes.len() as u32;
            clause.flags = 0; // COR_ILEXCEPTION_CLAUSE_EXCEPTION
            clause.class_token = class_token;
        }
    }

    /// Mark the end of a catch handler. Moves the clause to completed list.
    pub fn append_catch_end(&mut self) {
        if let Some(mut clause) = self.exception_stack.pop() {
            clause.handler_length = (self.bytes.len() as u32) - clause.handler_offset;
            self.completed_clauses.push(clause);
        }
    }

    // ==================== Accessors ====================

    /// Get the built IL bytes.
    pub fn get_bytes(&self) -> &[u8] {
        &self.bytes
    }

    /// Get the byte offset where user (original) code begins.
    pub fn get_user_code_offset(&self) -> u32 {
        self.user_code_offset
    }

    /// Get the completed exception clauses.
    pub fn get_completed_clauses(&self) -> &[ExceptionClause] {
        &self.completed_clauses
    }

    /// Get the current position (number of bytes written so far).
    pub fn position(&self) -> usize {
        self.bytes.len()
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    // ==================== Opcode encoding tests ====================

    #[test]
    fn append_single_byte_opcode() {
        let mut builder = InstructionBuilder::new();
        builder.append_opcode(CEE_LDNULL);
        assert_eq!(builder.get_bytes(), &[0x14]);
    }

    #[test]
    fn append_two_byte_opcode() {
        let mut builder = InstructionBuilder::new();
        builder.append_opcode(CEE_RETHROW);
        assert_eq!(builder.get_bytes(), &[0xFE, 0x1A]);
    }

    // ==================== Operand encoding tests (matching C++ InstructionSetTest) ====================

    #[test]
    fn append_opcode_u16_little_endian() {
        // Matches C++ InstructionSetTest::append_short: 0xDEAD
        let mut builder = InstructionBuilder::new();
        builder.append_opcode_u16(CEE_LDNULL, 0xDEAD);
        assert_eq!(builder.get_bytes(), &[0x14, 0xAD, 0xDE]);
    }

    #[test]
    fn append_opcode_u32_little_endian() {
        // Matches C++ InstructionSetTest::append_integer: 0xDEADBEEF
        let mut builder = InstructionBuilder::new();
        builder.append_opcode_u32(CEE_LDC_I4, 0xDEADBEEF);
        assert_eq!(builder.get_bytes(), &[0x20, 0xEF, 0xBE, 0xAD, 0xDE]);
    }

    #[test]
    fn append_opcode_u64_little_endian() {
        // Matches C++ InstructionSetTest::append_long: 0xBEEBDEADBEEFABBE
        let mut builder = InstructionBuilder::new();
        builder.append_opcode_u64(CEE_LDC_I8, 0xBEEB_DEAD_BEEF_ABBE);
        assert_eq!(
            builder.get_bytes(),
            &[0x21, 0xBE, 0xAB, 0xEF, 0xBE, 0xAD, 0xDE, 0xEB, 0xBE]
        );
    }

    // ==================== Load argument optimization ====================

    #[test]
    fn load_argument_0_to_3() {
        let mut builder = InstructionBuilder::new();
        builder.append_load_argument(0);
        builder.append_load_argument(1);
        builder.append_load_argument(2);
        builder.append_load_argument(3);
        // ldarg.0=0x02, ldarg.1=0x03, ldarg.2=0x04, ldarg.3=0x05
        assert_eq!(builder.get_bytes(), &[0x02, 0x03, 0x04, 0x05]);
    }

    #[test]
    fn load_argument_short_form() {
        let mut builder = InstructionBuilder::new();
        builder.append_load_argument(4);
        // ldarg.s 4
        assert_eq!(builder.get_bytes(), &[0x0E, 0x04]);
    }

    #[test]
    fn load_argument_long_form() {
        let mut builder = InstructionBuilder::new();
        builder.append_load_argument(256);
        // ldarg 256: [0xFE, 0x09, 0x00, 0x01]
        assert_eq!(builder.get_bytes(), &[0xFE, 0x09, 0x00, 0x01]);
    }

    // ==================== Load local optimization ====================

    #[test]
    fn load_local_0_to_3() {
        let mut builder = InstructionBuilder::new();
        builder.append_load_local(0);
        builder.append_load_local(1);
        builder.append_load_local(2);
        builder.append_load_local(3);
        assert_eq!(builder.get_bytes(), &[0x06, 0x07, 0x08, 0x09]);
    }

    #[test]
    fn load_local_short_form() {
        let mut builder = InstructionBuilder::new();
        builder.append_load_local(4);
        assert_eq!(builder.get_bytes(), &[0x11, 0x04]);
    }

    #[test]
    fn load_local_long_form() {
        let mut builder = InstructionBuilder::new();
        builder.append_load_local(256);
        assert_eq!(builder.get_bytes(), &[0xFE, 0x0C, 0x00, 0x01]);
    }

    // ==================== Store local optimization ====================

    #[test]
    fn store_local_0_to_3() {
        let mut builder = InstructionBuilder::new();
        builder.append_store_local(0);
        builder.append_store_local(1);
        builder.append_store_local(2);
        builder.append_store_local(3);
        assert_eq!(builder.get_bytes(), &[0x0A, 0x0B, 0x0C, 0x0D]);
    }

    #[test]
    fn store_local_short_form() {
        let mut builder = InstructionBuilder::new();
        builder.append_store_local(4);
        assert_eq!(builder.get_bytes(), &[0x13, 0x04]);
    }

    #[test]
    fn store_local_long_form() {
        let mut builder = InstructionBuilder::new();
        builder.append_store_local(256);
        assert_eq!(builder.get_bytes(), &[0xFE, 0x0E, 0x00, 0x01]);
    }

    // ==================== ldc.i4 optimization ====================

    #[test]
    fn ldc_i4_inline_values() {
        let mut builder = InstructionBuilder::new();
        builder.append_ldc_i4(-1);
        builder.append_ldc_i4(0);
        builder.append_ldc_i4(8);
        // ldc.i4.m1=0x15, ldc.i4.0=0x16, ldc.i4.8=0x1E
        assert_eq!(builder.get_bytes(), &[0x15, 0x16, 0x1E]);
    }

    #[test]
    fn ldc_i4_short_form() {
        let mut builder = InstructionBuilder::new();
        builder.append_ldc_i4(11);
        // ldc.i4.s 11
        assert_eq!(builder.get_bytes(), &[0x1F, 0x0B]);
    }

    #[test]
    fn ldc_i4_long_form() {
        let mut builder = InstructionBuilder::new();
        builder.append_ldc_i4(1000);
        // ldc.i4 1000 = [0x20, 0xE8, 0x03, 0x00, 0x00]
        assert_eq!(builder.get_bytes(), &[0x20, 0xE8, 0x03, 0x00, 0x00]);
    }

    // ==================== Jump label system ====================

    #[test]
    fn jump_label_forward_reference() {
        let mut builder = InstructionBuilder::new();

        // Emit nop, then a leave to "after"
        builder.append_opcode(CEE_NOP); // offset 0, 1 byte
        builder.append_jump(CEE_LEAVE as u8, "after"); // offset 1, 5 bytes (opcode + 4-byte distance)

        // Emit two nops (the target is right after these)
        builder.append_opcode(CEE_NOP); // offset 6
        builder.append_opcode(CEE_NOP); // offset 7

        // Place the label at offset 8
        builder.append_label("after");

        // Distance: label_pos(8) - (placeholder_pos(2) + 4) = 8 - 6 = 2
        let bytes = builder.get_bytes();
        assert_eq!(bytes[0], 0x00); // nop
        assert_eq!(bytes[1], 0xDD); // leave
        assert_eq!(bytes[2], 0x02); // distance low byte
        assert_eq!(bytes[3], 0x00);
        assert_eq!(bytes[4], 0x00);
        assert_eq!(bytes[5], 0x00);
        assert_eq!(bytes[6], 0x00); // nop
        assert_eq!(bytes[7], 0x00); // nop
    }

    #[test]
    fn jump_label_zero_distance() {
        let mut builder = InstructionBuilder::new();
        builder.append_jump(CEE_LEAVE as u8, "here");
        // Label immediately after the 5-byte jump: distance = 5 - (1 + 4) = 0
        builder.append_label("here");

        let bytes = builder.get_bytes();
        assert_eq!(bytes[0], 0xDD); // leave
        assert_eq!(bytes[1], 0x00); // distance = 0
        assert_eq!(bytes[2], 0x00);
        assert_eq!(bytes[3], 0x00);
        assert_eq!(bytes[4], 0x00);
    }

    #[test]
    fn jump_auto_generates_unique_labels() {
        let mut builder = InstructionBuilder::new();
        let label1 = builder.append_jump_auto(CEE_LEAVE as u8);
        let label2 = builder.append_jump_auto(CEE_LEAVE as u8);
        assert_ne!(label1, label2);
    }

    #[test]
    fn multiple_jumps_to_same_label() {
        let mut builder = InstructionBuilder::new();
        builder.append_jump(CEE_LEAVE as u8, "target"); // offset 0, 5 bytes
        builder.append_jump(CEE_LEAVE as u8, "target"); // offset 5, 5 bytes
        builder.append_label("target"); // offset 10

        let bytes = builder.get_bytes();

        // First jump: distance = 10 - (1 + 4) = 5
        assert_eq!(bytes[1], 0x05);
        assert_eq!(bytes[2], 0x00);

        // Second jump: distance = 10 - (6 + 4) = 0
        assert_eq!(bytes[6], 0x00);
        assert_eq!(bytes[7], 0x00);
    }

    // ==================== User code insertion ====================

    #[test]
    fn append_user_code_records_offset() {
        let mut builder = InstructionBuilder::new();
        builder.append_opcode(CEE_NOP);
        builder.append_opcode(CEE_NOP);
        builder.append_user_code(&[0x2A]); // ret
        assert_eq!(builder.get_user_code_offset(), 2);
        assert_eq!(builder.get_bytes(), &[0x00, 0x00, 0x2A]);
    }

    // ==================== Exception clause tracking ====================

    #[test]
    fn exception_clause_tracking() {
        let mut builder = InstructionBuilder::new();

        // try {
        builder.append_try_start();
        builder.append_opcode(CEE_NOP); // offset 0
        builder.append_opcode(CEE_NOP); // offset 1
        builder.append_try_end(); // try: offset=0, length=2

        // } catch(Exception) {
        builder.append_catch_start(0x0100_0042); // token for System.Exception
        builder.append_opcode(CEE_POP); // offset 2
        builder.append_catch_end(); // handler: offset=2, length=1

        let clauses = builder.get_completed_clauses();
        assert_eq!(clauses.len(), 1);
        assert_eq!(clauses[0].flags, 0); // catch
        assert_eq!(clauses[0].try_offset, 0);
        assert_eq!(clauses[0].try_length, 2);
        assert_eq!(clauses[0].handler_offset, 2);
        assert_eq!(clauses[0].handler_length, 1);
        assert_eq!(clauses[0].class_token, 0x0100_0042);
    }

    #[test]
    fn nested_exception_clauses() {
        let mut builder = InstructionBuilder::new();

        // Outer try
        builder.append_try_start();
        builder.append_opcode(CEE_NOP); // 0

        // Inner try
        builder.append_try_start();
        builder.append_opcode(CEE_NOP); // 1
        builder.append_try_end();

        builder.append_catch_start(0x01000001);
        builder.append_opcode(CEE_POP); // 2
        builder.append_catch_end();

        builder.append_try_end();

        builder.append_catch_start(0x01000002);
        builder.append_opcode(CEE_POP); // 3
        builder.append_catch_end();

        // Inner clause completes first (stack-based)
        let clauses = builder.get_completed_clauses();
        assert_eq!(clauses.len(), 2);
        // Inner clause
        assert_eq!(clauses[0].try_offset, 1);
        assert_eq!(clauses[0].class_token, 0x01000001);
        // Outer clause
        assert_eq!(clauses[1].try_offset, 0);
        assert_eq!(clauses[1].class_token, 0x01000002);
    }

    // ==================== Position tracking ====================

    #[test]
    fn position_tracks_bytes_written() {
        let mut builder = InstructionBuilder::new();
        assert_eq!(builder.position(), 0);
        builder.append_opcode(CEE_NOP);
        assert_eq!(builder.position(), 1);
        builder.append_opcode_u32(CEE_LDC_I4, 42);
        assert_eq!(builder.position(), 6); // 1 (nop) + 1 (opcode) + 4 (operand)
    }
}
