// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using Microsoft.Win32;

namespace FunctionalTests.Helpers
{
    public static class ComponentManager
    {
        private const string RegistryPathRoot = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData";
        private const string Components = "Components";
        private const string InstallLog = @"C:\\installLog.txt";
        private const string UninstallLog = @"C:\\uninstallLog.txt";
        private const string RepairLog = @"C:\\repairLog.txt";
        private const string InstallLogDest = @"C:\\moved_installLog.txt";
        private const string UninstallLogDest = @"C:\\moved_uninstallLog.txt";
        private const string RepairLogDest = @"C:\\moved_repairLog.txt";

        public static void CleanAndTruncateComponents(TestServer tserver)
        {
            CleanComponents(tserver);
            TruncateComponents(tserver);
        }

        public static void CleanComponents(TestServer tserver, string testName = "")
        {
            Console.WriteLine("ComponentManager - Starting CleanComponents Pass.");
            var disallowedGuids = GetDisallowedGuids(tserver.MgmtScope, UninstallLog);
            //disallowedGuids.AddRange(GetDisallowedGuids(tserver.MgmtScope, InstallLog));
            //disallowedGuids.AddRange(GetDisallowedGuids(tserver.MgmtScope, RepairLog));

            if (!disallowedGuids.Any())
            {
                Console.WriteLine("-- No disallowed GUIDs found.");
                Console.WriteLine("Finshed CleanComponents Pass.");
                return;
            }

            Console.WriteLine("-- Found disallowed GUIDs. Starting cleanup.");
            DeleteComponents(tserver, disallowedGuids);

            tserver.RunCleanUninstall(false, testName: testName);

            //CleanupInstallLog(tserver.MgmtScope);
            CleanupUninstallLog(tserver.MgmtScope);
            //CleanupRepairLog(tserver.MgmtScope);
            Console.WriteLine("Finshed CleanComponents Pass.");
        }

        public static void TruncateComponents(TestServer tserver)
        {
            Console.WriteLine("ComponentManager - Starting TruncateComponents Pass.");
            var code = GetProductCode(tserver.MgmtScope, InstallLog).PackGuid();
            if (String.IsNullOrEmpty(code))
            {
                Console.WriteLine("-- Product code is invalid");
                Console.WriteLine($"-- Product code (raw): '{GetProductCode(tserver.MgmtScope, InstallLog)}'.");
                Console.WriteLine("Finished TruncateComponents Pass.");
                return;
            }
            Console.WriteLine($"-- Product code (packed): {code}.");
            var packedComponents = GetComponentGuids(tserver.MgmtScope, InstallLog);
            var sKey = GetSKey(tserver);

            // read the values in each componenet, delete none matching, report deletion
            foreach (var component in packedComponents)
            {
                var path = $@"{RegistryPathRoot}\{sKey}\{Components}\{component}";
                if (!tserver.RegistryKeyExists(RegistryHive.LocalMachine, path))
                {
                    continue;
                }

                DoTruncateComponents(tserver, path, code);
            }

            CleanupInstallLog(tserver.MgmtScope);
            Console.WriteLine("Finished TruncateComponents Pass.");
        }

        private static void DoTruncateComponents(TestServer tserver, string path, string code)
        {
            var pairs = tserver.GetRegistryKeyValuePairs(RegistryHive.LocalMachine, path);
            foreach (var pair in pairs)
            {
                var name = pair.Split('=')[0];
                if (name == code)
                {
                    continue;
                }
                Console.WriteLine($"-- Checking {path}:{pair}.");
                Console.WriteLine("-- -- Found orphaned product code, deleting.");
                tserver.DeleteRegistryValue(RegistryHive.LocalMachine, path, name);
            }

            // verify
            var finalPairs = tserver.GetRegistryKeyValuePairs(RegistryHive.LocalMachine, path);
            if (pairs.Count != finalPairs.Count)
            {
                Console.WriteLine($"-- -- Final component count {finalPairs.Count}, expected 1.");
            }
        }

        private static void DeleteComponents(TestServer tserver, List<string> disallowedGuids)
        {
            var sKey = GetSKey(tserver);

            foreach (var guid in disallowedGuids)
            {
                var packedGuid = guid.PackGuid();

                var keyExists = tserver.RegistryKeyExists(RegistryHive.LocalMachine, $@"{RegistryPathRoot}\{sKey}\{Components}\{packedGuid}");
                if (!keyExists)
                {
                    continue;
                }

                if (packedGuid == "C48353F14549CF85EB9753AA31FD070E" || packedGuid == "76298F0E0294DF15981E789A51EF4918")
                {
                    Console.WriteLine($@"-- -- Leaving(IIS): ROOT\{sKey}\{Components}\{packedGuid}.");
                    continue;
                }

                Console.WriteLine($@"-- -- Deleting orphaned component: ROOT\{sKey}\{Components}\{packedGuid}.");
                LogValuePairs(tserver, $@"{RegistryPathRoot}\{sKey}\{Components}\{packedGuid}");
                tserver.DeleteRegistryKey(RegistryHive.LocalMachine, $@"{RegistryPathRoot}\{sKey}\{Components}", packedGuid);
                keyExists = tserver.RegistryKeyExists(RegistryHive.LocalMachine, $@"{RegistryPathRoot}\{sKey}\{Components}\{packedGuid}");
                Console.WriteLine($"-- -- Orphan Still exists? {keyExists}, expected: false");
            }
        }


        private static List<string> GetComponentGuids(ManagementScope scope, string path)
        {
            var fileData = ReadFile(scope, path);
            var guids = new List<string>();
            for (var i = 0; i < fileData.Count; i++)
            {
                if (fileData[i].Contains("Executing op: ComponentRegister(ComponentId"))
                {
                    guids.Add(fileData[i].ExtractGuid().PackGuid());
                }
            }

            return guids;
        }

        private static string GetProductCode(ManagementScope scope, string path)
        {
            var fileData = ReadFile(scope, path);
            for (var i = 0; i < fileData.Count; i++)
            {
                if (fileData[i].Contains("Product Code from property table before transforms"))
                {
                    return fileData[i].ExtractGuid();
                }
            }

            return String.Empty;
        }

        private static void LogValuePairs(TestServer tserver, string path)
        {
            if (!tserver.RegistryKeyExists(RegistryHive.LocalMachine, path))
            {
                Console.WriteLine("-- -- Component not found.");
                return;
            }

            var pairs = tserver.GetRegistryKeyValuePairs(RegistryHive.LocalMachine, path);

            if (!pairs.Any())
            {
                Console.WriteLine("-- No pairs found.");
                return;
            }

            foreach (var pair in pairs)
            {
                Console.WriteLine($"-- -- pair: {pair}.");
            }
        }

        private static string GetSKey(TestServer tserver)
        {
            var sKeys = tserver.GetRegistryKeySubKeys(RegistryHive.LocalMachine, RegistryPathRoot);
            if (sKeys.Length == 1)
            {
                return sKeys[0];
            }

            var shortKey = sKeys[0];
            foreach (var sKey in sKeys)
            {
                if (sKey.Length < shortKey.Length)
                {
                    shortKey = sKey;
                }
            }

            return shortKey;
        }

        private static string PackGuid(this string guid)
        {
            if (guid.Length < 36 || guid.Length > 38)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            var noBracesGuid = guid.TrimStart('{').TrimEnd('}');
            var guidSegments = noBracesGuid.Split('-');

            //segments 0-2: writes each group of the first three groups of hexadecimals characters in a standard GUID in reverse order
            for (var i = 0; i < 3; i++)
            {
                builder.Append(guidSegments[i].Reverse());
            }

            //segments 3-4: switches every two characters in the fourth and fifth group in a standard GUID
            builder.Append(guidSegments[3].SwapTwo());
            builder.Append(guidSegments[4].SwapTwo());

            return builder.ToString();
        }

        private static string Reverse(this string input)
        {
            return new string(input.ToCharArray().Reverse().ToArray());
        }

        private static string SwapTwo(this string input)
        {
            var builder = new StringBuilder();
            for (var i = 0; i < input.Length; i += 2)
            {
                builder.Append(input[i + 1]);
                builder.Append(input[i]);
            }
            return builder.ToString();
        }

        private static List<string> GetDisallowedGuids(ManagementScope scope, string path)
        {
            var fileData = ReadFile(scope, path);
            var guids = new List<string>();
            for (var i = 0; i < fileData.Count; i++)
            {
                if (fileData[i].Contains("Disallowing uninstallation of component"))
                {
                    guids.Add(fileData[i].ExtractGuid());
                }
            }

            return guids;
        }

        private static List<string> ReadFile(ManagementScope scope, string path)
        {
            if (!FileOperations.FileOrDirectoryExists(scope, path))
            {
                Console.WriteLine($"-- -- {path} not found.");
                return new List<string>();
            }

            var fileData = FileOperations.ParseTextFileToArray(path);
            return fileData.ToList();
        }

        private static string ExtractGuid(this string input)
        {
            return input.Substring(input.IndexOf("{") + 1, 36);
        }

        private static void CleanupInstallLog(ManagementScope scope)
        {
            if (FileOperations.FileOrDirectoryExists(scope, InstallLog))
            {
                TryDeleteFile(scope, InstallLogDest);
                FileOperations.MoveFile(InstallLog, InstallLogDest);
            }
        }

        private static void CleanupUninstallLog(ManagementScope scope)
        {
            if (FileOperations.FileOrDirectoryExists(scope, UninstallLog))
            {
                TryDeleteFile(scope, UninstallLogDest);
                FileOperations.MoveFile(UninstallLog, UninstallLogDest);
            }
        }

        private static void CleanupRepairLog(ManagementScope scope)
        {
            if (FileOperations.FileOrDirectoryExists(scope, RepairLog))
            {
                TryDeleteFile(scope, RepairLogDest);
                FileOperations.MoveFile(RepairLog, RepairLogDest);
            }
        }

        private static void TryDeleteFile(ManagementScope scope, string path)
        {
            if (FileOperations.FileOrDirectoryExists(scope, path))
            {
                FileOperations.DeleteFileOrDirectory(scope, path);
            }
        }
    }
}
