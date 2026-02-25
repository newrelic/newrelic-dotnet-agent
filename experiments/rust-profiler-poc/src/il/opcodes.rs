// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

//! CIL opcode constants used by the IL injection template.
//!
//! Opcode values match the ECMA-335 specification and the CLR's opcode.def.
//! Single-byte opcodes use the low byte directly; two-byte opcodes have
//! a 0xFE prefix and are stored as `0xFE00 | second_byte` for convenience.
//!
//! Only opcodes actually used by the injection template are defined here.
//! Additional opcodes can be added as the implementation expands.

#![allow(non_upper_case_globals)]

/// Opcode type: u16 to accommodate both 1-byte and 2-byte (0xFE-prefixed) opcodes.
pub type IlOpcode = u16;

// ==================== No operation ====================

pub const CEE_NOP: IlOpcode = 0x00;

// ==================== Load argument ====================

pub const CEE_LDARG_0: IlOpcode = 0x02;
pub const CEE_LDARG_1: IlOpcode = 0x03;
pub const CEE_LDARG_2: IlOpcode = 0x04;
pub const CEE_LDARG_3: IlOpcode = 0x05;
pub const CEE_LDARG_S: IlOpcode = 0x0E;
pub const CEE_LDARG: IlOpcode = 0xFE09;

// ==================== Load local ====================

pub const CEE_LDLOC_0: IlOpcode = 0x06;
pub const CEE_LDLOC_1: IlOpcode = 0x07;
pub const CEE_LDLOC_2: IlOpcode = 0x08;
pub const CEE_LDLOC_3: IlOpcode = 0x09;
pub const CEE_LDLOC_S: IlOpcode = 0x11;
pub const CEE_LDLOC: IlOpcode = 0xFE0C;

// ==================== Store local ====================

pub const CEE_STLOC_0: IlOpcode = 0x0A;
pub const CEE_STLOC_1: IlOpcode = 0x0B;
pub const CEE_STLOC_2: IlOpcode = 0x0C;
pub const CEE_STLOC_3: IlOpcode = 0x0D;
pub const CEE_STLOC_S: IlOpcode = 0x13;
pub const CEE_STLOC: IlOpcode = 0xFE0E;

// ==================== Load constant ====================

pub const CEE_LDNULL: IlOpcode = 0x14;
pub const CEE_LDC_I4_M1: IlOpcode = 0x15;
pub const CEE_LDC_I4_0: IlOpcode = 0x16;
pub const CEE_LDC_I4_1: IlOpcode = 0x17;
pub const CEE_LDC_I4_2: IlOpcode = 0x18;
pub const CEE_LDC_I4_3: IlOpcode = 0x19;
pub const CEE_LDC_I4_4: IlOpcode = 0x1A;
pub const CEE_LDC_I4_5: IlOpcode = 0x1B;
pub const CEE_LDC_I4_6: IlOpcode = 0x1C;
pub const CEE_LDC_I4_7: IlOpcode = 0x1D;
pub const CEE_LDC_I4_8: IlOpcode = 0x1E;
pub const CEE_LDC_I4_S: IlOpcode = 0x1F;
pub const CEE_LDC_I4: IlOpcode = 0x20;
pub const CEE_LDC_I8: IlOpcode = 0x21;

// ==================== Stack manipulation ====================

pub const CEE_DUP: IlOpcode = 0x25;
pub const CEE_POP: IlOpcode = 0x26;

// ==================== Call ====================

pub const CEE_CALL: IlOpcode = 0x28;
pub const CEE_RET: IlOpcode = 0x2A;

// ==================== Branch ====================

pub const CEE_BR_S: IlOpcode = 0x2B;
pub const CEE_BRFALSE_S: IlOpcode = 0x2C;
pub const CEE_BRTRUE_S: IlOpcode = 0x2D;
pub const CEE_BR: IlOpcode = 0x38;
pub const CEE_BRFALSE: IlOpcode = 0x39;
pub const CEE_BRTRUE: IlOpcode = 0x3A;

// ==================== Object model ====================

pub const CEE_CALLVIRT: IlOpcode = 0x6F;
pub const CEE_LDSTR: IlOpcode = 0x72;
pub const CEE_NEWOBJ: IlOpcode = 0x73;
pub const CEE_CASTCLASS: IlOpcode = 0x74;
pub const CEE_ISINST: IlOpcode = 0x75;

// ==================== Array ====================

pub const CEE_NEWARR: IlOpcode = 0x8D;
pub const CEE_STELEM_REF: IlOpcode = 0xA2;

// ==================== Boxing ====================

pub const CEE_BOX: IlOpcode = 0x8C;
pub const CEE_UNBOX_ANY: IlOpcode = 0xA5;

// ==================== Exception handling ====================

pub const CEE_THROW: IlOpcode = 0x7A;
pub const CEE_LEAVE: IlOpcode = 0xDD;
pub const CEE_LEAVE_S: IlOpcode = 0xDE;
pub const CEE_RETHROW: IlOpcode = 0xFE1A;
pub const CEE_ENDFINALLY: IlOpcode = 0xDC;

// ==================== Token operations ====================

pub const CEE_LDTOKEN: IlOpcode = 0xD0;

/// Returns true if the opcode requires a 2-byte encoding (0xFE prefix).
pub fn is_two_byte_opcode(opcode: IlOpcode) -> bool {
    opcode > 0xFF
}

/// Returns the encoded byte(s) for an opcode.
/// Single-byte opcodes return 1 byte; two-byte opcodes return `[0xFE, low_byte]`.
pub fn encode_opcode(opcode: IlOpcode) -> Vec<u8> {
    if is_two_byte_opcode(opcode) {
        vec![0xFE, (opcode & 0xFF) as u8]
    } else {
        vec![opcode as u8]
    }
}

/// Returns the optimized ldc.i4 opcode for a small integer value.
/// Values -1 through 8 use single-byte encodings; 9-127 use ldc.i4.s;
/// larger values use ldc.i4.
pub fn ldc_i4_opcode_for(value: i32) -> (IlOpcode, Option<i32>) {
    match value {
        -1 => (CEE_LDC_I4_M1, None),
        0 => (CEE_LDC_I4_0, None),
        1 => (CEE_LDC_I4_1, None),
        2 => (CEE_LDC_I4_2, None),
        3 => (CEE_LDC_I4_3, None),
        4 => (CEE_LDC_I4_4, None),
        5 => (CEE_LDC_I4_5, None),
        6 => (CEE_LDC_I4_6, None),
        7 => (CEE_LDC_I4_7, None),
        8 => (CEE_LDC_I4_8, None),
        v if v >= -128 && v <= 127 => (CEE_LDC_I4_S, Some(v)),
        v => (CEE_LDC_I4, Some(v)),
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn single_byte_opcodes_are_in_range() {
        assert!(!is_two_byte_opcode(CEE_NOP));
        assert!(!is_two_byte_opcode(CEE_LDNULL));
        assert!(!is_two_byte_opcode(CEE_RET));
        assert!(!is_two_byte_opcode(CEE_LEAVE));
        assert!(!is_two_byte_opcode(CEE_THROW));
    }

    #[test]
    fn two_byte_opcodes_are_in_range() {
        assert!(is_two_byte_opcode(CEE_RETHROW));
        assert!(is_two_byte_opcode(CEE_LDARG));
        assert!(is_two_byte_opcode(CEE_LDLOC));
        assert!(is_two_byte_opcode(CEE_STLOC));
    }

    #[test]
    fn encode_single_byte_opcode() {
        assert_eq!(encode_opcode(CEE_NOP), vec![0x00]);
        assert_eq!(encode_opcode(CEE_LDNULL), vec![0x14]);
        assert_eq!(encode_opcode(CEE_RET), vec![0x2A]);
    }

    #[test]
    fn encode_two_byte_opcode() {
        assert_eq!(encode_opcode(CEE_RETHROW), vec![0xFE, 0x1A]);
        assert_eq!(encode_opcode(CEE_LDARG), vec![0xFE, 0x09]);
        assert_eq!(encode_opcode(CEE_LDLOC), vec![0xFE, 0x0C]);
        assert_eq!(encode_opcode(CEE_STLOC), vec![0xFE, 0x0E]);
    }

    #[test]
    fn ldc_i4_optimization() {
        // Values -1 through 8 get single-byte encodings
        assert_eq!(ldc_i4_opcode_for(-1), (CEE_LDC_I4_M1, None));
        assert_eq!(ldc_i4_opcode_for(0), (CEE_LDC_I4_0, None));
        assert_eq!(ldc_i4_opcode_for(8), (CEE_LDC_I4_8, None));

        // 9-127 use ldc.i4.s
        assert_eq!(ldc_i4_opcode_for(9), (CEE_LDC_I4_S, Some(9)));
        assert_eq!(ldc_i4_opcode_for(127), (CEE_LDC_I4_S, Some(127)));

        // Larger values use ldc.i4
        assert_eq!(ldc_i4_opcode_for(128), (CEE_LDC_I4, Some(128)));
        assert_eq!(ldc_i4_opcode_for(1000), (CEE_LDC_I4, Some(1000)));

        // Negative values beyond -1 use ldc.i4.s for -128..-2, ldc.i4 otherwise
        assert_eq!(ldc_i4_opcode_for(-2), (CEE_LDC_I4_S, Some(-2)));
        assert_eq!(ldc_i4_opcode_for(-128), (CEE_LDC_I4_S, Some(-128)));
        assert_eq!(ldc_i4_opcode_for(-129), (CEE_LDC_I4, Some(-129)));
    }

    #[test]
    fn ldarg_short_forms() {
        // ldarg.0 through ldarg.3 are sequential
        assert_eq!(CEE_LDARG_0, 0x02);
        assert_eq!(CEE_LDARG_1, 0x03);
        assert_eq!(CEE_LDARG_2, 0x04);
        assert_eq!(CEE_LDARG_3, 0x05);
        assert_eq!(CEE_LDARG_1 - CEE_LDARG_0, 1);
        assert_eq!(CEE_LDARG_3 - CEE_LDARG_0, 3);
    }

    #[test]
    fn ldloc_short_forms() {
        // ldloc.0 through ldloc.3 are sequential
        assert_eq!(CEE_LDLOC_0, 0x06);
        assert_eq!(CEE_LDLOC_3, 0x09);
        assert_eq!(CEE_LDLOC_3 - CEE_LDLOC_0, 3);
    }

    #[test]
    fn stloc_short_forms() {
        // stloc.0 through stloc.3 are sequential
        assert_eq!(CEE_STLOC_0, 0x0A);
        assert_eq!(CEE_STLOC_3, 0x0D);
        assert_eq!(CEE_STLOC_3 - CEE_STLOC_0, 3);
    }
}
