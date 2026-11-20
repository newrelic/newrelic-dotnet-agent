// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

// Linked into Core (see Core.csproj), which defines NR_OTELDISTRO_CORE; the generator build compiles the other branch.
#if NR_OTELDISTRO_CORE
namespace NewRelic.Agent.Core.OtelDistro.Xml;
#else
namespace NewRelic.OtelDistro.Generators;
#endif

/// <summary>
/// Content hash of an instrumentation XML file, used to tell "the shipped file, unchanged" from "the file was edited".
/// FNV-1a 64 over the UTF-8 bytes of the text after a leading BOM is stripped and CRLF is normalised to LF, so an editor
/// that re-saves a file with a BOM or Windows line endings and no real change still hashes the same. The build (which
/// only sees decoded text) hashes the text; the run time hashes the file's bytes (<see cref="Compute(byte[])"/>), which
/// gives the same value for a UTF-8 file without decoding it.
/// </summary>
public static class XmlContentHasher
{
    private const char ByteOrderMark = '\uFEFF';

    public static ulong Compute(string text)
    {
        var normalized = text ?? string.Empty;
        if (normalized.Length > 0 && normalized[0] == ByteOrderMark)
        {
            normalized = normalized.Substring(1);
        }

        return Fnv1a64.AddUtf8(Fnv1a64.OffsetBasis, normalized.Replace("\r\n", "\n"));
    }

    /// <summary>
    /// The hash of a file's raw bytes, with no copy: a leading UTF-8 BOM (EF BB BF) is skipped and a CR (0x0D) directly
    /// before an LF (0x0A) is skipped. For UTF-8 content this equals <see cref="Compute(string)"/> of the decoded text;
    /// any other encoding hashes differently, so the file is parsed (correct, only slower). Null hashes as empty.
    /// </summary>
    public static ulong Compute(byte[] utf8)
    {
        var hash = Fnv1a64.OffsetBasis;
        if (utf8 == null)
        {
            return hash;
        }

        var start = utf8.Length >= 3 && utf8[0] == 0xEF && utf8[1] == 0xBB && utf8[2] == 0xBF ? 3 : 0;
        for (var i = start; i < utf8.Length; i++)
        {
            var b = utf8[i];
            if (b == 0x0D && i + 1 < utf8.Length && utf8[i + 1] == 0x0A)
            {
                continue;
            }

            hash = Fnv1a64.Add(hash, b);
        }

        return hash;
    }
}
