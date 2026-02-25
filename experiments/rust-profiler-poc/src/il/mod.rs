// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

//! IL (Intermediate Language) infrastructure for bytecode manipulation.
//!
//! This module provides the building blocks for generating and modifying
//! CIL bytecode, matching the C++ profiler's MethodRewriter subsystem.
//!
//! Modules:
//! - `opcodes` — CIL opcode constants
//! - `sig_compression` — ECMA-335 compressed integer encoding
//! - `instruction_builder` — Bytecode builder with jump labels and exception tracking
//! - `method_header` — IL method header parsing and writing (tiny/fat)
//! - `exception_handler` — Exception clause parsing, offset shifting, serialization
//! - `locals` — Local variable signature construction

pub mod opcodes;
pub mod sig_compression;
pub mod instruction_builder;
pub mod method_header;
pub mod exception_handler;
pub mod inject_default;
pub mod locals;

/// Error type for IL operations.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum IlError {
    /// Invalid or corrupt IL header
    InvalidHeader(String),
    /// Value too large for compressed encoding
    CompressionOverflow(u32),
    /// Unexpected end of data
    UnexpectedEnd,
    /// Invalid exception clause format
    InvalidExceptionClause(String),
    /// Jump label referenced but never defined
    UndefinedLabel(String),
    /// Token resolution failure
    TokenResolutionFailed(String),
    /// Generic IL generation error
    GenerationError(String),
}

impl std::fmt::Display for IlError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            IlError::InvalidHeader(msg) => write!(f, "Invalid IL header: {}", msg),
            IlError::CompressionOverflow(val) => {
                write!(f, "Value too large for compression: 0x{:08x}", val)
            }
            IlError::UnexpectedEnd => write!(f, "Unexpected end of data"),
            IlError::InvalidExceptionClause(msg) => {
                write!(f, "Invalid exception clause: {}", msg)
            }
            IlError::UndefinedLabel(label) => write!(f, "Undefined label: {}", label),
            IlError::TokenResolutionFailed(msg) => {
                write!(f, "Token resolution failed: {}", msg)
            }
            IlError::GenerationError(msg) => write!(f, "IL generation error: {}", msg),
        }
    }
}
