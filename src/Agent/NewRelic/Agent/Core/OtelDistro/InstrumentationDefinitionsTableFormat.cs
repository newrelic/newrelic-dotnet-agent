// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>
/// The one place the "NRIDT" (New Relic Instrumentation Definitions Table) binary format is defined.
/// The reader (InstrumentationDefinitionsTable.cs) and the build-time writer both use these constants,
/// so the layout cannot drift between them. The generator project cannot reference Core, so it links this file in.
///
/// Why a binary table at all: the distro hands OTel's native profiler thousands of CallTarget definitions at start-up.
/// A giant array initializer of "new DistroCallTargetDefinition(...)" costs ~100-280 ms and ~12 allocations per
/// definition. A byte[] literal compiles to one allocation plus a memory copy, and this reader then decodes it with
/// far fewer allocations because of the sharing described below.
///
/// Rules for the whole file: little-endian only, and every index and count is unsigned. One byte order and no
/// signed indices means no sign-extension or byte-swap bugs (the kind found in the CP buffer parser).
///
/// Layout, in this order, with no gaps and no padding (the header offsets must agree with that):
///
///   Header        50 bytes
///   Strings       HeaderStringCount entries
///   Signatures    HeaderSignatureCount entries
///   Records       HeaderRecordCount x RecordSize bytes
///   Assemblies    HeaderAssemblyCount x AssemblyEntrySize bytes
///   Files         HeaderFileCount x FileEntrySize bytes
///
/// The header offsets are redundant with the counts and sizes while the sections are contiguous. They are kept so a
/// later reader can jump straight to one section (lazy per-assembly registration) without any format change.
///
/// A table is valid only if all of the following hold. This is the complete list of what the reader checks: it
/// rejects a table that breaks any of these rules, and the writer must guarantee all of them.
///   - the header is complete, starts with MagicText and carries FormatVersion;
///   - TotalLength equals the length of the buffer;
///   - the sections are contiguous, in the order above, with no padding, and each header offset is where the previous
///     section ended;
///   - every entry fits inside the buffer, and the files section ends exactly at TotalLength (no trailing bytes);
///   - every string index (in a signature, a record, an assembly entry or a file entry) and every signature index in a
///     record is in range, except a record's metric name, which is in range or NoString;
///   - every record's assembly, type, method, integration type and tracer factory strings are non-empty;
///   - every signature has at least one element;
///   - every record's assembly field equals the assembly string index of the assembly entry whose run holds it, and
///     the runs are in order, contiguous and cover every record exactly once;
///   - the file count is at most MaxFileCount, and every record's source file is PluginSourceFile or less than it;
///   - every file's home name is non-empty and no two files' home names are equal ignoring case (ordinal).
/// Accepted, not rejected: a run of length 0, an empty string as a signature element (the definition only needs the
/// list to be non-empty), an empty metric name or argument signature, any tracer arguments, function id, point key and
/// content hash value, reserved flag bits (record flags other than FlagDefaultOff and FlagLogForwarding, file flags other than
/// FileFlagCompiled; ignored), a record whose source file is a hash-only file, duplicate point keys, duplicate strings,
/// duplicate signatures, unused strings or signatures, and string bytes that are not valid UTF-8 (decoded with
/// replacement characters, as Encoding.UTF8 does).
///
/// Writer guarantees that the version 3 reader does not check: each assembly appears in exactly one assembly entry, and
/// the entries are in ordinal order of assembly name (a table with AsmA, AsmB, AsmA entries would be read as it is);
/// reserved flag bits are written as 0.
/// </summary>
internal static class InstrumentationDefinitionsTableFormat
{
    // ---- Header ---------------------------------------------------------------------------------------------------
    // Why: identifies the blob, lets the reader reject a table written by a different format version instead of
    // misreading it, and gives the position of every section. TotalLength lets the reader detect truncation.

    /// <summary>First five bytes of every table, ASCII.</summary>
    public const string MagicText = "NRIDT";

    /// <summary>Byte 5. The reader accepts exactly this version; any change to the layout bumps it.</summary>
    public const byte FormatVersion = 3;

    public const int VersionOffset = 5;

    // Eleven u32 fields follow the version byte: the entry count and byte offset of the first four sections, the total
    // length, then the count and offset of the files section (added in version 3, so placed after the total length to
    // keep every version 2 field where it was).
    public const int HeaderStringCount = 6;
    public const int HeaderStringsOffset = 10;
    public const int HeaderSignatureCount = 14;
    public const int HeaderSignaturesOffset = 18;
    public const int HeaderRecordCount = 22;
    public const int HeaderRecordsOffset = 26;
    public const int HeaderAssemblyCount = 30;
    public const int HeaderAssembliesOffset = 34;
    public const int HeaderTotalLength = 38;
    public const int HeaderFileCount = 42;
    public const int HeaderFilesOffset = 46;
    public const int HeaderSize = 50;

    // ---- Strings --------------------------------------------------------------------------------------------------
    // Each entry: u16 UTF-8 byte length, then the bytes. Every DISTINCT string appears exactly once, in order of first
    // use while writing the records in record order (assembly, type, method, each signature string, integration type,
    // tracer factory, metric name unless it is null, argument signature), then the file entries in file order (home
    // name).
    // Why: assemblies, types, parameter type names and tracer factories repeat heavily (an overload-wide match produces
    // nine arity variants of one method). The reader decodes each string once and every definition and site shares that
    // instance, which is the main allocation saving.

    public const int StringLengthSize = 2;

    /// <summary>Longest string the u16 length prefix can describe. The writer fails the build rather than exceed it.</summary>
    public const int MaxStringBytes = ushort.MaxValue;

    // ---- Signatures -----------------------------------------------------------------------------------------------
    // Each entry: u8 element count, then that many u32 string indices. Every DISTINCT list appears exactly once. An
    // entry is exactly what OTel wants in TargetSignatureTypes: element 0 is the return type ("_" = any), then the
    // parameter types. Why: arity variants and repeated signatures share one string[] instead of one each.

    public const int SignatureCountSize = 1;
    public const int SignatureElementSize = 4;

    /// <summary>Longest list the u8 count can describe. The writer fails the build rather than exceed it.</summary>
    public const int MaxSignatureElements = byte.MaxValue;

    // ---- Records --------------------------------------------------------------------------------------------------
    // One fixed-width record per definition. Why fixed width: record N starts at RecordsOffset + N * RecordSize, so it
    // can be read without parsing the records before it. The six u16 version bounds are the CallTarget min and max
    // assembly version.
    //
    // Since version 2 each record also carries its instrumentation site: the data the generated integration class used
    // to build in its own static constructor (6 string literals and a ulong per class, thousands of classes). Why in
    // the table: the strings are already decoded once and shared, and a site is then built lazily on the first call
    // that needs it. The site index of a record is its record index (its position after the sort by assembly).
    //   - tracer factory: the NR wrapper to run; required, like the other names.
    //   - metric name: the XML metricName, which may be absent. Null and "" are different values (the wrapper sees
    //     what the XML said), so null is the reserved index NoString, never a string. NoString can never be a real
    //     index: every string costs at least its 2-byte length prefix and TotalLength is a u32, so a table holds fewer
    //     than 0x80000000 strings.
    //   - argument signature: the comma-joined XML parameter types, "" for an overload-wide match; "" is allowed.
    //   - tracer arguments: u32, always 0 today. Kept so the site keeps its field and a later XML attribute needs no
    //     format change.
    //   - function id: u64, the generator's FunctionIdHasher value, which also names the integration class. Read as
    //     two little-endian u32 halves, low half first, so a value with the high bit set is read unchanged.
    //
    // Since version 3 each record also carries what the distro needs to turn its XML point off and on from the XML on
    // disk (the agent's enabled="false" and file edits), without re-reading anything at start-up:
    //   - point key: u64, the generator's PointKeyHasher value for the XML point the record came from. Every record of
    //     one point (the nine arity variants of an overload-wide match) shares it, so a point is turned off or on as a
    //     whole. Read like the function id.
    //   - source file: u16, the index in the files section of the shipped XML file the point came from, or
    //     PluginSourceFile for a point from the plugin's own XML (always on, in no home, so not tracked by hash).
    //   - flags: u16. Bit 0 FlagDefaultOff: the shipped XML has enabled="false" on the point's tracer factory, so the
    //     site starts off and is not registered until the XML on disk turns it on. Bit 1 FlagLogForwarding: the point is
    //     one of NR's log-forwarding wrappers (MEL Logger.Log, log4net/Sitecore CallAppenders, Serilog's dispatch, NLog
    //     LoggerImpl.Write), so the distro brackets the call with its OTel/NR log dedupe counter (DistroLogCapture). Other
    //     bits are reserved (0).

    public const int RecordSize = 68;
    public const int RecordAssembly = 0; // u32 string index
    public const int RecordType = 4; // u32 string index
    public const int RecordMethod = 8; // u32 string index
    public const int RecordIntegrationType = 12; // u32 string index
    public const int RecordSignature = 16; // u32 signature index
    public const int RecordMinMajor = 20; // u16
    public const int RecordMinMinor = 22; // u16
    public const int RecordMinPatch = 24; // u16
    public const int RecordMaxMajor = 26; // u16
    public const int RecordMaxMinor = 28; // u16
    public const int RecordMaxPatch = 30; // u16
    public const int RecordTracerFactory = 32; // u32 string index
    public const int RecordMetricName = 36; // u32 string index, or NoString for null
    public const int RecordArgumentSignature = 40; // u32 string index
    public const int RecordTracerArguments = 44; // u32
    public const int RecordFunctionId = 48; // u64
    public const int RecordPointKey = 56; // u64
    public const int RecordSourceFile = 64; // u16 file index, or PluginSourceFile
    public const int RecordFlags = 66; // u16

    /// <summary>Metric name index meaning null (no metric name), unlike an index of the empty string.</summary>
    public const uint NoString = 0xFFFFFFFF;

    /// <summary>Source file meaning "the plugin's own XML": always on, not in the files section, not hash-tracked.</summary>
    public const ushort PluginSourceFile = 0xFFFF;

    /// <summary>Record flag bit: the point is off unless the XML on disk turns it on.</summary>
    public const ushort FlagDefaultOff = 0x0001;

    /// <summary>
    /// Record flag bit: the point is an NR log-forwarding wrapper (it records a log line). The distro increments its
    /// same-thread log-dedupe depth (DistroLogCapture) around such a call so OTel's own log bridge, which emits
    /// synchronously inside that call, does not forward the same line a second time.
    /// </summary>
    public const ushort FlagLogForwarding = 0x0002;

    // ---- Assemblies -----------------------------------------------------------------------------------------------
    // Records are stable-sorted by target assembly name (ordinal), so each assembly owns one contiguous run of
    // records. This index lists the runs in record order. Why: later, lazy per-assembly registration can read only
    // the records of an assembly that actually loads, with no format change. The reader does not depend on the
    // sort; it only requires that the runs are contiguous, in order, and cover every record exactly once.

    public const int AssemblyEntrySize = 12;
    public const int AssemblyEntryString = 0; // u32 string index of the assembly name
    public const int AssemblyEntryFirstRecord = 4; // u32 index of the assembly's first record
    public const int AssemblyEntryRecordCount = 8; // u32 number of records in the run

    // ---- Files (version 3) ----------------------------------------------------------------------------------------
    // The manifest of shipped NR instrumentation XML files: every file the agent home ships that the distro knows
    // about, by its home name (the file name in <home>/extensions), with the hash of the content it was built from.
    // Why: at start-up a file on disk whose hash matches needs no parse (its points are exactly the records that name
    // it), and a changed file is parsed and compared point by point. A compiled file has records; a hash-only file
    // (FileFlagCompiled clear) is shipped but compiled into no record, and is tracked only so it is recognised as a
    // shipped file. Records name a file by its index here (RecordSourceFile).

    public const int FileEntrySize = 13;
    public const int FileEntryHomeName = 0; // u32 string index of the home name
    public const int FileEntryContentHash = 4; // u64, the generator's XmlContentHasher value
    public const int FileEntryFlags = 12; // u8

    /// <summary>File flag bit: the file's points are compiled into the records; clear = hash-only.</summary>
    public const byte FileFlagCompiled = 0x01;

    /// <summary>Most files a table can name: indices 0 to 0xFFFE, because 0xFFFF is <see cref="PluginSourceFile"/>.</summary>
    public const int MaxFileCount = PluginSourceFile;
}
