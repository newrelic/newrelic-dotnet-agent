// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.Linq;
using System.Management;

namespace FunctionalTests.Helpers
{
    public static class FileOperations
    {
        /// <summary>
        /// Deletes a file or directory at the specified location.
        /// </summary>
        /// <param name="path">The path to the file or directory.</param>
        public static void DeleteFileOrDirectory(ManagementScope mgmtScope, String path, bool directory = false)
        {
            var wmiClass = directory
                ? "Win32_Directory"
                : "CIM_DataFile";
            WMI.WMIQuery_InvokeMethod(mgmtScope, String.Format("SELECT * FROM {0} WHERE Name = '{1}'", wmiClass, path), "Delete");
        }

        /// <summary>
        /// Creates a file or folder at the specified location.
        /// </summary>
        /// <param name="path">The path to the file or directory.</param>
        /// <param name="directory">Boolean indicating file or directory.</param>
        public static void CreateFileOrDirectory(ManagementScope mgmtScope, String path, bool directory = false)
        {
            var dosCommand = directory
                ? "mkdir"
                : "ECHO Create >";
            var wmiCommand = String.Format("cmd.exe /c {0} \"{1}\"", dosCommand, path);
            WMI.MakeWMICall(mgmtScope, "Win32_Process", wmiCommand);
        }

        /// <summary>
        /// Verifies that the specified file or directory exists.
        /// </summary>
        /// <param name="path">The path to the file or directory relative to the local drive.</param>
        /// <param name="directory">Boolean indicating whether or not the path is a directory.</param>
        /// <returns>Boolean representing the status of the file or directory.</returns>
        public static bool FileOrDirectoryExists(ManagementScope mgmtScope, String path, bool directory = false)
        {
            if (Settings.IsDeveloperMode)
            {
                path = path.Replace("\\", "\\\\");
            }
            var wmiClass = directory
                ? "Win32_Directory"
                : "CIM_DataFile";
            var dirQuery = new ObjectQuery(String.Format("SELECT * FROM {0} WHERE Name = '{1}'", wmiClass, path));

            using (var search = new ManagementObjectSearcher(mgmtScope, dirQuery))
            {
                return search.Get().Cast<ManagementObject>().Any();
            }
        }

        /// <summary>
        /// Copies a file.
        /// </summary>
        /// <param name="path">The path to the file to be copied.</param>
        /// <param name="copyTo">The destination to copy the file to.</param>
        public static void CopyFile(String path, String copyTo)
        {
            if (Settings.Environment == Enumerations.EnvironmentSetting.Local)
            {
                File.Copy(path, copyTo, true);
            }
            else
            {
                Common.Impersonate(() => File.Copy(path, copyTo, true));
            }
        }

        /// <summary>
        /// Moves a file (also used for renames).
        /// </summary>
        /// <param name="path">The path to the file to be moved.</param>
        /// <param name="moveTo">The destination to move the file to.</param>
        public static void MoveFile(String path, String moveTo)
        {
            if (Settings.Environment == Enumerations.EnvironmentSetting.Local)
            {
                File.Move(path, moveTo);
            }
            else
            {
                Common.Impersonate(() => File.Move(path, moveTo));
            }
        }

        /// <summary>
        /// Parses a text file at the specified location.
        /// </summary>
        /// <param name="path">The path of the file to parse.</param>
        /// <returns>A string representing the text in the file.</returns>
        public static String ParseTextFile(String path)
        {
            return Settings.Environment == Enumerations.EnvironmentSetting.Remote
                ? Common.Impersonate(() => DoParseTextFile(path)).ToString()
                : DoParseTextFile(path);
        }

        public static String DoParseTextFile(String path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (var reader = new StreamReader(fs))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        /// <summary>
        /// Parses a text file at the specified location into a string array.
        /// </summary>
        /// <param name="path">The path of the file to parse.</param>
        /// <returns>A string array representing the text in the file.</returns>
        public static string[] ParseTextFileToArray(string path)
        {
            return Settings.Environment == Enumerations.EnvironmentSetting.Remote
                ? Common.Impersonate(() => DoParseTextFileToArray(path))
                : DoParseTextFileToArray(path);
        }

        public static string[] DoParseTextFileToArray(string path)
        {
            return System.IO.File.ReadAllLines(path);
        }
    }
}
