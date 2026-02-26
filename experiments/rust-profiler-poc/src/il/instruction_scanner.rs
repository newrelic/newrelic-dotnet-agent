// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

//! CIL instruction scanner and ret rewriter.
//!
//! Provides bytecode-level parsing of CIL instruction streams and preprocessing
//! of user code for instrumentation. The primary use is rewriting `ret`
//! instructions before wrapping user code in a try-catch block.
//!
//! The C++ profiler's equivalent is `FunctionPreprocessor` in
//! `Profiler/FunctionPreprocessor.h`.

use super::IlError;

/// Operand size for the `switch` instruction is variable.
/// Stored as a sentinel in the lookup table; handled specially during scanning.
const SWITCH_OPERAND: i8 = -2;

/// Invalid/undefined opcode sentinel.
const INVALID_OPCODE: i8 = -1;

/// Operand sizes for single-byte opcodes (0x00-0xFF).
/// Each entry is the operand size in bytes, or INVALID_OPCODE for undefined opcodes.
/// The `switch` opcode (0x45) uses SWITCH_OPERAND.
///
/// Source: ECMA-335 Partition III, opcode definitions.
#[rustfmt::skip]
static SINGLE_BYTE_OPERAND_SIZE: [i8; 256] = [
    // 0x00-0x0F
    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1,
    // 0x10-0x1F
    1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1,
    // 0x20-0x2F: ldc.i4(4), ldc.i8(8), ldc.r4(4), ldc.r8(8), UNUSED, dup, pop, jmp(4), call(4), calli(4), ret, br.s(1), brfalse.s(1), brtrue.s(1), beq.s(1), bge.s(1)
    4, 8, 4, 8, INVALID_OPCODE, 0, 0, 4, 4, 4, 0, 1, 1, 1, 1, 1,
    // 0x30-0x3F: bgt.s(1), ble.s(1), blt.s(1), bne.un.s(1), bge.un.s(1), bgt.un.s(1), ble.un.s(1), blt.un.s(1), br(4), brfalse(4), brtrue(4), beq(4), bge(4), bgt(4), ble(4), blt(4)
    1, 1, 1, 1, 1, 1, 1, 1, 4, 4, 4, 4, 4, 4, 4, 4,
    // 0x40-0x4F: bne.un(4), bge.un(4), bgt.un(4), ble.un(4), blt.un(4), switch(VARIABLE), ldind.i1-ldind.r8
    4, 4, 4, 4, 4, SWITCH_OPERAND, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
    // 0x50-0x5F: ldind.ref, stind.ref, stind.i1-i8, stind.r4, stind.r8, add-rem.un
    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
    // 0x60-0x6F: and-conv.u8, callvirt(4)
    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 4,
    // 0x70-0x7F: cpobj(4), ldobj(4), ldstr(4), newobj(4), castclass(4), isinst(4), conv.r.un, UNUSED, UNUSED, unbox(4), throw, ldfld(4), ldflda(4), stfld(4), ldsfld(4), ldsflda(4)
    4, 4, 4, 4, 4, 4, 0, INVALID_OPCODE, INVALID_OPCODE, 4, 0, 4, 4, 4, 4, 4,
    // 0x80-0x8F: stsfld(4), stobj(4), conv.ovf.i1.un-conv.ovf.u.un, box(4), newarr(4), ldlen, ldelema(4)
    4, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 4, 4, 0, 4,
    // 0x90-0x9F: ldelem.i1-stelem.r8
    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
    // 0xA0-0xAF: stelem.r4, stelem.r8, stelem.ref, ldelem(4), stelem(4), unbox.any(4), UNUSED(6)...
    0, 0, 0, 4, 4, 4, INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE,
    // 0xB0-0xBF: UNUSED(3), conv.ovf.i1-conv.ovf.u8, UNUSED(7)
    INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE, 0, 0, 0, 0, 0, 0, 0, 0, INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE,
    // 0xC0-0xCF: UNUSED(2), refanyval(4), ckfinite, UNUSED(2), mkrefany(4), UNUSED(9), ldtoken(4)
    INVALID_OPCODE, INVALID_OPCODE, 4, 0, INVALID_OPCODE, INVALID_OPCODE, 4, INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE,
    // 0xD0-0xDF: ldtoken(4), conv.u2, conv.u1, conv.i, conv.ovf.i-sub.ovf.un, endfinally, leave(4), leave.s(1), stind.i, conv.u
    4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 4, 1, 0,
    // 0xE0-0xEF: conv.u, UNUSED(29)
    0, INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE,
    // 0xF0-0xFF: UNUSED(14), PREFIX(0xFE), PREFIXREF(0xFF)
    INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE, INVALID_OPCODE,
];

/// Operand sizes for two-byte opcodes (0xFE XX), indexed by the second byte.
/// Only indices 0x00-0x1E are defined.
#[rustfmt::skip]
static TWO_BYTE_OPERAND_SIZE: [i8; 32] = [
    // 0xFE00-0xFE0F: arglist, ceq, cgt, cgt.un, clt, clt.un, ldftn(4), ldvirtftn(4), UNUSED, ldarg(2), ldarga(2), starg(2), ldloc(2), ldloca(2), stloc(2), localloc
    0, 0, 0, 0, 0, 0, 4, 4, INVALID_OPCODE, 2, 2, 2, 2, 2, 2, 0,
    // 0xFE10-0xFE1F: UNUSED, endfilter, unaligned.(1), volatile., tail., initobj(4), constrained.(4), cpblk, initblk, no.(1), rethrow, UNUSED, sizeof(4), refanytype, readonly.
    INVALID_OPCODE, 0, 1, 0, 0, 4, 4, 0, 0, 1, 0, INVALID_OPCODE, 4, 0, 0, INVALID_OPCODE,
];

/// A parsed CIL instruction.
#[derive(Debug, Clone)]
pub struct IlInstruction {
    /// Byte offset in the original code stream.
    pub offset: usize,
    /// Opcode value. Single-byte opcodes use the low byte directly.
    /// Two-byte opcodes are stored as `0xFE00 | second_byte`.
    pub opcode: u16,
    /// Size of the opcode encoding (1 or 2 bytes).
    pub opcode_size: usize,
    /// Size of the operand in bytes.
    pub operand_size: usize,
    /// Total instruction size (opcode_size + operand_size).
    pub total_size: usize,
}

impl IlInstruction {
    /// Returns true if this instruction is a `ret` (0x2A).
    pub fn is_ret(&self) -> bool {
        self.opcode == 0x2A
    }

    /// Returns true if this is a branch instruction (short or long form).
    pub fn is_branch(&self) -> bool {
        matches!(self.opcode,
            0x2B..=0x37 |  // short branches (br.s through blt.un.s)
            0x38..=0x44 |  // long branches (br through blt.un)
            0xDD | 0xDE    // leave, leave.s
        )
    }

    /// Returns true if this is a short-form branch (1-byte offset).
    pub fn is_short_branch(&self) -> bool {
        matches!(self.opcode,
            0x2B..=0x37 | 0xDE  // short branches + leave.s
        )
    }

    /// Returns the branch target offset (absolute position in the code stream).
    /// Only valid for branch instructions.
    pub fn branch_target(&self, code: &[u8]) -> Option<usize> {
        if !self.is_branch() {
            return None;
        }
        let operand_offset = self.offset + self.opcode_size;
        let next_instruction = self.offset + self.total_size;

        if self.is_short_branch() {
            let relative = code[operand_offset] as i8;
            Some((next_instruction as isize + relative as isize) as usize)
        } else {
            let relative = i32::from_le_bytes([
                code[operand_offset],
                code[operand_offset + 1],
                code[operand_offset + 2],
                code[operand_offset + 3],
            ]);
            Some((next_instruction as isize + relative as isize) as usize)
        }
    }
}

/// Scan a CIL code stream into individual instructions.
///
/// Walks the bytecode from start to end, recognizing instruction boundaries
/// using the opcode operand size lookup tables. Returns an error if an
/// undefined opcode is encountered or the code stream is malformed.
pub fn scan_instructions(code: &[u8]) -> Result<Vec<IlInstruction>, IlError> {
    let mut instructions = Vec::new();
    let mut pos = 0;

    while pos < code.len() {
        let byte = code[pos];

        let (opcode, opcode_size, operand_size) = if byte == 0xFE {
            // Two-byte opcode
            if pos + 1 >= code.len() {
                return Err(IlError::UnexpectedEnd);
            }
            let second = code[pos + 1] as usize;
            if second >= TWO_BYTE_OPERAND_SIZE.len() {
                return Err(IlError::GenerationError(format!(
                    "Unknown two-byte opcode 0xFE{:02X} at offset {}",
                    second, pos
                )));
            }
            let op_size = TWO_BYTE_OPERAND_SIZE[second];
            if op_size == INVALID_OPCODE {
                return Err(IlError::GenerationError(format!(
                    "Undefined two-byte opcode 0xFE{:02X} at offset {}",
                    second, pos
                )));
            }
            (0xFE00 | second as u16, 2usize, op_size as usize)
        } else if byte == 0x45 {
            // Switch instruction: 4 bytes for count N, then N * 4 bytes for targets
            if pos + 5 > code.len() {
                return Err(IlError::UnexpectedEnd);
            }
            let n = u32::from_le_bytes([
                code[pos + 1],
                code[pos + 2],
                code[pos + 3],
                code[pos + 4],
            ]) as usize;
            let operand_size = 4 + n * 4; // count + targets
            (0x45u16, 1usize, operand_size)
        } else {
            let op_size = SINGLE_BYTE_OPERAND_SIZE[byte as usize];
            if op_size == INVALID_OPCODE {
                return Err(IlError::GenerationError(format!(
                    "Undefined opcode 0x{:02X} at offset {}",
                    byte, pos
                )));
            }
            (byte as u16, 1usize, op_size as usize)
        };

        let total_size = opcode_size + operand_size;
        if pos + total_size > code.len() {
            return Err(IlError::UnexpectedEnd);
        }

        instructions.push(IlInstruction {
            offset: pos,
            opcode,
            opcode_size,
            operand_size,
            total_size,
        });

        pos += total_size;
    }

    Ok(instructions)
}

/// Count the number of `ret` (0x2A) instructions in a code stream.
pub fn count_rets(instructions: &[IlInstruction]) -> usize {
    instructions.iter().filter(|i| i.is_ret()).count()
}

/// Result of preprocessing user code for instrumentation.
#[derive(Debug)]
pub struct PreprocessedCode {
    /// The rewritten bytecode.
    pub code: Vec<u8>,
    /// Mapping from old offset → new offset for each original instruction.
    /// Used to update exception handler offsets after ret rewriting.
    pub offset_map: Vec<(usize, usize)>,
}

/// Preprocess user code to handle `ret` instructions before wrapping in try-catch.
///
/// The CLR prohibits `ret` from executing inside a try block in a way that would
/// bypass our finish-tracer code. This preprocessor rewrites ret instructions:
///
/// - **0 rets**: Returns code unchanged.
/// - **1 ret** (at end): Replaces the final `ret` with `nop`.
/// - **Multiple rets**: Replaces the final `ret` with `nop` and all non-final
///   `ret` instructions with `br` (long branch) targeting the final instruction.
///   All branch offsets in existing instructions are recalculated.
///
/// Reference: C++ profiler's `FunctionPreprocessor::Process()` in
/// `Profiler/FunctionPreprocessor.h` lines 325-350.
pub fn preprocess_user_code(code: &[u8]) -> Result<PreprocessedCode, IlError> {
    let instructions = scan_instructions(code)?;
    let ret_count = count_rets(&instructions);

    if ret_count == 0 {
        return Ok(PreprocessedCode {
            code: code.to_vec(),
            offset_map: instructions.iter().map(|i| (i.offset, i.offset)).collect(),
        });
    }

    // Find the final instruction — it must be ret for valid IL.
    let last = instructions.last().ok_or(IlError::UnexpectedEnd)?;
    if !last.is_ret() {
        return Err(IlError::GenerationError(
            "Final instruction is not ret".to_string(),
        ));
    }

    if ret_count == 1 {
        // Simple case: nop the final ret.
        let mut new_code = code.to_vec();
        let ret_offset = last.offset;
        new_code[ret_offset] = 0x00; // CEE_NOP
        return Ok(PreprocessedCode {
            code: new_code,
            offset_map: instructions.iter().map(|i| (i.offset, i.offset)).collect(),
        });
    }

    // Multiple rets: rewrite non-final rets as `br` to the final instruction.
    // This changes instruction sizes (1 byte ret → 5 byte br), so all offsets shift.
    rewrite_multi_return(code, &instructions)
}

/// Rewrite a method with multiple `ret` instructions.
///
/// Strategy (matching C++ profiler's `FunctionPreprocessor::RewriteMultiReturnMethod`):
/// 1. Change the final `ret` → `nop`
/// 2. Change all non-final `ret` → `br` (long-form, 5 bytes) targeting the final nop
/// 3. Recalculate all branch offsets (expanding short → long if needed)
/// 4. Return the rewritten bytecode with old→new offset mapping
fn rewrite_multi_return(
    code: &[u8],
    instructions: &[IlInstruction],
) -> Result<PreprocessedCode, IlError> {
    let final_ret_idx = instructions.len() - 1;

    // Phase 1: Calculate new sizes for each instruction.
    // Non-final rets grow from 1 byte to 5 bytes (br + i32).
    // Short branches may need expansion to long form if offsets shift too much.
    let mut new_sizes: Vec<usize> = instructions.iter().map(|i| i.total_size).collect();

    // Mark non-final rets for expansion (1 → 5 bytes)
    for (idx, inst) in instructions.iter().enumerate() {
        if inst.is_ret() && idx != final_ret_idx {
            new_sizes[idx] = 5; // br (1) + i32 offset (4)
        }
    }

    // Calculate preliminary new offsets
    let mut new_offsets: Vec<usize> = Vec::with_capacity(instructions.len());
    let mut offset = 0;
    for &size in &new_sizes {
        new_offsets.push(offset);
        offset += size;
    }
    let new_code_size = offset;

    // Check if any short branches need expansion to long form.
    // A short branch has a signed byte operand (-128..127). If the distance
    // between the branch and its target exceeds that after offset changes,
    // we need to expand it.
    let needs_expansion = new_code_size > code.len() + 127;

    if needs_expansion {
        // Expand ALL short branches to long form (matching C++ profiler behavior).
        // Short branch opcodes 0x2B-0x37 become 0x38-0x44 (same order).
        // leave.s (0xDE) becomes leave (0xDD).
        // Size: short = opcode(1) + i8(1) = 2 → long = opcode(1) + i32(4) = 5.
        for (idx, inst) in instructions.iter().enumerate() {
            if inst.is_short_branch() {
                new_sizes[idx] = 5; // opcode(1) + i32(4)
            }
        }

        // Recalculate offsets after expansion
        new_offsets.clear();
        let mut offset = 0;
        for &size in &new_sizes {
            new_offsets.push(offset);
            offset += size;
        }
    }

    // Phase 2: Build the new bytecode.
    let final_nop_offset = new_offsets[final_ret_idx];
    let mut new_code = Vec::with_capacity(offset);

    for (idx, inst) in instructions.iter().enumerate() {
        let inst_start = new_code.len();
        debug_assert_eq!(inst_start, new_offsets[idx]);

        if inst.is_ret() && idx == final_ret_idx {
            // Final ret → nop
            new_code.push(0x00);
        } else if inst.is_ret() {
            // Non-final ret → br to final nop
            let next_inst_offset = inst_start + 5; // br(1) + i32(4)
            let relative = final_nop_offset as i32 - next_inst_offset as i32;
            new_code.push(0x38); // CEE_BR
            new_code.extend_from_slice(&relative.to_le_bytes());
        } else if needs_expansion && inst.is_short_branch() {
            // Expand short branch to long form
            let long_opcode = short_to_long_branch(inst.opcode);
            new_code.push(long_opcode as u8);

            // Recalculate the branch target
            let old_target = inst
                .branch_target(code)
                .ok_or_else(|| IlError::GenerationError("Missing branch target".into()))?;

            // Find the instruction at the old target offset
            let target_idx = instructions
                .iter()
                .position(|i| i.offset == old_target)
                .ok_or_else(|| {
                    IlError::GenerationError(format!(
                        "Branch target offset {} not found",
                        old_target
                    ))
                })?;

            let new_target = new_offsets[target_idx];
            let next_inst_offset = inst_start + 5;
            let relative = new_target as i32 - next_inst_offset as i32;
            new_code.extend_from_slice(&relative.to_le_bytes());
        } else if inst.is_branch() && new_offsets != offset_identity(instructions) {
            // Existing branch with changed offsets — recalculate target
            let operand_start = inst.offset + inst.opcode_size;

            // Copy opcode bytes
            for i in 0..inst.opcode_size {
                new_code.push(code[inst.offset + i]);
            }

            let old_target = inst
                .branch_target(code)
                .ok_or_else(|| IlError::GenerationError("Missing branch target".into()))?;

            let target_idx = instructions
                .iter()
                .position(|i| i.offset == old_target)
                .ok_or_else(|| {
                    // Target might be at code.len() (past the end) — e.g., leave targeting
                    // the instruction after the last one. Map it to the new end.
                    IlError::GenerationError(format!(
                        "Branch target offset {} not found at instruction boundary",
                        old_target
                    ))
                });

            match target_idx {
                Ok(tidx) => {
                    let new_target = new_offsets[tidx];
                    let next_inst_offset = inst_start + new_sizes[idx];

                    if inst.is_short_branch() {
                        let relative = new_target as i32 - next_inst_offset as i32;
                        if relative < -128 || relative > 127 {
                            return Err(IlError::GenerationError(format!(
                                "Short branch offset {} out of range at instruction offset {}",
                                relative, inst.offset
                            )));
                        }
                        new_code.push(relative as i8 as u8);
                    } else {
                        let relative = new_target as i32 - next_inst_offset as i32;
                        new_code.extend_from_slice(&relative.to_le_bytes());
                    }
                }
                Err(_) => {
                    // Fall back: copy original operand bytes unchanged.
                    // This handles edge cases like branches to synthetic targets.
                    for i in 0..inst.operand_size {
                        new_code.push(code[operand_start + i]);
                    }
                }
            }
        } else {
            // Copy instruction unchanged
            for i in 0..inst.total_size {
                new_code.push(code[inst.offset + i]);
            }
        }
    }

    let offset_map: Vec<(usize, usize)> = instructions
        .iter()
        .zip(new_offsets.iter())
        .map(|(inst, &new_off)| (inst.offset, new_off))
        .collect();

    Ok(PreprocessedCode {
        code: new_code,
        offset_map,
    })
}

/// Helper: check if offsets haven't changed (identity mapping).
fn offset_identity(instructions: &[IlInstruction]) -> Vec<usize> {
    instructions.iter().map(|i| i.offset).collect()
}

/// Convert a short-form branch opcode to its long-form equivalent.
///
/// Short branches 0x2B-0x37 → long branches 0x38-0x44 (offset +0x0D).
/// leave.s (0xDE) → leave (0xDD).
fn short_to_long_branch(opcode: u16) -> u16 {
    match opcode {
        0x2B..=0x37 => opcode + 0x0D, // br.s→br, brfalse.s→brfalse, etc.
        0xDE => 0xDD,                  // leave.s → leave
        _ => opcode,                   // not a short branch, return unchanged
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    // ===== Instruction scanner tests =====

    #[test]
    fn scan_empty_code() {
        let instructions = scan_instructions(&[]).unwrap();
        assert!(instructions.is_empty());
    }

    #[test]
    fn scan_single_ret() {
        let code = [0x2A]; // ret
        let instructions = scan_instructions(&code).unwrap();
        assert_eq!(instructions.len(), 1);
        assert_eq!(instructions[0].opcode, 0x2A);
        assert_eq!(instructions[0].total_size, 1);
        assert!(instructions[0].is_ret());
    }

    #[test]
    fn scan_nop_ret() {
        let code = [0x00, 0x2A]; // nop, ret
        let instructions = scan_instructions(&code).unwrap();
        assert_eq!(instructions.len(), 2);
        assert_eq!(instructions[0].offset, 0);
        assert_eq!(instructions[0].opcode, 0x00);
        assert_eq!(instructions[1].offset, 1);
        assert!(instructions[1].is_ret());
    }

    #[test]
    fn scan_ldstr_call_ret() {
        // ldstr token(4), call token(4), ret
        let code = [0x72, 0x01, 0x00, 0x00, 0x70, 0x28, 0x02, 0x00, 0x00, 0x06, 0x2A];
        let instructions = scan_instructions(&code).unwrap();
        assert_eq!(instructions.len(), 3);
        assert_eq!(instructions[0].opcode, 0x72); // ldstr
        assert_eq!(instructions[0].total_size, 5); // opcode + token
        assert_eq!(instructions[1].opcode, 0x28); // call
        assert_eq!(instructions[1].offset, 5);
        assert_eq!(instructions[2].opcode, 0x2A); // ret
        assert_eq!(instructions[2].offset, 10);
    }

    #[test]
    fn scan_short_branch() {
        // br.s +3, nop, nop, nop, ret
        let code = [0x2B, 0x03, 0x00, 0x00, 0x00, 0x2A];
        let instructions = scan_instructions(&code).unwrap();
        assert_eq!(instructions.len(), 5);
        assert!(instructions[0].is_branch());
        assert!(instructions[0].is_short_branch());
        let target = instructions[0].branch_target(&code).unwrap();
        assert_eq!(target, 5); // offset 2 + 3 = 5
    }

    #[test]
    fn scan_long_branch() {
        // br +0x00000003, nop, nop, nop, ret
        let code = [0x38, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x2A];
        let instructions = scan_instructions(&code).unwrap();
        assert!(instructions[0].is_branch());
        assert!(!instructions[0].is_short_branch());
        let target = instructions[0].branch_target(&code).unwrap();
        assert_eq!(target, 8); // offset 5 + 3 = 8
    }

    #[test]
    fn scan_two_byte_opcode() {
        // rethrow = 0xFE 0x1A
        let code = [0xFE, 0x1A];
        let instructions = scan_instructions(&code).unwrap();
        assert_eq!(instructions.len(), 1);
        assert_eq!(instructions[0].opcode, 0xFE1A);
        assert_eq!(instructions[0].opcode_size, 2);
        assert_eq!(instructions[0].total_size, 2);
    }

    #[test]
    fn scan_stloc_two_byte() {
        // stloc 0x0005 (0xFE, 0x0E, 0x05, 0x00)
        let code = [0xFE, 0x0E, 0x05, 0x00];
        let instructions = scan_instructions(&code).unwrap();
        assert_eq!(instructions.len(), 1);
        assert_eq!(instructions[0].opcode, 0xFE0E);
        assert_eq!(instructions[0].total_size, 4); // 2 byte opcode + 2 byte operand
    }

    #[test]
    fn scan_ldc_i8() {
        // ldc.i8 0xDEADBEEFCAFEBABE
        let code = [0x21, 0xBE, 0xBA, 0xFE, 0xCA, 0xEF, 0xBE, 0xAD, 0xDE];
        let instructions = scan_instructions(&code).unwrap();
        assert_eq!(instructions.len(), 1);
        assert_eq!(instructions[0].total_size, 9); // 1 + 8
    }

    #[test]
    fn scan_switch_instruction() {
        // switch with 2 targets: [0x45, 0x02,0x00,0x00,0x00, target1(4), target2(4)]
        let code = [0x45, 0x02, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x0A, 0x00, 0x00, 0x00, 0x2A];
        let instructions = scan_instructions(&code).unwrap();
        assert_eq!(instructions[0].opcode, 0x45);
        assert_eq!(instructions[0].total_size, 13); // 1 + 4 + 2*4
        assert_eq!(instructions[1].offset, 13);
        assert!(instructions[1].is_ret());
    }

    #[test]
    fn scan_truncated_code_fails() {
        // Incomplete ldc.i4 (needs 4 bytes after opcode, only 2 present)
        let code = [0x20, 0x01, 0x02];
        assert!(scan_instructions(&code).is_err());
    }

    #[test]
    fn scan_undefined_opcode_fails() {
        let code = [0x24]; // UNUSED opcode
        assert!(scan_instructions(&code).is_err());
    }

    #[test]
    fn count_rets_none() {
        let code = [0x00, 0x00]; // nop, nop
        let instructions = scan_instructions(&code).unwrap();
        assert_eq!(count_rets(&instructions), 0);
    }

    #[test]
    fn count_rets_single() {
        let code = [0x00, 0x2A]; // nop, ret
        let instructions = scan_instructions(&code).unwrap();
        assert_eq!(count_rets(&instructions), 1);
    }

    #[test]
    fn count_rets_multiple() {
        // brfalse.s +1, ret, nop, ret
        let code = [0x2C, 0x01, 0x2A, 0x00, 0x2A];
        let instructions = scan_instructions(&code).unwrap();
        assert_eq!(count_rets(&instructions), 2);
    }

    // ===== Ret rewriting tests =====

    #[test]
    fn preprocess_no_rets_returns_unchanged() {
        let code = [0x00, 0x00]; // nop, nop
        let result = preprocess_user_code(&code).unwrap();
        assert_eq!(result.code, code);
    }

    #[test]
    fn preprocess_single_ret_becomes_nop() {
        let code = [0x00, 0x2A]; // nop, ret
        let result = preprocess_user_code(&code).unwrap();
        assert_eq!(result.code, [0x00, 0x00]); // nop, nop
    }

    #[test]
    fn preprocess_single_ret_offset_map_is_identity() {
        let code = [0x00, 0x2A];
        let result = preprocess_user_code(&code).unwrap();
        assert_eq!(result.offset_map, vec![(0, 0), (1, 1)]);
    }

    #[test]
    fn preprocess_two_rets_first_becomes_br() {
        // ldarg.0(0x02), ret(0x2A), nop(0x00), ret(0x2A)
        // The first ret (at offset 1) should become br to the final nop.
        // Final ret (at offset 3) should become nop.
        let code = [0x02, 0x2A, 0x00, 0x2A];
        let result = preprocess_user_code(&code).unwrap();

        // First instruction (ldarg.0) unchanged: 1 byte at new offset 0
        assert_eq!(result.code[0], 0x02);

        // Second instruction (was ret): now br (0x38) + i32 offset = 5 bytes at new offset 1
        assert_eq!(result.code[1], 0x38); // CEE_BR

        // Third instruction (nop) at new offset 6
        assert_eq!(result.code[6], 0x00);

        // Fourth instruction (was ret): now nop at new offset 7
        assert_eq!(result.code[7], 0x00);

        // Total size: 1 + 5 + 1 + 1 = 8
        assert_eq!(result.code.len(), 8);

        // br target should point to offset 7 (the final nop)
        let br_operand = i32::from_le_bytes([
            result.code[2],
            result.code[3],
            result.code[4],
            result.code[5],
        ]);
        // next_inst after br = offset 1 + 5 = 6
        // target = 7, so relative = 7 - 6 = 1
        assert_eq!(br_operand, 1);
    }

    #[test]
    fn preprocess_multi_ret_offset_map_reflects_growth() {
        // ldarg.0(0x02), ret(0x2A), nop(0x00), ret(0x2A)
        let code = [0x02, 0x2A, 0x00, 0x2A];
        let result = preprocess_user_code(&code).unwrap();

        // Old offsets: [0, 1, 2, 3]
        // New offsets: [0, 1, 6, 7] (ret at 1 grew from 1→5 bytes)
        assert_eq!(result.offset_map[0], (0, 0));
        assert_eq!(result.offset_map[1], (1, 1));
        assert_eq!(result.offset_map[2], (2, 6));
        assert_eq!(result.offset_map[3], (3, 7));
    }

    #[test]
    fn preprocess_multi_ret_with_branch_recalculates_target() {
        // br.s +2, nop, ret, nop, ret
        // Branch at offset 0 targets offset 4 (nop before final ret).
        // After rewriting, the middle ret grows to 5 bytes (br),
        // so the nop at old offset 3 shifts.
        let code = [0x2B, 0x02, 0x00, 0x2A, 0x00, 0x2A];
        let result = preprocess_user_code(&code).unwrap();

        // Original instructions: br.s(2), nop(1), ret(1), nop(1), ret(1)
        // New sizes:             br.s(2), nop(1), br(5), nop(1), nop(1)
        // New offsets:           [0, 2, 3, 8, 9]
        assert_eq!(result.offset_map[0], (0, 0)); // br.s
        assert_eq!(result.offset_map[1], (2, 2)); // nop
        assert_eq!(result.offset_map[2], (3, 3)); // ret→br
        assert_eq!(result.offset_map[3], (4, 8)); // nop
        assert_eq!(result.offset_map[4], (5, 9)); // ret→nop

        // The br.s at offset 0 originally targeted offset 4.
        // Now it should target new offset 8 (where old offset 4 moved to).
        // br.s operand at byte 1: next_inst = 2, target = 8, relative = 6
        assert_eq!(result.code[1] as i8, 6);
    }

    #[test]
    fn preprocess_three_rets() {
        // ret, ret, ret — all three rets, first two become br to final nop
        let code = [0x2A, 0x2A, 0x2A];
        let result = preprocess_user_code(&code).unwrap();

        // Sizes: br(5), br(5), nop(1) = 11 bytes
        assert_eq!(result.code.len(), 11);

        // First br at offset 0, targets offset 10 (final nop)
        assert_eq!(result.code[0], 0x38);
        let br1 = i32::from_le_bytes([result.code[1], result.code[2], result.code[3], result.code[4]]);
        assert_eq!(br1, 10 - 5); // target(10) - next_inst(5)

        // Second br at offset 5, targets offset 10
        assert_eq!(result.code[5], 0x38);
        let br2 = i32::from_le_bytes([result.code[6], result.code[7], result.code[8], result.code[9]]);
        assert_eq!(br2, 10 - 10); // target(10) - next_inst(10)

        // Final nop at offset 10
        assert_eq!(result.code[10], 0x00);
    }

    #[test]
    fn preprocess_real_method_doSomeWork() {
        // Actual DoSomeWork IL code from ProfilerTestApp
        let code: Vec<u8> = vec![
            0x00, 0x16, 0x0A, 0x16, 0x0B, 0x2B, 0x0A, 0x00,
            0x06, 0x07, 0x58, 0x0A, 0x00, 0x07, 0x17, 0x58,
            0x0B, 0x07, 0x02, 0xFE, 0x04, 0x0C, 0x08, 0x2D,
            0xEE, 0x06, 0x0D, 0x2B, 0x00, 0x09, 0x2A,
        ];

        let instructions = scan_instructions(&code).unwrap();
        assert_eq!(count_rets(&instructions), 1);

        let result = preprocess_user_code(&code).unwrap();
        // Single ret → nop, same size
        assert_eq!(result.code.len(), code.len());
        assert_eq!(result.code[30], 0x00); // ret → nop
    }

    #[test]
    fn short_to_long_branch_mapping() {
        assert_eq!(short_to_long_branch(0x2B), 0x38); // br.s → br
        assert_eq!(short_to_long_branch(0x2C), 0x39); // brfalse.s → brfalse
        assert_eq!(short_to_long_branch(0x2D), 0x3A); // brtrue.s → brtrue
        assert_eq!(short_to_long_branch(0x37), 0x44); // blt.un.s → blt.un
        assert_eq!(short_to_long_branch(0xDE), 0xDD); // leave.s → leave
    }

    #[test]
    fn branch_target_calculation_short() {
        // br.s +5 at offset 0
        let code = [0x2B, 0x05, 0x00, 0x00, 0x00, 0x00, 0x00, 0x2A];
        let instructions = scan_instructions(&code).unwrap();
        // next_inst = 0 + 2 = 2, target = 2 + 5 = 7
        assert_eq!(instructions[0].branch_target(&code), Some(7));
    }

    #[test]
    fn branch_target_calculation_short_negative() {
        // Code: nop, nop, br.s -4
        let code = [0x00, 0x00, 0x2B, 0xFC]; // 0xFC = -4 as i8
        let instructions = scan_instructions(&code).unwrap();
        // br.s at offset 2, next_inst = 4, target = 4 + (-4) = 0
        assert_eq!(instructions[2].branch_target(&code), Some(0));
    }

    #[test]
    fn non_branch_returns_none_for_target() {
        let code = [0x00]; // nop
        let instructions = scan_instructions(&code).unwrap();
        assert_eq!(instructions[0].branch_target(&code), None);
    }
}
