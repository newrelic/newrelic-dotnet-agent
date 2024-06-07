// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.XPath;
using Microsoft.Win32;
using WixToolset.Dtf.WindowsInstaller;

namespace InstallerActions
{
    public class CustomActions
    {
        [CustomAction]
        public static ActionResult FindPreviousLicenseKey(Session session)
        {
            try
            {
                Log(session, "Looking for previous license key in a number of locations.");
                return new LicenseKeyFinder(session).FindLicenseKey();
            }
            catch (Exception exception)
            {
                Log(session, "Failed to locate a previous licenseKey.\n{0}", exception);
                return ActionResult.Success;
            }
        }

        [CustomAction]
        public static ActionResult SaveDeferredCustomActionData(Session session)
        {
            var customActionData = new CustomActionData
            {
                { "NR_LICENSE_KEY", session["NR_LICENSE_KEY"] },
                { "NETAGENTCOMMONFOLDER", session["NETAGENTCOMMONFOLDER"] },
                { "LOGSFOLDER", session["LOGSFOLDER"] },
                { "NETAGENTFOLDER", session["NETAGENTFOLDER"] },
                { "AppDataExtensionsFolder", session["FrameworkExtensionsFolder"] }
            };

            session.DoAction("MigrateConfiguration", customActionData);
            session.DoAction("SetLicenseKey", customActionData);
            session.DoAction("CleanupPreviousInstall", customActionData);

            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult MigrateConfiguration(Session session)
        {
            try
            {
                return new ConfigurationMigrator(session).MigrateConfiguration();
            }
            catch (Exception exception)
            {
                session.Log("Unable to migrate configuration.\n{0}", exception);
                return ActionResult.Success;
            }
        }

        [CustomAction]
        public static ActionResult SetLicenseKey(Session session)
        {
            try
            {
                var path = session.CustomActionData["NETAGENTCOMMONFOLDER"] + @"\newrelic.config";
                var licenseKey = session.CustomActionData["NR_LICENSE_KEY"];

                var document = new XmlDocument();
                document.Load(path);
                var namespaceManager = new XmlNamespaceManager(document.NameTable);
                namespaceManager.AddNamespace("newrelic-config", "urn:newrelic-config");

                var serviceNode = document.SelectSingleNode("/newrelic-config:configuration/newrelic-config:service", namespaceManager);
                if (serviceNode == null)
                {
                    session.Log("Unable to locate /configuration/service node in newrelic.config.  License key not set.");
                    return ActionResult.Success;
                }
                session.Log("/configuration/service node found in newrelic.config");

                if (serviceNode.Attributes == null)
                {
                    session.Log("No attributes found in /configuration/service node in newrelic.config. License key not set.");
                    return ActionResult.Success;
                }

                var licenseKeyAttribute = serviceNode.Attributes["licenseKey"];
                if (licenseKeyAttribute == null)
                {
                    session.Log("licenseKey attribute not found on /configuration/service node.  License key not set.");
                    return ActionResult.Success;
                }
                session.Log("licenseKey attribute found in /configuration/service node.");

                licenseKeyAttribute.Value = licenseKey;
                session.Log("License key set to " + licenseKey);

                document.Save(session.CustomActionData["NETAGENTCOMMONFOLDER"] + @"\newrelic.config");
                session.Log("newrelic.config saved with updated license key.");
            }
            catch (Exception exception)
            {
                session.Log("Exception while attempting to set the license key in newrelic.config.\n{0}", exception);
            }

            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult CleanupPreviousInstall(Session session)
        {
            var cleanup = new Cleanup(session);
            if (cleanup.ValidateFilesAndFolders() == ActionResult.Failure) return ActionResult.Failure;
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult RestartIis(Session session)
        {
            try
            {
                var processStartInfo = new ProcessStartInfo("iisreset")
                {
                    Verb = "runas"
                };
                var process = new Process
                {
                    EnableRaisingEvents = true,
                    StartInfo = processStartInfo
                };
                process.Start();
                process.WaitForExit();
            }
            catch (Exception exception)
            {
                session.Log("Exception thrown while restarting IIS.\n{0}", exception);
            }

            return ActionResult.Success;
        }

        [CustomAction]
        // Leaving this action even though NewRelicStatusMonitor has been removed in case Agent being replaced still has it
        public static ActionResult CloseStatusMonitor(Session session)
        {
            try
            {
                var procList = Process.GetProcessesByName("NewRelicStatusMonitor");

                if (procList.Length > 0)
                {
                    session.Log("NewRelicStatusMonitor is running. Installer will attempt to terminate the process.");
                    procList[0].Kill();

                    var retryWait = 0;
                    //Retry for up to 5 seconds or until it has exited to move on.
                    while (!procList[0].HasExited || retryWait <= 5)
                    {
                        procList[0].WaitForExit(1000);
                        retryWait++;
                    }

                    //Force rollback if we cannot shut down.
                    if (!procList[0].HasExited)
                    {
                        throw new Exception("The New Relic Status Monitor was not able to be shutdown automatically.  Please shut it down manually and try the install again.");
                    }

                }
                else
                {
                    session.Log("NewRelicStatusMonitor is NOT running.");
                }

            }
            catch (Exception exception)
            {
                session.Log("Exception thrown while attempting to kill the NewRelicStatusMonitor.\n{0}", exception);
            }

            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult CheckBitness(Session session)
        {
            try
            {
                var uninstallPaths = new string[] {
                    "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall",
                    "SOFTWARE\\Wow6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall" };

                // check 64-bit first and then 32-bit if needed.
                string displayName = null;
                foreach (var path in uninstallPaths)
                {
                    if (string.IsNullOrEmpty(displayName))
                    {
                        displayName = GetDisplayName(path);
                    }
                }

                // didn't find NR so not installed.
                if (displayName == null)
                {
                    return ActionResult.Success;
                }

                var productName = session["ProductName"];
                if (displayName == productName)
                {
                    return ActionResult.Success;
                }

                session.Message(InstallMessage.FatalExit, BuildBitnessErrorRecord(productName));
                return ActionResult.Failure;
            }
            catch(Exception exception)
            {
                session.Log("Exception thrown checking bitness:\n{0}", exception);
                return ActionResult.Failure;
            }
        }

        private static string GetDisplayName(string path)
        {
            try
            {
                var regKey = Registry.LocalMachine.OpenSubKey(path);
                foreach (var subkeyName in regKey.GetSubKeyNames())
                {
                    // Our key a a guid so skip anything that is not.
                    if (!subkeyName.StartsWith("{"))
                    {
                        continue;
                    }

                    // Some entries are missing the DisplayName value.
                    var subkey = regKey.OpenSubKey(subkeyName);
                    if (!HasDisplayName(subkey.GetValueNames()))
                    {
                        continue;
                    }

                    var displayName = subkey.GetValue("DisplayName").ToString();
                    if (string.IsNullOrEmpty(displayName) || !displayName.StartsWith("New Relic .NET Agent"))
                    {
                        continue;
                    }

                    // we have a new relic displayname here.
                    return displayName;
                }
            }
            catch
            {}

            return null;
        } 

        private static Record BuildBitnessErrorRecord(string productName)
        {
            var builder = new StringBuilder();
            if (productName == "New Relic .NET Agent (64-bit)")
            {
                builder.AppendLine("The installed x86 (32-bit) version of the New Relic .NET Agent is not compatible with this 64-bit installer.");
                builder.AppendLine();
                builder.AppendLine("Either remove the existing installation or use the x86 (32-bit) installer.");
            }
            else
            {
                builder.AppendLine("The installed 64-bit version of the New Relic .NET Agent is not compatible with this x86 (32-bit) installer.");
                builder.AppendLine();
                builder.AppendLine("Either remove the existing installation or use the 64-bit installer.");
            }

            return new Record { FormatString = builder.ToString() };
        }

        private static bool HasDisplayName(string[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == "DisplayName")
                {
                    return true;
                }
            }

            return false;
        }

        public static void Log(Session session, string message, params object[] arguments)
        {
            session["LogHack"] = String.Format(message, arguments);
        }
    }

    internal abstract class MySession
    {
        protected Session session { get; private set; }

        protected MySession(Session session)
        {
            this.session = session;
        }

        public virtual void Log(string message, params object[] arguments)
        {
            session.Log(message, arguments);
        }

        public virtual void LogError(string message, params object[] arguments)
        {
            Log("New Relic Error: " + message, arguments);
        }

        public virtual void LogSuccess(string message, params object[] arguments)
        {
            Log("New Relic Success: " + message, arguments);
        }
    }

    internal class Cleanup : MySession
    {
        public Cleanup(Session session) : base(session)
        { }

        public ActionResult ValidateFilesAndFolders()
        {
            DeleteFolder(@"C:\Program Files\New Relic .NET Agent\");
            DeleteFolder(@"C:\Program Files (x86)\New Relic .NET Agent\");
            DeleteFile(@"C:\Windows\System32\NewRelic.IL.dll");
            DeleteFile(@"C:\Windows\SysWOW64\NewRelic.IL.dll");
            DeleteFile(session.CustomActionData["NETAGENTCOMMONFOLDER"] + @"newrelic.xml");

            // Delete old logging XMLs to prevent duplicate logs from being sent up.
            DeleteFile(@"C:\ProgramData\New Relic\.NET Agent\Extensions\netcore\NewRelic.Providers.Wrapper.Logging.Instrumentation.xml");
            DeleteFile(@"C:\ProgramData\New Relic\.NET Agent\Extensions\netframework\NewRelic.Providers.Wrapper.Logging.Instrumentation.xml");

            DeleteFile(@"C:\ProgramData\New Relic\.NET Agent\Extensions\netframework\NewRelic.Providers.Wrapper.CastleMonoRail2.Instrumentation.xml");

            // Asp35 was renamed to AspNet
            DeleteFile(@"C:\ProgramData\New Relic\.NET Agent\Extensions\netframework\NewRelic.Providers.Wrapper.Asp35.Instrumentation.xml");


            // Delete old logging DLLs (from default locations) to prevent duplicate logs from being sent up.
            DeleteFile(@"C:\Program Files\New Relic\.NET Agent\netcore\Extensions\NewRelic.Providers.Wrapper.Logging.dll");
            DeleteFile(@"C:\Program Files\New Relic\.NET Agent\netframework\Extensions\NewRelic.Providers.Wrapper.Logging.dll");
            DeleteFile(@"C:\Program Files (x86)\New Relic\.NET Agent\netcore\Extensions\NewRelic.Providers.Wrapper.Logging.dll");
            DeleteFile(@"C:\Program Files (x86)\New Relic\.NET Agent\netframework\Extensions\NewRelic.Providers.Wrapper.Logging.dll");

            DeleteFile(@"C:\Program Files (x86)\New Relic\.NET Agent\netframework\Extensions\NewRelic.Providers.Wrapper.CastleMonoRail2.dll");
            DeleteFile(@"C:\Program Files\New Relic\.NET Agent\netframework\Extensions\NewRelic.Providers.Wrapper.CastleMonoRail2.dll");

            // Asp35 was renamed to AspNet
            DeleteFile(@"C:\Program Files (x86)\New Relic\.NET Agent\netframework\Extensions\NewRelic.Providers.Wrapper.Asp35.dll");
            DeleteFile(@"C:\Program Files\New Relic\.NET Agent\netframework\Extensions\NewRelic.Providers.Wrapper.Asp35.dll");


            String newrelicHomePath = Environment.GetEnvironmentVariable("NEWRELIC_HOME");
            if (newrelicHomePath != null) DeleteFile(newrelicHomePath + @"\newrelic.xml");

            return ActionResult.Success;
        }

        private void DeleteFolder(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    SaferFileUtils.DeleteDirectoryAndContents(path, this);
                    LogSuccess("Folder deleted at '{0}'.", path);
                }
                else
                {
                    LogSuccess("Folder does not exist at '{0}', as expected.", path);
                }
            }
            catch (Exception exception)
            {
                LogError("Exception thrown while attempting to delete folder at '{0}'", path);
                LogError(exception.ToString());
            }
        }

        private void DeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    SaferFileUtils.FileDelete(path, this);
                    LogSuccess("File deleted at '{0}'.", path);
                }
                else
                {
                    LogSuccess("File does not exist at '{0}', as expected.", path);
                }
            }
            catch (Exception exception)
            {
                LogError("Exception thrown while attempting to delete file at '{0}'", path);
                LogError(exception.ToString());
            }
        }

    }

    internal class LicenseKeyFinder : MySession
    {
        public LicenseKeyFinder(Session session) : base(session)
        { }

        public ActionResult FindLicenseKey()
        {
            // If the user supplied a license key use that.
            if (session["NR_LICENSE_KEY"] != null && session["NR_LICENSE_KEY"].Length != 0)
            {
                Log("License key supplied to installer by user (or already found), skipping check for previous license key.");
                return ActionResult.Success;
            }

            // Check a number of locations for a license key in newrelic.config.
            String[] folderPaths =
            {
                session["NETAGENTCOMMONFOLDER"],
                Environment.GetEnvironmentVariable("NEWRELIC_HOME"),
                @"C:\Program Files\New Relic .NET Agent",
                @"C:\Program Files (x86)\New Relic .NET Agent"
            };
            foreach (String folderPath in folderPaths)
            {
                String licenseKey = GetKeyFromFolder(folderPath);
                if (licenseKey == null) continue;

                Log("Setting NR_LICENSE_KEY to {0} found in {1}", licenseKey, folderPath);
                session["NR_LICENSE_KEY"] = licenseKey;
                session["PREVLICENSEKEYFOUND"] = "1";

                return ActionResult.Success;
            }

            Log("No previous license key found.");
            return ActionResult.Success;
        }

        private String GetKeyFromFile(String path)
        {
            Log("newrelic.config found at {0}, looking for license key inside.", path);

            try
            {
                var document = new XPathDocument(path);
                var navigator = document.CreateNavigator();
                if (navigator.NameTable == null)
                {
                    Log("Unable to load name table in newrelic.config.");
                    return null;
                }

                var namespaceManager = new XmlNamespaceManager(navigator.NameTable);
                namespaceManager.AddNamespace("newrelic-config", "urn:newrelic-config");

                var serviceNode = navigator.SelectSingleNode("/newrelic-config:configuration/newrelic-config:service", namespaceManager);
                if (serviceNode == null)
                {
                    Log("Unable to locate /configuration/service in newrelic.config.");
                    return null;
                }

                var licenseKey = serviceNode.GetAttribute("licenseKey", "");
                if (licenseKey == String.Empty)
                {
                    Log("licenseKey node not found in /configuration/service in newrelic.config.");
                    return null;
                }

                if (licenseKey == "REPLACE_WITH_LICENSE_KEY")
                {
                    Log("licenseKey in the previous newrelic.config not set, moving on.");
                    return null;
                }

                return licenseKey;
            }
            catch (Exception exception)
            {
                Log("Unable to get license key from newrelic.config at {0}.\n{1}", path, exception);
                return null;
            }
        }

        private String GetKeyFromFolder(String path)
        {
            if (path == null) return null;

            Log("Looking for newrelic.config inside {0}.", path);

            var newRelicConfigPath = path + @"\newrelic.config";
            if (!File.Exists(newRelicConfigPath))
            {
                Log("newrelic.config not found at {0}.", newRelicConfigPath);

                newRelicConfigPath = path + @"\newrelic.xml";
                if (!File.Exists(newRelicConfigPath))
                {
                    Log("newrelic.config not found at {0}.", newRelicConfigPath);
                    return null;
                }
            }

            return GetKeyFromFile(newRelicConfigPath);
        }

        public override void Log(string message, params object[] arguments)
        {
            CustomActions.Log(session, message, arguments);
        }

        public override void LogError(string message, params object[] arguments)
        {
            CustomActions.Log(session, "New Relic Error: " + message, arguments);
        }

        public override void LogSuccess(string message, params object[] arguments)
        {
            CustomActions.Log(session, "New Relic Success: " + message, arguments);
        }

    }

    internal class ConfigurationMigrator : MySession
    {
        public ConfigurationMigrator(Session session) : base(session)
        { }

        public ActionResult MigrateConfiguration()
        {
            try
            {
                MigrateNewRelicXml();
                MigrateLogs();
                MigrateCustomInstrumentation();
            }
            catch (Exception exception)
            {
                session.Log("Exception occurred while migrating configuration from previous installation.\n{0}", exception);
            }

            return ActionResult.Success;
        }

        private void MigrateNewRelicXml()
        {
            session.Log("Attempting to migrate previous newrelic.config.");
            String destinationPath = session.CustomActionData["NETAGENTCOMMONFOLDER"] + "newrelic.config";

            // If there is already a newrelic.config then do nothing.
            if (File.Exists(destinationPath))
            {
                session.Log("newrelic.config found at {0}, leaving it in place.", destinationPath);
                return;
            }

            String newrelicHomePath = Environment.GetEnvironmentVariable("NEWRELIC_HOME");
            var newrelicHomeXml = null as String;
            if (newrelicHomePath != null) newrelicHomeXml = newrelicHomePath + @"\newrelic.xml";
            var existingXml = session.CustomActionData["NETAGENTCOMMONFOLDER"] + "newrelic.xml";

            // Check each of a number of locations for a newrelic.config to migrate.
            String[] sourcePaths =
            {
                existingXml,
                newrelicHomeXml,
                @"C:\Program Files\New Relic .NET Agent\newrelic.xml",
                @"C:\Program Files (x86)\New Relic .NET Agent\newrelic.xml",
                session.CustomActionData["NETAGENTFOLDER"] + @"\default_newrelic.config"
            };
            foreach (String sourcePath in sourcePaths)
            {
                if (sourcePath == null) continue;
                if (!File.Exists(sourcePath)) continue;

                session.Log("Attempting to move file from {0} to {1}.", sourcePath, destinationPath);
                try
                {
                    SaferFileUtils.FileCopy(sourcePath, destinationPath, this);
                    session.Log("Moved file from {0} to {1}.", sourcePath, destinationPath);
                }
                catch (IOException)
                {
                    session.Log("{0} already exists, leaving {1} in place.", destinationPath, sourcePath);
                }
                return;
            }

            session.Log("Unable to locate a newrelic.config file!");
        }

        private void MigrateLogs()
        {
            session.Log("Attempting to migrate logs from previous installation.");
            String destinationPath = session.CustomActionData["LOGSFOLDER"];

            String homeLogPath = Environment.GetEnvironmentVariable("NEWRELIC_HOME");
            if (homeLogPath != null) homeLogPath += @"\Logs\";

            // Check each of a number of locations for a logs folder to migrate.
            String[] sourcePaths =
            {
                homeLogPath,
                @"C:\Program Files\New Relic .NET Agent\Logs\",
                @"C:\Program Files (x86)\New Relic .NET Agent\Logs\"
            };
            foreach (String sourcePath in sourcePaths)
            {
                if (string.IsNullOrEmpty(sourcePath) || sourcePath == @"\Logs\" || !Directory.Exists(sourcePath)) continue;

                session.Log("Attempting to move contents of directory from {0} to {1}.", sourcePath, destinationPath);
                CopyFolderContents(sourcePath, destinationPath);
                session.Log("Moved directory from {0} to {1}.", sourcePath, destinationPath);
            }
        }

        private void MigrateCustomInstrumentation()
        {
            session.Log("Attempting to migrate previous instrumentation.");
            String destinationPath = session.CustomActionData["AppDataExtensionsFolder"];

            String newrelicHomePath = Environment.GetEnvironmentVariable("NEWRELIC_HOME");
            if (newrelicHomePath != null) newrelicHomePath += @"\Extensions\";

            // Check each of a number of locations for instrumentation files to migrate.
            String[] sourcePaths =
            {
                newrelicHomePath,
                @"C:\Program Files\New Relic .NET Agent\Extensions\",
                @"C:\Program Files (x86)\New Relic .NET Agent\Extensions\"
            };
            foreach (String sourcePath in sourcePaths)
            {
                if (string.IsNullOrEmpty(sourcePath) || !Directory.Exists(sourcePath)) continue;

                session.Log("Attempting to move contents of directory from {0} to {1}.", sourcePath, destinationPath);
                CopyFolderContents(sourcePath, destinationPath);
                session.Log("Moved contents from {0} to {1}.", sourcePath, destinationPath);
            }
        }

        private void CopyFolderContents(string source, string destination)
        {
            if (source == null) return;
            if (destination == null) return;

            // Make sure the destination has a trailing \
            if (!destination.EndsWith(@"\")) destination += @"\";

            // Copy all subdirectories in full if they don't already exist.
            string[] directories = Directory.GetDirectories(source);
            foreach (string sourceDirectoryPath in directories)
            {
                string directoryName = Path.GetFileName(sourceDirectoryPath);
                string destinationPath = destination + directoryName;

                // If the subfolder already exists don't try to copy.
                if (Directory.Exists(destinationPath)) continue;

                SaferFileUtils.DirectoryMove(sourceDirectoryPath, destinationPath, this);
            }

            // Copy all the files in the root of the directory.
            string[] files = Directory.GetFiles(source);
            foreach (string sourceFilePath in files)
            {
                string fileName = Path.GetFileName(sourceFilePath);
                string destinationPath = destination + fileName;

                SaferFileUtils.FileCopy(sourceFilePath, destinationPath, true, this);
                SaferFileUtils.FileDelete(sourceFilePath, this);
            }
        }
    }

    /// <summary>
    /// This class is wrapper around the standard File and Directory classes
    /// that will check the file and directory structure for symlinks and junctions
    /// before attempting to copy or delete the file or directory. This code uses
    /// the presence of reparse points to approximate the usage of junctions or
    /// symlinks. No agent file or directory is expected to have a reparse point
    /// defined.
    /// </summary>
    internal static class SaferFileUtils
    {
        internal static void FileCopy(string source, string destination, MySession logger)
        {
            if (!FileIsSafeToUse(source, logger))
            {
                logger.Log("{0} was not copied.", source);
                return;
            }

            File.Copy(source, destination);
        }

        internal static void FileCopy(string source, string destination, bool overwrite, MySession logger)
        {
            if (!FileIsSafeToUse(source, logger))
            {
                logger.Log("{0} was not copied.", source);
                return;
            }

            File.Copy(source, destination, overwrite);
        }

        internal static void FileDelete(string fileNameAndPath, MySession logger)
        {
            if (!FileIsSafeToUse(fileNameAndPath, logger))
            {
                logger.Log("{0} was not deleted.", fileNameAndPath);
                return;
            }

            File.Delete(fileNameAndPath);
        }

        internal static void DeleteDirectoryAndContents(string path, MySession logger)
        {
            if (!DirectoryIsSafeToUse(path, logger))
            {
                logger.Log("{0} was not deleted.", path);
                return;
            }

            Directory.Delete(path, true);
        }

        internal static void DirectoryMove(string source, string destination, MySession logger)
        {
            if (!DirectoryIsSafeToUse(source, logger))
            {
                logger.Log("{0} was not moved.", source);
                return;
            }

            Directory.Move(source, destination);
        }

        private static bool FileIsSafeToUse(string fileNameAndPath, MySession logger)
        {
            if (!File.Exists(fileNameAndPath))
            {
                return false;
            }

            var fileInfo = new FileInfo(fileNameAndPath);

            if (FileSystemReportsAReparsePoint(fileInfo, logger))
            {
                return false;
            }

            for (var directory = fileInfo.Directory; directory != null; directory = directory.Parent)
            {
                if (FileSystemReportsAReparsePoint(directory, logger))
                {
                    return false;
                }
            }

            return DirectoryAndParentsAreSafe(fileInfo.Directory, logger);
        }

        private static bool DirectoryIsSafeToUse(string path, MySession logger)
        {
            if (!Directory.Exists(path))
            {
                return false;
            }

            var directory = new DirectoryInfo(path);
            if (!DirectoryAndParentsAreSafe(directory, logger))
            {
                return false;
            }

            foreach (var childDirectory in directory.GetDirectories("*", SearchOption.AllDirectories))
            {
                if (FileSystemReportsAReparsePoint(childDirectory, logger))
                {
                    return false;
                }
            }

            foreach (var childFile in directory.GetFiles("*", SearchOption.AllDirectories))
            {
                if (FileSystemReportsAReparsePoint(childFile, logger))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool DirectoryAndParentsAreSafe(DirectoryInfo directoryToCheck, MySession logger)
        {
            for (var directory = directoryToCheck; directory != null; directory = directory.Parent)
            {
                if (FileSystemReportsAReparsePoint(directory, logger))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool FileSystemReportsAReparsePoint(FileSystemInfo fsInfo, MySession logger)
        {
            if((fsInfo.Attributes & System.IO.FileAttributes.ReparsePoint) != 0)
            {
                logger.Log("Reparse point detected for {0}.", fsInfo.FullName);
                return true;
            }

            return false;
        }
    }
}
