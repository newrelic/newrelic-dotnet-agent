// Copyright 2020 New Relic Corporation. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

// Compiled by MSBuild as an inline task (see Installer.csproj), not as part of the project.
// Removes the build-time values that WiX and the Windows Installer API write into an MSI so that
// building the same inputs twice gives the same bytes:
//   - PackageCode (the summary information revision number), which WiX generates randomly
//   - the summary information create and last-saved times
//   - the compound file's root entry timestamps
//   - the cabinet set ID and per-file timestamps of every embedded cabinet (the media cabinet and
//     the managed custom action package), which come from file modification times and a random ID
// None of these values are covered by a checksum, so the payload is untouched.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using WixToolset.Dtf.WindowsInstaller;

public class StabilizeMsi : Task
{
    // The media cabinet and the cabinet inside the managed custom action package. A different count means a WiX update
    // moved or changed the cabinets, and the MSI would silently stop being deterministic.
    private const int ExpectedCabinets = 2;

    private const ushort FixedDosDate = (20 << 9) | (1 << 5) | 1; // 2000-01-01
    private const ushort FixedDosTime = 0;

    private static readonly DateTime FixedTime = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Required]
    public string MsiPath { get; set; }

    [Required]
    public string PackageCode { get; set; }

    public override bool Execute()
    {
        try
        {
            var cabinets = StabilizeDatabase();
            if (cabinets != ExpectedCabinets)
            {
                Log.LogError("Found {0} embedded cabinet(s) in {1} but expected {2}. A WiX update probably moved or changed the cabinets, so the MSI would no longer be deterministic. The MSI was left unmodified.", cabinets, MsiPath, ExpectedCabinets);
                return false;
            }

            ZeroRootEntryTimes();
            Log.LogMessage(MessageImportance.High, "Stabilized {0} embedded cabinet(s) in {1}.", cabinets, MsiPath);
            return true;
        }
        catch (Exception ex)
        {
            Log.LogErrorFromException(ex, true);
            return false;
        }
    }

    // Returns the number of cabinets found. Nothing is committed unless that is the expected number.
    private int StabilizeDatabase()
    {
        using (var database = new Database(MsiPath, DatabaseOpenMode.Transact))
        {
            var patched = new Dictionary<string, byte[]>();
            var cabinets = 0;

            using (var view = database.OpenView("SELECT `Name`, `Data` FROM `_Streams`"))
            {
                view.Execute();
                foreach (var record in view)
                {
                    using (record)
                    {
                        byte[] data;
                        using (var stream = record.GetStream(2))
                        using (var buffer = new MemoryStream())
                        {
                            stream.CopyTo(buffer);
                            data = buffer.ToArray();
                        }

                        var count = PatchCabinets(data);
                        if (count > 0)
                        {
                            cabinets += count;
                            patched.Add(record.GetString(1), data);
                        }
                    }
                }
            }

            if (cabinets != ExpectedCabinets)
            {
                return cabinets;
            }

            using (var view = database.OpenView("SELECT `Name`, `Data` FROM `_Streams`"))
            {
                view.Execute();
                foreach (var pair in patched)
                {
                    using (var record = new Record(2))
                    {
                        record.SetString(1, pair.Key);
                        record.SetStream(2, new MemoryStream(pair.Value));
                        view.Assign(record);
                    }
                }
            }

            var summary = database.SummaryInfo;
            summary.RevisionNumber = "{" + PackageCode.Trim('{', '}').ToUpperInvariant() + "}";
            summary.CreateTime = FixedTime;
            summary.LastSaveTime = FixedTime;

            database.Commit();
            return cabinets;
        }
    }

    // Clears the cabinet set ID and sets every file timestamp in each cabinet found in the data. Returns how many.
    private static int PatchCabinets(byte[] data)
    {
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
                if (position + 17 > data.Length)
                {
                    throw new InvalidDataException("A cabinet file entry runs past the end of the cabinet.");
                }

                BitConverter.GetBytes(FixedDosDate).CopyTo(data, position + 10);
                BitConverter.GetBytes(FixedDosTime).CopyTo(data, position + 12);

                position += 16;
                while (position < data.Length && data[position] != 0)
                {
                    position++;
                }

                position++;
            }

            patched++;
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

    // The summary information and stream changes are written by the Windows Installer API, which leaves the root entry's
    // creation and modification times (16 bytes) in the compound file directory. The first directory sector is at the
    // header's directory start sector, and the root entry is its first entry, so no sector chain needs to be followed.
    private void ZeroRootEntryTimes()
    {
        using (var stream = new FileStream(MsiPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var header = new byte[512];
            ReadFully(stream, header);

            if (BitConverter.ToUInt64(header, 0) != 0xE11AB1A1E011CFD0)
            {
                throw new InvalidDataException("Not a compound file.");
            }

            var sectorSize = 1 << BitConverter.ToUInt16(header, 30);
            var directoryStart = BitConverter.ToUInt32(header, 48);
            var rootEntry = ((long)directoryStart + 1) * sectorSize;

            var entry = new byte[128];
            stream.Seek(rootEntry, SeekOrigin.Begin);
            ReadFully(stream, entry);

            const int RootStorageType = 5;
            if (entry[66] != RootStorageType)
            {
                throw new InvalidDataException("The first directory entry is not the root entry.");
            }

            stream.Seek(rootEntry + 100, SeekOrigin.Begin);
            stream.Write(new byte[16], 0, 16);
        }
    }

    private static void ReadFully(Stream stream, byte[] buffer)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var n = stream.Read(buffer, read, buffer.Length - read);
            if (n <= 0)
            {
                throw new EndOfStreamException();
            }

            read += n;
        }
    }
}
