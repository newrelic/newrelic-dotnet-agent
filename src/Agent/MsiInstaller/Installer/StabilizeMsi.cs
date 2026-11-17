// Copyright 2020 New Relic Corporation. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

// Compiled by MSBuild as an inline task (see Installer.csproj), not as part of the project.
// Removes the build-time values that WiX and the Windows Installer API write into an MSI so that
// building the same inputs twice gives the same bytes:
//   - PackageCode (the summary information revision number), which WiX generates randomly
//   - the summary information create and last-saved times
//   - the compound file's directory timestamps
//   - the cabinet set ID and per-file timestamps of every embedded cabinet (the media cabinet and
//     the managed custom action package), which come from file modification times and a random ID
// None of these values are covered by a checksum, so the payload is untouched.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

public class StabilizeMsi : Task
{
    private const uint PidRevNumber = 9;
    private const uint PidCreateTime = 12;
    private const uint PidLastSaveTime = 13;
    private const uint VtEmpty = 0;
    private const uint VtLpstr = 30;
    private const uint VtFiletime = 64;

    private const ushort FixedDosDate = (20 << 9) | (1 << 5) | 1; // 2000-01-01
    private const ushort FixedDosTime = 0;
    private const long FixedFileTime = 125911584000000000L; // 2000-01-01T00:00:00Z

    private const uint EndOfChain = 0xFFFFFFFE;
    private const uint MaxRegularSector = 0xFFFFFFFA;

    [Required]
    public string MsiPath { get; set; }

    [Required]
    public string PackageCode { get; set; }

    public override bool Execute()
    {
        try
        {
            SetSummaryInformation();
            PatchFile();
            return true;
        }
        catch (Exception ex)
        {
            Log.LogErrorFromException(ex, true);
            return false;
        }
    }

    private void SetSummaryInformation()
    {
        IntPtr database;
        Check(MsiOpenDatabaseW(MsiPath, (IntPtr)2, out database), "MsiOpenDatabase");

        try
        {
            IntPtr summary;
            Check(MsiGetSummaryInformationW(database, null, 20, out summary), "MsiGetSummaryInformation");

            try
            {
                var empty = 0L;
                Check(MsiSummaryInfoSetPropertyW(summary, PidRevNumber, VtLpstr, 0, ref empty, "{" + PackageCode.Trim('{', '}').ToUpperInvariant() + "}"), "set revision number");

                foreach (var pid in new[] { PidCreateTime, PidLastSaveTime })
                {
                    uint type;
                    int intValue;
                    long fileTime = 0;
                    var length = 0u;
                    var buffer = new StringBuilder();
                    MsiSummaryInfoGetPropertyW(summary, pid, out type, out intValue, ref fileTime, buffer, ref length);

                    if (type != VtEmpty)
                    {
                        var fixedTime = FixedFileTime;
                        Check(MsiSummaryInfoSetPropertyW(summary, pid, VtFiletime, 0, ref fixedTime, null), "set summary time");
                    }
                }

                Check(MsiSummaryInfoPersist(summary), "MsiSummaryInfoPersist");
            }
            finally
            {
                MsiCloseHandle(summary);
            }
        }
        finally
        {
            MsiCloseHandle(database);
        }
    }

    private void PatchFile()
    {
        using (var stream = new FileStream(MsiPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var header = ReadAt(stream, 0, 512);
            var sectorSize = 1 << BitConverter.ToUInt16(header, 30);
            var fatSectorCount = BitConverter.ToInt32(header, 44);
            var directoryStart = BitConverter.ToUInt32(header, 48);
            var difatStart = BitConverter.ToUInt32(header, 68);
            var difatCount = BitConverter.ToInt32(header, 72);
            var perSector = sectorSize / 4;

            var difat = new List<uint>();
            for (var i = 0; i < 109; i++)
            {
                difat.Add(BitConverter.ToUInt32(header, 76 + i * 4));
            }

            var difatSector = difatStart;
            for (var i = 0; i < difatCount; i++)
            {
                var block = ReadAt(stream, SectorOffset(difatSector, sectorSize), sectorSize);
                for (var j = 0; j < perSector - 1; j++)
                {
                    difat.Add(BitConverter.ToUInt32(block, j * 4));
                }

                difatSector = BitConverter.ToUInt32(block, sectorSize - 4);
            }

            var fat = new List<uint>();
            for (var i = 0; i < fatSectorCount; i++)
            {
                var block = ReadAt(stream, SectorOffset(difat[i], sectorSize), sectorSize);
                for (var j = 0; j < perSector; j++)
                {
                    fat.Add(BitConverter.ToUInt32(block, j * 4));
                }
            }

            var cabinets = 0;
            foreach (var directorySector in Chain(fat, directoryStart))
            {
                var sectorOffset = SectorOffset(directorySector, sectorSize);
                var sector = ReadAt(stream, sectorOffset, sectorSize);

                for (var entry = 0; entry < sectorSize / 128; entry++)
                {
                    var o = entry * 128;
                    var type = sector[o + 66];
                    if (type == 0)
                    {
                        continue;
                    }

                    Array.Clear(sector, o + 100, 16);

                    var size = BitConverter.ToInt64(sector, o + 120);
                    if (type == 2 && size >= 4096)
                    {
                        cabinets += PatchCabinets(stream, fat, BitConverter.ToUInt32(sector, o + 116), size, sectorSize);
                    }
                }

                stream.Seek(sectorOffset, SeekOrigin.Begin);
                stream.Write(sector, 0, sector.Length);
            }

            Log.LogMessage(MessageImportance.High, "Stabilized {0} embedded cabinet(s) in {1}.", cabinets, MsiPath);
        }
    }

    private static int PatchCabinets(FileStream file, List<uint> fat, uint startSector, long size, int sectorSize)
    {
        var sectors = Chain(fat, startSector);
        var data = new byte[size];
        var copied = 0L;
        foreach (var sector in sectors)
        {
            var count = (int)Math.Min(sectorSize, size - copied);
            if (count <= 0)
            {
                break;
            }

            Buffer.BlockCopy(ReadAt(file, SectorOffset(sector, sectorSize), count), 0, data, (int)copied, count);
            copied += count;
        }

        var patched = 0;
        var from = 0;
        while (true)
        {
            var at = IndexOfSignature(data, from);
            if (at < 0)
            {
                break;
            }

            from = at + 4;

            if (at + 36 > data.Length)
            {
                continue;
            }

            var cabinetSize = BitConverter.ToUInt32(data, at + 8);
            if (cabinetSize < 36 || at + cabinetSize != data.Length)
            {
                continue;
            }

            var filesOffset = BitConverter.ToUInt32(data, at + 16);
            var fileCount = BitConverter.ToUInt16(data, at + 28);
            if (filesOffset >= cabinetSize)
            {
                continue;
            }

            data[at + 32] = 0;
            data[at + 33] = 0;

            var position = (long)at + filesOffset;
            for (var i = 0; i < fileCount; i++)
            {
                BitConverter.GetBytes(FixedDosDate).CopyTo(data, position + 10);
                BitConverter.GetBytes(FixedDosTime).CopyTo(data, position + 12);

                position += 16;
                while (data[position] != 0)
                {
                    position++;
                }

                position++;
            }

            patched++;
        }

        if (patched > 0)
        {
            var written = 0L;
            foreach (var sector in sectors)
            {
                var count = (int)Math.Min(sectorSize, size - written);
                if (count <= 0)
                {
                    break;
                }

                var block = new byte[count];
                Buffer.BlockCopy(data, (int)written, block, 0, count);
                file.Seek(SectorOffset(sector, sectorSize), SeekOrigin.Begin);
                file.Write(block, 0, count);
                written += count;
            }
        }

        return patched;
    }

    private static int IndexOfSignature(byte[] data, int from)
    {
        for (var i = from; i <= data.Length - 4; i++)
        {
            if (data[i] == (byte)'M' && data[i + 1] == (byte)'S' && data[i + 2] == (byte)'C' && data[i + 3] == (byte)'F')
            {
                return i;
            }
        }

        return -1;
    }

    private static List<uint> Chain(List<uint> fat, uint start)
    {
        var chain = new List<uint>();
        var sector = start;
        while (sector <= MaxRegularSector)
        {
            chain.Add(sector);
            sector = fat[(int)sector];

            if (chain.Count > fat.Count)
            {
                throw new InvalidDataException("Loop in the compound file sector chain.");
            }
        }

        if (sector != EndOfChain)
        {
            throw new InvalidDataException("Unexpected end of sector chain.");
        }

        return chain;
    }

    private static long SectorOffset(uint sector, int sectorSize)
    {
        return ((long)sector + 1) * sectorSize;
    }

    private static byte[] ReadAt(FileStream stream, long offset, int count)
    {
        var buffer = new byte[count];
        stream.Seek(offset, SeekOrigin.Begin);

        var read = 0;
        while (read < count)
        {
            var n = stream.Read(buffer, read, count - read);
            if (n <= 0)
            {
                throw new EndOfStreamException();
            }

            read += n;
        }

        return buffer;
    }

    private static void Check(uint result, string what)
    {
        if (result != 0)
        {
            throw new InvalidOperationException(what + " failed with Windows Installer error " + result);
        }
    }

    [DllImport("msi.dll", CharSet = CharSet.Unicode)]
    private static extern uint MsiOpenDatabaseW(string path, IntPtr persist, out IntPtr database);

    [DllImport("msi.dll", CharSet = CharSet.Unicode)]
    private static extern uint MsiGetSummaryInformationW(IntPtr database, string path, uint updateCount, out IntPtr summary);

    [DllImport("msi.dll", CharSet = CharSet.Unicode)]
    private static extern uint MsiSummaryInfoSetPropertyW(IntPtr summary, uint property, uint dataType, int intValue, ref long fileTime, string stringValue);

    [DllImport("msi.dll", CharSet = CharSet.Unicode)]
    private static extern uint MsiSummaryInfoGetPropertyW(IntPtr summary, uint property, out uint dataType, out int intValue, ref long fileTime, StringBuilder stringValue, ref uint stringLength);

    [DllImport("msi.dll")]
    private static extern uint MsiSummaryInfoPersist(IntPtr summary);

    [DllImport("msi.dll")]
    private static extern uint MsiCloseHandle(IntPtr handle);
}
